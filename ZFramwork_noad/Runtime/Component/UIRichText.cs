using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;

/// <summary>
/// UIRichText
/// 富文本组件
/// @Liukeming 2020-6-12
/// </summary>
public class UIRichText : Text, ICanvasRaycastFilter
{
    //支持的富文本标签，注意，image会产生较多的GC，慎用
    public enum Tags
    {
        //普通文本
        Text,
        //扩展，按渲染顺序排序
        Image,      //image=宽,高,图片位置（0常规，1相对居中）,FPS,图片1;图片2...
        Shadow,
        Outline,    //outline=宽度,插值次数,颜色
        Gradient,   //gradient=开始颜色，结束颜色
        Underline,  //underline=颜色
        Strickout,  //strickout=颜色
        Link,       //link=逗号隔开的参数定义字符串，由LUA层去解析
    }

    //除了基本的可见性，还包含一些可能被重载效果的渲染深度位置
    public struct CharInfo
    {
        public bool visible;
        public byte outlineDeep;
        public byte gradientDeep;
        public byte shadowDeep;
        public byte underlineDeep;
        public byte strickoutDeep;
        public byte linkDeep;
        public int line;//行号，用于获取行位置，显示颜色渐变下划线
        public int renderCharIndex;//渲染的字符索引，用于获取此字符在实际显示的顶点数组中的索引，代表四个顶点
        public Rect rect;
    }

    //渲染效果的结构定义
    public struct Effect
    {
        public Tags tag;
        public int id;//唯一ID，下划线等换行的时候做逻辑判断需要用到

        public int ia;
        public int ib;

        public byte deep;//渲染深度

        public float w;//[image] 宽度
        public float h;//[image] 高度

        public bool mid;//[image] 是否相对文字居中
        public float fps;//[image] FPS

        public float size;//[outline] [underline] 大小
        public int precision;//[outline] 精度

        public Color color;//[gradient] [outline] [underline] 颜色/起始颜色
        public Color color2;//[gradient] 结束颜色

        public string args;//[image click] 符合参数（解析时带引号）
    }

    //字符信息缓存增长步长
    private const int charsCacheStep = 32;

    //全局缓存长度,起始为32,按32增长，只增不减
    //private static CharInfo[] chars = new CharInfo[32];
    private CharInfo[] chars = new CharInfo[32];
    private CharInfo ci;//当前渲染的ci
    public UIVertex[] temp = new UIVertex[4];//4个顶点的缓存
    private UIVertex[] temp2 = new UIVertex[4];//额外缓存，用于Outline
    private UIVertex[] temp3 = new UIVertex[4];//额外缓存，用于Outline以及Underline的下划线文本 
    private string sourceText = null;//解析前的原文本
    private string parsedText = null;//从富文本解析而来的纯文本
    private List<Effect> el = new List<Effect>();//用于渲染的效果列表
    private Stack<Effect> es = new Stack<Effect>();//用于解析效果的临时栈
    private VertexHelper vh = null;
    private UIRichTextParser tp = new UIRichTextParser();//富文本标签解析器
    //private ContentSizeFitter sf = null;

    //保存上一个字符的数据，保证每段效果（比如下划线）独立渲染并且不受【换行】影响
    private int lastEffectID = -1;
    private int lastLine = -1;
    private float lastLeft = -1;


    //一个<image>标签会生成一个UISprite，用于渲染动态图片
    List<UISprite> sprites = new List<UISprite>();
    //渲染的时候，按这个自增索引依次访问sprites列表
    int spriteIndex = 0;

    //手动布局
    public string doLayout()
    {
        if (text == sourceText)
            return parsedText;

        parse();
        layout();
        return parsedText;
    }

