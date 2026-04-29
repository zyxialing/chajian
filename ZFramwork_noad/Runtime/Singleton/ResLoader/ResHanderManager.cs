//using BehaviorDesigner.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public class ResHanderManager : Singleton<ResHanderManager>
{
    private Dictionary<string, ResHander> _dicRes;

    private ResHanderManager()
    {
        _dicRes = new Dictionary<string, ResHander>();
        //////////////////////////////////////////////////////////////////
    }

    /// <summary>
    /// 预加载只能写入全路径
    /// </summary>
    /// <param name="list"></param>
    public void PreloadAudioAssets(List<string> list,Action callBack)
    {
        if(list==null || list.Count == 0)
        {
            callBack?.Invoke();
            return;
        }
        int loadCount = 0;
        for (int i = 0; i < list.Count; i++)
        {
            string path = list[i];
            if (!_dicRes.ContainsKey(path))
            {
                AsyncOperationHandle hander = Addressables.LoadAssetAsync<AudioClip>(path);
                hander.Completed += obj =>
                {
                    _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
                    loadCount++;
                    if (loadCount == list.Count)
                    {
                        callBack?.Invoke();
                    }
                };

            }
        }
    }

    #region 获取 添加引用计数
    public AudioClip GetAudio(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as AudioClip;
        }
        else
        {
           var hander = ResLoader.Instance.GetAudioClip(path);
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as AudioClip;
        }
    }

    public void GetAudioAsync(string path, Action<AudioClip> callBack)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++;

            if (res.hander.IsDone)
            {
                callBack?.Invoke(res.hander.Result as AudioClip);
            }
            else
            {
                res.hander.Completed += obj =>
                {
                    callBack?.Invoke(obj.Result as AudioClip);
                };
            }

            return;
        }

        AsyncOperationHandle hander = ResLoader.Instance.GetAudioClip(path, null);
        _dicRes.Add(path, new ResHander(hander));
        hander.Completed += obj =>
        {
            callBack?.Invoke(obj.Result as AudioClip);
        };
    }
    //public ExternalBehavior GetAI(string path)
    //{
    //    if (_dicRes.ContainsKey(path))
    //    {
    //        var res = _dicRes[path];
    //        res.count++; //引用计数加1
    //        return res.hander.Result as ExternalBehavior;
    //    }
    //    else
    //    {
    //        var hander = ResLoader.Instance.GetAI(path);
    //        _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
    //        return _dicRes[path].hander.Result as ExternalBehavior;
    //    }
    //}
    public Material GetMaterial(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as Material;
        }
        else
        {
            var hander = ResLoader.Instance.GetMaterial(path);
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as Material;
        }
    }
    /// <summary>
    /// 默认UI路径
    /// </summary>
    /// <param name="path"></param>
    /// <param name="objType"></param>
    /// <returns></returns>
    public GameObject GetGameObject(string path,bool objType)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as GameObject;
        }
        else
        {
            AsyncOperationHandle hander;
            if (objType)
            {
                hander = ResLoader.Instance.GetUIPrefab(path);
            }
            else
            {
                hander = ResLoader.Instance.GetGamePrefab(path);
            }
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as GameObject;
        }
    }
    /// <summary>
    /// 默认UI路径
    /// </summary>
    /// <param name="path"></param>
    /// <param name="objType"></param>
    /// <returns></returns>
    public Sprite GetSprite(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as Sprite;
        }
        else
        {
            AsyncOperationHandle hander = ResLoader.Instance.GetUISprite(path);
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as Sprite;
        }
    }
        /// <summary>
    /// 默认UI路径
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public Sprite[] GetSprites(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as Sprite[];
        }
        else
        {
            AsyncOperationHandle hander = ResLoader.Instance.GetSprites(path);
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as Sprite[];
        }
    }
    /// <summary>
    /// 默认UI路径
    /// </summary>
    /// <param name="path"></param>
    /// <param name="objType"></param>
    /// <returns></returns>
    public Texture GetTexture(string path, bool objType)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as Texture;
        }
        else
        {
            AsyncOperationHandle hander;
            if (objType)
            {
                hander = ResLoader.Instance.GetUITexture(path);
            }
            else
            {
                hander = ResLoader.Instance.GetGameTexture(path);
            }
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as Texture;
        }
    }
    public TextAsset GetTextAsset(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as TextAsset;
        }
        else
        {
            var hander = ResLoader.Instance.GetTextAsset(path);
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as TextAsset;
        }
    }
    public RuntimeAnimatorController GetGetRuntimeAnimatorController(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as RuntimeAnimatorController;
        }
        else
        {
            var hander = ResLoader.Instance.GetRuntimeAnimatorController(path);
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as RuntimeAnimatorController;
        }
    }
    public Shader GetShader(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var res = _dicRes[path];
            res.count++; //引用计数加1
            return res.hander.Result as Shader;
        }
        else
        {
            var hander = ResLoader.Instance.GetShader(path);
            _dicRes.Add(path, new ResHander(hander));//默认引用计数为1
            return _dicRes[path].hander.Result as Shader;
        }
    }
    #endregion
    #region 释放 减少引用计数
    public void ReleaseRes(string path)
    {
        if (_dicRes.ContainsKey(path))
        {
            var resHander = _dicRes[path];
            resHander.count = 0; //引用计数减1
#if UNITY_EDITOR
            if (resHander.count < 0)
            {
                Debug.LogError(string.Format("{0}释放音效有问题:{1}",path, resHander.count));
            }
#endif
        }
    }
    #endregion
}

class ResHander
{
    public int count;
    public AsyncOperationHandle hander;

    public ResHander(AsyncOperationHandle hander)
    {
        this.hander = hander;
        this.count = 1;
    }

}

