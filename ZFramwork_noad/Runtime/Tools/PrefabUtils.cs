using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class PrefabUtils
{
    public static GameObject Instance(string path,bool isUI=false)
    {
        var obj = ResHanderManager.Instance.GetGameObject(path, isUI);
        return GameObject.Instantiate(obj);
    }
    public static GameObject Instance(string path,Transform parent,bool isUI = false)
    {
        var obj = ResHanderManager.Instance.GetGameObject(path, isUI);
        return GameObject.Instantiate(obj, parent);
    }
    public static GameObject Instance(string path, Vector3 position,Quaternion rotation, Transform parent, bool isUI = false)
    {
        var obj = ResHanderManager.Instance.GetGameObject(path,isUI);
        return GameObject.Instantiate(obj, position, rotation, parent);
    }
    public static GameObject Instance(string path, Transform parent,bool instantiateInWorldSpace, bool isUI = false)
    {
        var obj = ResHanderManager.Instance.GetGameObject(path, isUI);
        return GameObject.Instantiate(obj, parent, instantiateInWorldSpace);
    }

    public static void Release(string path)
    {
        ResHanderManager.Instance.ReleaseRes(path);
    }
}
