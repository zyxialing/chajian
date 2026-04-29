using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// UIRichTextParser
/// 富文本解析器
/// @Liukeming 2020-6-12
/// </summary>
public class UIRichTextParser
{
    //used for match richtext tags
    struct TagPattern
    {
        public string name;
        public UIRichText.Tags tag;
    }

    static TagPattern[] patterns =
        {
            new TagPattern(){ name="image", tag = UIRichText.Tags.Image},
            new TagPattern(){ name="shadow", tag = UIRichText.Tags.Shadow},
            new TagPattern(){ name="outline", tag = UIRichText.Tags.Outline},
            new TagPattern(){ name="text", tag = UIRichText.Tags.Text},
            new TagPattern(){ name="gradient", tag = UIRichText.Tags.Gradient},
            new TagPattern(){ name="underline", tag = UIRichText.Tags.Underline},
            new TagPattern(){ name="strickout", tag = UIRichText.Tags.Strickout},
            new TagPattern(){ name="link", tag = UIRichText.Tags.Link},
        };

    private StringBuilder sb = new StringBuilder();//save the pure text
    private string s;//reference of Text.text
    private int i;
    private int iend;

    public void parseEffect(int fontSize, string text, List<UIRichText.Effect> el, Stack<UIRichText.Effect> es)
    {
        s = text;
        i = 0;
        iend = s.Length;
        sb.Clear();
        sb.Length = 0;

        while (i < iend)
        {
            if (s[i] == '<')
            {
                ++i;
                if (i < iend && s[i] == '/')
                {
                    //闭标签
                    ++i;
                    int iParttern = getPartternIndex();
                    if (iParttern > 0)
                    {
                        TagPattern pat = patterns[iParttern];
                        UIRichText.Effect eff = es.Pop();
                        if (eff.tag != pat.tag)
                            throw new Exception("[unmatched tag]");
                        else
                        {
                            parseClose();
                            eff.ib = sb.Length;
                            eff.id = el.Count;
                            el.Insert(0, eff);//es栈是后入式，el线表需要从0插入来保持文本的渲染顺序
                        }
                    }
                    else
                    {
                        //普通文本的"</"
                        sb.Append("</");
                        parseText('>', false);
                    }
                }
                else
                {
                    //开标签
                    int iParttern = getPartternIndex();
                    if (iParttern >= 0)
                    {
                        TagPattern pat = patterns[iParttern];
                        UIRichText.Effect eff = new UIRichText.Effect();
                        eff.tag = pat.tag;
                        eff.deep = (byte)es.Count;
                        parseArgs(ref eff);
                        eff.ia = sb.Length;

                        //image标签自闭，EDITOR模式下就简单显示个白底就行了
                        if (eff.tag == UIRichText.Tags.Image)
                        {
                            //向下偏移时，字体大小会变化
                            float imgFontSize = eff.h;
                            float imgWidthRate = eff.w / eff.h;

                            if (eff.mid)
                            {
                                imgFontSize = fontSize;
                                imgWidthRate = eff.w / imgFontSize;
                            }

                            sb.Append(string.Format("<quad size={0} width={1} color=#0000 />", imgFontSize, imgWidthRate));

                            eff.ib = eff.ia + 1;//只需要渲染一次
                            eff.id = el.Count;
                            el.Insert(0, eff);//es栈是后入式，el线表需要从0插入来保持文本的渲染顺序
                        }
                        else
                            es.Push(eff);
                    }
                    else
                    {
                        //普通文本的"<"
                        sb.Append("<");
                        parseText('>', false);
                    }
                }
            }
            else
                parseText();
        }

        //清理没处理完的Effect
        while (es.Count > 0)
        {
            UIRichText.Effect eff = es.Pop();
            eff.ib = sb.Length;
            eff.id = el.Count;
            el.Insert(0, eff);//es栈是后入式，el线表需要从0插入来保持文本的渲染顺序
        }
    }

    //提取普通文本
    private void parseText(char endc = '<', bool isNormal = true)
    {
        while (i < iend && s[i] != endc)
        {
            char c = s[i++];
            sb.Append(c == ' ' ? '\u00A0' : c);//空格会被UNITY强行优化，这里替换成普通文本
        }

        if (!isNormal && s[i] == endc)
            sb.Append(s[i++]);
    }

    //去掉标签内部的无效空白
    private void parseBlank()
    {
        while (" \t\r\n".IndexOf(s[i]) >= 0)
            ++i;
    }

