using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UISprite
/// 动态图片
/// @Liukeming 2020-6-12
/// 
/// 为了支持动态加载和实时修改，内部保存一套数据用以比较不同
/// </summary>
[DisallowMultipleComponent]
public class UISprite : MonoBehaviour
{
    class Chip
    {
        public string path = null;//资源路径
        public Sprite sprite = null;//加载的图片
        public bool loading = false;//是否加载中
    }

    //公开数据
    public bool visible = true;//是否可见
    public float fps = 0;//每秒切换多少次
    public string targets = null;//指定的目标路径，分号隔开
    public bool userLocation = false;//是否由用户指定大小和位置
    public Vector2 size = Vector2.zero;//指定的目标大小
    public Vector2 position = Vector2.zero;//指定的目标位置

    //保存数据
    private bool visible_saved = true;
    private float fps_saved = 0;
    private string targets_saved = null;
    private Vector2 size_saved = Vector2.zero;
    private Vector2 position_saved = Vector2.zero;
    private List<Chip> chips = new List<Chip>();

    private Image image = null;
    private RectTransform rt = null;
    private CanvasGroup cg = null;
    private int frame = 0;
    private float time = 0;

    void Update()
    {
        //延迟加载组件，以兼容在富文本中动态创建
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;//取消点击，防止遮挡富文本
        }

        if(rt == null)
            rt = GetComponent<RectTransform>();

        if (cg == null)
            cg = gameObject.AddComponent<CanvasGroup>();

        //是否可见
        if(visible != visible_saved)
        {
            visible_saved = visible;
            cg.alpha = visible_saved ? 1 : 0;
        }

        if (visible)
        {

            //监听FPS
            if (fps != fps_saved)
            {
                fps_saved = fps;
                frame = 0;
                time = 0;
            }

            //监听路径
            if (targets != targets_saved)
            {
                targets_saved = targets;
                chips.Clear();
                frame = 0;
                time = 0;
                string[] paths = targets.Split(';');
                foreach (string path in paths)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        Chip chip = new Chip();
                        chip.path = path;
                        if (Application.isPlaying)
                        {
                            chip.sprite = UIUtils.GetSprite(path);
                        }
                        chips.Add(chip);
                    }
                }
            }

            //大小和路径
            if (rt != null && userLocation)
            {
                if (size != size_saved)
                {
                    size_saved = size;
                    rt.sizeDelta = size_saved;
                }

                if (position != position_saved)
                {
                    position_saved = position;
                    rt.localPosition = position_saved;
                }
            }

            //刷新帧
            if (chips.Count > 0)
            {
                if (fps > 0)
                {
                    float spf = 1 / fps;
                    time += Time.deltaTime;

                    if (time > spf)
                    {
                        frame = (frame + 1) % chips.Count;
                        time -= spf;
                    }
                }

                Chip chip = chips[frame];
                if (chip.loading || chip.sprite == null)
                    image.color = new Color(0, 0, 0, 0);
                else if (chip.sprite != image.sprite)
                {
                    image.sprite = chip.sprite;
                    image.color = Color.white;
                }
            }
        }
    }
}