    //是否再文本区域内
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        //终点目标点
        Vector2 uiPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPoint,
            eventCamera,
            out uiPoint
            );
        return rectTransform.rect.Contains(uiPoint);
    }

    //重载Text函数
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        //隐藏图片
        for (int i = 0; i < sprites.Count; ++i)
        {
            sprites[i].visible = false;
        }

        if (!parse())
        {
            base.OnPopulateMesh(vertexHelper);

        }
        else
        {
            layout();
            vh = vertexHelper;
            vh.Clear();
            render();
        }
    }

    public override float minWidth => base.minWidth;

    public override float minHeight => base.minHeight;

    public override float preferredWidth
    {
        get
        {
            doLayout();
            var setting = GetGenerationSettings(Vector2.zero);
            return cachedTextGeneratorForLayout.GetPreferredWidth(parsedText, setting);
        }
    }

    public override float preferredHeight
    {
        get
        {
            doLayout();
            float width = rectTransform.rect.width;
            var setting = GetGenerationSettings(new Vector2(width, 0));
            return cachedTextGeneratorForLayout.GetPreferredHeight(parsedText, setting);
        }
    }

    //调整全局缓存的大小以保证能放下渲染的信息，同时刷新visible
    void adjustCharsCacheSize(int cacheSize)
    {
       
        int size = chars.Length;

        while (size < cacheSize)
            size += 32;

        if (size > chars.Length)
        {
            chars = new CharInfo[size];
            for (int i = 0; i != size; ++i)
                chars[i] = new CharInfo();
        }

        for (int i = 0; i != size; ++i)
        {
            chars[i].visible = true;
            chars[i].outlineDeep = 0;
            chars[i].gradientDeep = 0;
            chars[i].shadowDeep = 0;
            chars[i].underlineDeep = 0;
            chars[i].strickoutDeep = 0;
            chars[i].linkDeep = 0;
            chars[i].rect = Rect.zero;
        }
        
    }

    //调整图像的大小，多余的子节点则隐藏，不够的子节点则新增
    void adjustSprites(int index, Effect effect)
    {
        //新增
        RectTransform parentRect = transform.gameObject.GetComponent<RectTransform>();


        if (Application.isPlaying)
        {
            while (sprites.Count <= index)
            {
                GameObject img = new GameObject("image");
                img.transform.SetParent(transform, false);
                UISprite uiSprite = img.AddComponent<UISprite>();
                uiSprite.userLocation = true;
                uiSprite.visible = false;
                RectTransform rt = img.AddComponent<RectTransform>();
                rt.anchorMin = parentRect.anchorMin;
                rt.anchorMax = parentRect.anchorMax;
                sprites.Add(uiSprite);
            }
            UISprite sprite = sprites[index];
            sprite.fps = effect.fps;
            sprite.targets = effect.args;
        }
    }

    //解析富文本，返回是否成功，解析失败时直接调用UNITY内部的渲染
    bool parse()
    {
        sourceText = text;
        el.Clear();
        es.Clear();
        try
        {
            tp.parseEffect(font.fontSize, text, el, es);

            if (el.Count == 0)
            {
                parsedText = text;
                return false;
            }
            else
                parsedText = tp.toString();
        }
        catch (Exception e)
        {
            Debug.Log("解析富文本出错："+e.ToString());
            el.Clear();
            parsedText = text;
        }

        //默认打开文本渲染
        Effect textEffect = new Effect();
        textEffect.tag = Tags.Text;
        textEffect.ia = 0;
        textEffect.ib = parsedText.Length;
        el.Insert(0, textEffect);

        //调整缓存大小
        adjustCharsCacheSize(parsedText.Length);

        //调整图片列表，保证能正确渲染到子节点
        int spriteIndex = 0;
        for (int i = 0; i != el.Count; ++i)
        {
            Effect spriteEffect = el[i];
            if (spriteEffect.tag == Tags.Image)
                adjustSprites(spriteIndex++, spriteEffect);
        }

        //深度缓存
        for (int i = 0; i != el.Count; ++i)
        {
            Effect eff = el[i];
            for (int j = eff.ia; j < eff.ib; ++j)
            {
                switch (eff.tag)
                {
                    case Tags.Image: chars[j].visible = false; break;//image不走文本绘制，不可见，只要留空白
                    case Tags.Outline: chars[j].outlineDeep = eff.deep; break;
                    case Tags.Gradient: chars[j].gradientDeep = eff.deep; break;
                    case Tags.Shadow: chars[j].shadowDeep = eff.deep; break;
                    case Tags.Underline: chars[j].underlineDeep = eff.deep; break;
                    case Tags.Strickout: chars[j].strickoutDeep = eff.deep; break;
                    case Tags.Link: chars[j].linkDeep = eff.deep; break;
                }
            }
        }

        //1.不同标签按定义排序 2.同类型标签按层次排序
        el.Sort((Effect a, Effect b) =>
        {
            if (a.tag != b.tag)
                return a.tag - b.tag;

            if (a.deep != b.deep)
                return a.deep - b.deep;

            return 0;
        });

        return true;
    }

    //设置布局信息
    void layout()
    {
		//如果存在下划线或者删除线，生成下划线'_'模板，下划线将使用它的uv点来渲染，实际是使用了uv范围的中心点（颜色最亮的点）来渲染
		for (int i = 0; i != el.Count; ++i)
		{
			if (el[i].tag == Tags.Underline || el[i].tag == Tags.Strickout || el[i].tag == Tags.Link)
			{
				var setting_underline = GetGenerationSettings(rectTransform.rect.size);
				cachedTextGenerator.Populate("_", setting_underline);
				if (cachedTextGenerator.verts.Count >= 4)
					for (int j = 0; j != 4; ++j)
						temp3[j] = cachedTextGenerator.verts[j];
				break;
			}
		}

        //生成显示的文本，这里有个问题，右对齐且超出范围的时候会按左对齐显示。。。
        var setting = GetGenerationSettings(rectTransform.rect.size);
        cachedTextGenerator.Populate(parsedText, setting);

        //重置字符信息
        for (int i = 0; i != chars.Length; ++i)
            chars[i].visible = false;

        /*
            通过行来依次处理顶点数据，同时计算可见性，位置，顶点索引，三个长度定义如下：
                textCount = 解析后的文本长度
                charCount = Unity内部字符数量（宽度为0的不会渲染）
                vertCount = Unity内部渲染顶点对应的字符数量（顶点数 / 4）

            由于字符信息数组（chars）的长度是我们自己解析出来的，因此需要根据UNITY实际渲染的数量来做一个映射，
            让可见的字符能对应到合适的顶点数据
        */
        int renderdCharIndex = 0;//动态映射的字符顶点索引
        int charCount = cachedTextGenerator.characterCountVisible;
        int vertCount = cachedTextGenerator.verts.Count / 4;
        IList<UICharInfo> cis = cachedTextGenerator.characters;
        for (int i = 0; i != cachedTextGenerator.lineCount; ++i)
        {
            UILineInfo li = cachedTextGenerator.lines[i];
            int istart = li.startCharIdx;
            int iend = i < cachedTextGenerator.lineCount - 1 ? cachedTextGenerator.lines[i + 1].startCharIdx : parsedText.Length;
            
            //正常情况下parsedText.Length和UNITY的字符数量应该是相当的，也就是istart <= iend
            for (int j = istart; j < iend; ++j)
            {
                /*
                    2019.4.15附近的版本在默认情况下UNITY是不会生成内置标签文本的顶点的，
                    但是2019.3.9以及之前的版本版本在多行的情况下（单行正常）依然会对内置标签
                    生成顶点数据，此时charCount和vertCount是相等的，可以全部显示出来
                */
                bool fullVisible = vertCount == cachedTextGenerator.characterCount;
                bool charVisible = fullVisible || cis[j].charWidth > 0;
                if (j < parsedText.Length && j < charCount && charVisible && renderdCharIndex < vertCount)
                {
                    chars[j].visible = true;
                    chars[j].renderCharIndex = renderdCharIndex++;
                    chars[j].line = i;

                    //左下角+字符宽+行高构成字符的整体大小，用于响应点击
                    int rci = chars[j].renderCharIndex;
                    Vector2 lb = cachedTextGenerator.verts[rci * 4 + 3].position;
                    Vector2 rt = cachedTextGenerator.verts[rci * 4 + 1].position;
                    chars[j].rect = new Rect(lb.x, li.topY - li.height, rt.x - lb.x, li.height);
                }
            }
        }
    }

    //渲染全局共享的四个顶点数据缓存
    void submit()
    {
        for (int i = 0; i != 4; ++i)
            temp[i].position /= pixelsPerUnit;
        vh.AddUIVertexQuad(temp);
    }

    //渲染所有通道
    void render()
    {
        //重置要渲染的sprite
        spriteIndex = 0;

        //依次渲染效果
        for (int j = 0; j != el.Count; ++j)
        {
            Effect eff = el[j];
            lastEffectID = -1;
            lastLine = -1;
            lastLeft = -1;
            for (int i = eff.ia; i != eff.ib; ++i)
            {
                if(i >= 0 && i < chars.Length && chars[i].visible)
                {
                    ci = chars[i];
                    renderEffect(eff);
                }
            }
        }
    }

    //清空
    public void clear()
    {
        for (int i = sprites.Count-1; i>=0; i-- )
        {
            Destroy(sprites[i].gameObject);
            sprites.RemoveAt(i);
        }
    }

    //渲染单个字符单个通道
    void renderEffect(Effect eff)
    {
        for (int i = 0; i != 4; ++i)
            temp[i] = cachedTextGenerator.verts[ci.renderCharIndex * 4 + i];

        switch (eff.tag)
        {
            case Tags.Text:
                if (ci.gradientDeep == 0)
                    applyText();
                break;
            case Tags.Image:
                applyImage(eff);
                break;
            case Tags.Outline:
                if (ci.outlineDeep == eff.deep)
                    applyOutline(eff);
                break;
            case Tags.Gradient:
                if (ci.gradientDeep == eff.deep)
                    applyGradient(eff);
                break;
            case Tags.Underline:
                if (ci.underlineDeep == eff.deep)
                    applyUnderline(eff);
                break;
            case Tags.Strickout:
                if (ci.strickoutDeep == eff.deep)
                    applyStrickout(eff);
                break;
            case Tags.Link:
                if (ci.linkDeep == eff.deep)
                    applyLink(eff);
                break;
        }
    }

    //渲染单个字符普通文本
    void applyText()
    {
        submit();
    }

    //渲染一个图片，EDITOR模式下不可见
    void applyImage(Effect eff)
    {
        if (Application.isPlaying)
        {
            //动态定位
            UISprite sprite = sprites[spriteIndex];
            //float w = temp[1].position.x - temp[0].position.x;
            //float h = temp[1].position.y - temp[2].position.y;
            float x = (temp[1].position.x + temp[3].position.x) / 2;
            float y = (temp[1].position.y + temp[3].position.y) / 2;
            sprite.size = new Vector2(eff.w, eff.h);
            sprite.visible = true;

            if (!eff.mid)
                sprite.position = new Vector2(x / pixelsPerUnit, y / pixelsPerUnit);
            else
                sprite.position = new Vector2(x / pixelsPerUnit, y / pixelsPerUnit);

            //每次渲染都会从0开始转增的索引，一定会全部遍历到
            spriteIndex++;
        }
    }

    //渲染单个字符描边，一个方向以及一个象限的插值，描边不受外部颜色影响
    void applyOutlineOnce(Effect eff, float dx, float dy, float rx, float ry)
    {
        Color outlineColor = eff.color;
        outlineColor.a *= color.a;

        //基本的偏移（调四次会有四个方向）
        for (int i = 0; i != 4; ++i)
        {
            temp[i] = temp2[i];
            temp[i].position.x += dx;
            temp[i].position.y += dy;
            temp[i].color = outlineColor;
        }
        submit();

        //插值，范围是处于基本的偏移之间的，一次一个象限，四次画完所有方向
        float sampleAngle = (Mathf.PI / 2) / (eff.precision + 1);
        for (int j = 0; j != eff.precision; ++j)
        {
            float angle = sampleAngle * (j + 1);
            float x = rx * Mathf.Cos(angle);
            float y = ry * Mathf.Sin(angle);

            for (int i = 0; i != 4; ++i)
            {
                temp[i] = temp2[i];
                temp[i].position.x += x;
                temp[i].position.y += y;
                temp[i].color = outlineColor;
            }
            submit();
        }

        //上面再覆盖一层原版文字
        for (int i = 0; i != 4; ++i)
            temp[i] = temp3[i];
        submit();
    }

    //渲染单个字符描边，调用四次单边渲染完成所有方向
    void applyOutline(Effect eff)
    {
        for (int i = 0; i != 4; ++i)
        {
            temp2[i] = temp[i];
            temp3[i] = temp[i];
        }

        applyOutlineOnce(eff, -eff.size, 0, -eff.size, eff.size);
        applyOutlineOnce(eff, eff.size, 0, eff.size, eff.size);
        applyOutlineOnce(eff, 0, -eff.size, eff.size, -eff.size);
        applyOutlineOnce(eff, 0, eff.size, -eff.size, -eff.size);
    }

    //渲染单个字符渐变
    void applyGradient(Effect eff)
    {
        UILineInfo li = cachedTextGenerator.lines[ci.line];
        float bottom = li.topY - li.height;
        for (int i = 0; i != 4; ++i)
        {
            float v = (li.topY - temp[i].position.y) / li.height;//从上到下算
            temp[i].color = new Color(
                Mathf.Lerp(eff.color.r, eff.color2.r, v),
                Mathf.Lerp(eff.color.g, eff.color2.g, v),
                Mathf.Lerp(eff.color.b, eff.color2.b, v),
                Mathf.Lerp(eff.color.a, eff.color2.a, v) * color.a
                );
        }
        submit();
    }

    //渲染单个字符下划线
    void applyUnderline(Effect eff)
    {
        UILineInfo li = cachedTextGenerator.lines[ci.line];
        Color underlineColor = eff.color;
        underlineColor.a *= color.a;

        //同一个Underline渲染，同一行，将被连接在一起
        bool contact = lastLine == ci.line && lastEffectID == eff.id;
        float left = contact ? lastLeft : temp[0].position.x;
        float right = temp[1].position.x;
        float height = (temp3[1].position.y - temp3[2].position.y) / 2;
        float bottomOffset = height * 0.2f;//距离底部需要一定距离
        float top = li.topY - li.height + bottomOffset + height;
        float bottom = li.topY - li.height + bottomOffset;

        if (ci.line != lastLine)
            lastLine = ci.line;

        lastEffectID = eff.id;
        lastLeft = right;

        //通过左上和右下两个点将4个点的UV都设置为中心点（原下划线不是铺满网格的，中点最亮）
        Vector2 centerUV = (temp3[0].uv0 + temp3[2].uv0) / 2;
        for (int i = 0; i != 4; ++i)
        {
            temp[i] = temp3[i];
            temp[i].uv0 = centerUV;
            temp[i].color = underlineColor;
        }

        temp[0].position.x = left;
        temp[0].position.y = top;
        temp[1].position.x = right;
        temp[1].position.y = top;
        temp[2].position.x = right;
        temp[2].position.y = bottom;
        temp[3].position.x = left;
        temp[3].position.y = bottom;
        submit();
    }

    //渲染单个字符删除线
    void applyStrickout(Effect eff)
    {
        UILineInfo li = cachedTextGenerator.lines[ci.line];
        Color strickoutColor = eff.color;
        strickoutColor.a *= color.a;

        //同一个Underline渲染，同一行，将被连接在一起
        bool contact = lastLine == ci.line && lastEffectID == eff.id;
        float left = contact ? lastLeft : temp[0].position.x;
        float right = temp[1].position.x;
        float height = (temp3[1].position.y - temp3[2].position.y) / 2;
        float top = li.topY - li.height / 2 + height / 2;
        float bottom = li.topY - li.height / 2 - height / 2;

        if (ci.line != lastLine)
            lastLine = ci.line;

        lastEffectID = eff.id;
        lastLeft = right;

        //通过左上和右下两个点将4个点的UV都设置为中心点（原下划线不是铺满网格的，中点最亮）
        Vector2 centerUV = (temp3[0].uv0 + temp3[2].uv0) / 2;
        for (int i = 0; i != 4; ++i)
        {
            temp[i] = temp3[i];
            temp[i].uv0 = centerUV;
            temp[i].color = strickoutColor;
        }

        temp[0].position.x = left;
        temp[0].position.y = top;
        temp[1].position.x = right;
        temp[1].position.y = top;
        temp[2].position.x = right;
        temp[2].position.y = bottom;
        temp[3].position.x = left;
        temp[3].position.y = bottom;
        submit();
    }

    //渲染链接，渲染过程类似下划线
    void applyLink(Effect eff)
    {
        UILineInfo li = cachedTextGenerator.lines[ci.line];
        Color linkColor = temp[0].color;//连接的下划线跟文字一样

        //同一个Underline渲染，同一行，将被连接在一起
        bool contact = lastLine == ci.line && lastEffectID == eff.id;
        float left = contact ? lastLeft : temp[0].position.x;
        float right = temp[1].position.x;
        float height = (temp3[1].position.y - temp3[2].position.y) / 2;
        float bottomOffset = height * 0.2f;//距离底部需要一定距离
        float top = li.topY - li.height + bottomOffset + height;
        float bottom = li.topY - li.height + bottomOffset;

        if (ci.line != lastLine)
            lastLine = ci.line;

        lastEffectID = eff.id;
        lastLeft = right;

        //通过左上和右下两个点将4个点的UV都设置为中心点（原下划线不是铺满网格的，中点最亮）
        Vector2 centerUV = (temp3[0].uv0 + temp3[2].uv0) / 2;
        for (int i = 0; i != 4; ++i)
        {
            temp[i] = temp3[i];
            temp[i].uv0 = centerUV;
            temp[i].color = linkColor;
        }

        temp[0].position.x = left;
        temp[0].position.y = top;
        temp[1].position.x = right;
        temp[1].position.y = top;
        temp[2].position.x = right;
        temp[2].position.y = bottom;
        temp[3].position.x = left;
        temp[3].position.y = bottom;
        submit();
    }
     
}
