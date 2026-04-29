using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class TextAssetUtils
{
    public static TextAsset GetTextAsset(string path)
    {
        return ResHanderManager.Instance.GetTextAsset(path);
    }


    public static void Release(string path)
    {
        ResHanderManager.Instance.ReleaseRes(path);
    }
}
