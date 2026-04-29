//using BehaviorDesigner.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class AIUtils
{
    //public static ExternalBehavior GetExternalBehavior(string path)
    //{
    //    return ResHanderManager.Instance.GetAI(path);
    //}

    public static void Release(string path)
    {
        ResHanderManager.Instance.ReleaseRes(path);
    }
}