    //提取可解析的标签ID(TagPartern枚举类型)，没有匹配时返回-1
    private int getPartternIndex()
    {
        int iPattern = -1;
        for (int j = 0; j != patterns.Length; ++j)
        {
            if (string.CompareOrdinal(s, i, patterns[j].name, 0, patterns[j].name.Length) == 0)
            {
                iPattern = j;
                i += patterns[j].name.Length;
                parseBlank();
                break;
            }
        }
        return iPattern;
    }

    //提取参数，从=开始，没有参数定义则直接返回
    private int parseArgs(ref UIRichText.Effect eff)
    {
        if (s[i] != '=')
            parseBlank();
        else
        {
            ++i;
            parseBlank();
            switch (eff.tag)
            {
                case UIRichText.Tags.Image:
                    eff.w = parseInt();
                    eff.h = parseInt();
                    eff.mid = parseInt() == 1;
                    eff.fps = parseFloat();
                    eff.args = parseString();
                    parseBlank();
                    if (s[i++] != '/')
                        throw new Exception("need / to close image");
                    break;
                case UIRichText.Tags.Gradient:
                    eff.color = parseColor();
                    eff.color2 = parseColor();
                    break;
                case UIRichText.Tags.Outline:
                    eff.size = parseInt();
                    eff.precision = parseInt();
                    eff.color = parseColor();
                    break;
                case UIRichText.Tags.Underline:
                    eff.color = parseColor();
                    break;
                case UIRichText.Tags.Strickout:
                    eff.color = parseColor();
                    break;
                case UIRichText.Tags.Link:
                    eff.args = parseString();
                    break;
            }
        }
        return parseClose();
    }

    //关闭标签的右方括号
    private int parseClose()
    {
        parseBlank();

        if (s[i++] != '>')
            throw new Exception("[tag format error]");
        else
            return i;
    }

    //add to buffer
    void addString(int a, int b)
    {
        for (int i = a; i != b; ++i)
            sb.Append(s[i]);
    }

    //no gc parse int
    int parseInt()
    {
        int ret = 0;
        while (i < iend && s[i] != ',' && s[i] != '>')
        {
            ret = ret * 10 + s[i++] - '0';
        }
        parseBlank();
        if (s[i] == ',')
            ++i;
        return ret;
    }

    //no gc parse float
    float parseFloat()
    {
        float head = 0;
        float tail = 0;
        bool isTail = false;
        float deep = 1;
        while (i < iend && s[i] != ',' && s[i] != '>')
        {
            if (s[i] == '.')
            {
                isTail = true;
                ++i;
            }
            else
            {
                if (!isTail)
                    head = head * 10 + s[i++] - '0';
                else
                {
                    deep *= 10;
                    tail = tail * 10 + s[i++] - '0';
                }
            }
        }
        parseBlank();
        if (s[i] == ',')
            ++i;
        return head + tail / deep;
    }

    //解析字符串，字符串遇到/>也退出，为了兼容<image/>标签
    string parseString()
    {
        int istart = i;
        while (i < iend && s[i] != ',' && s[i] != '>')
        {
            if (s[i] == '/' && i + 1 < iend && s[i + 1] == '>')
                break;
            else
                ++i;
        }
        if (s[i] == ',')
            ++i;
        return s.Substring(istart, i - istart);
    }

    //no gc parse color
    Color parseColor()
    {
        Color c = Color.black;

        if (s[i++] != '#')
            return c;

        bool isDouble = (s[i + 3] != ',' && s[i + 3] != '>' && s[i + 4] != ',' && s[i + 4] != '>');
        c.r = parseHex(isDouble) / 255.0f;
        c.g = parseHex(isDouble) / 255.0f;
        c.b = parseHex(isDouble) / 255.0f;
        c.a = (s[i] != ',' && s[i] != '>') ? parseHex(isDouble) / 255.0f : 1;
        parseBlank();
        if (s[i] == ',')
            ++i;
        return c;
    }

    int parseHex4()
    {
        char c = s[i++];
        if (c >= '0' && c <= '9')
            return c - '0';
        else if (c >= 'A' && c <= 'Z')
            return c - 'A' + 10;
        else
            return c - 'a' + 10;
    }

    int parseHex(bool isDouble)
    {
        int h = parseHex4();
        h = h * 16 + (isDouble ? parseHex4() : h);
        return h;
    }

    public string toString()
    {
        return sb.ToString(0, sb.Length);
    }
}