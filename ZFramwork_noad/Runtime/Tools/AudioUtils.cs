//using BehaviorDesigner.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class AudioUtils
{
    public static AudioClip GetAudio(string path)
    {
        return ResHanderManager.Instance.GetAudio(path);
    }

    public static void GetAudioAsync(string path, Action<AudioClip> callBack)
    {
        ResHanderManager.Instance.GetAudioAsync(path, callBack);
    }

    public static void Release(string path)
    {
        ResHanderManager.Instance.ReleaseRes(path);
    }
}
