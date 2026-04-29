using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIUtils
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
    /// <param name="isUI">isUI �Ƿ���UI·��</param>
    public static void SetSprite(Image image,string path)
    {
        image.sprite = ResHanderManager.Instance.GetSprite(path);
    }

    public static Sprite GetSprite(string path)
    {
        return ResHanderManager.Instance.GetSprite(path);
    }

    public static Sprite[] GetSprites(string path)
    {
        return ResHanderManager.Instance.GetSprites(path);
    }

    public static void Release(string path)
    {
        ResHanderManager.Instance.ReleaseRes(path);
    }
}
