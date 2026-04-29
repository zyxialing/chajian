//using BehaviorDesigner.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class ResLoader : Singleton<ResLoader>
{
    private ResLoader()
    {
       
    }

    public AsyncOperationHandle GetAudioClip(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<AudioClip>(AdressablePath.Instance.audio_path + path);
        hander.Completed += obj =>
        {
            callBack?.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetAudioClip(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<AudioClip>(AdressablePath.Instance.audio_path + path);
        hander.WaitForCompletion();
        return hander;
    }
    /// <summary>
    /// 异步加载ai
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="callBack">回调</param>
    /// <returns></returns>
    //public AsyncOperationHandle GetAI(string path, Action<AsyncOperationHandle> callBack)
    //{
    //    AsyncOperationHandle hander = Addressables.LoadAssetAsync<ExternalBehavior>(path);
       
    //    hander.Completed += obj =>
    //    {
    //        callBack?.Invoke(obj);
    //    };
    //    return hander;
    //}
    /// <summary>
    /// 同步加载ai
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns></returns>
    //public AsyncOperationHandle GetAI(string path)
    //{
    //    AsyncOperationHandle hander = Addressables.LoadAssetAsync<ExternalBehavior>(path);
    //    hander.WaitForCompletion();
    //    return hander;
    //}

    public AsyncOperationHandle GetMaterial(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Material>(AdressablePath.Instance.material_path + path);
        hander.Completed += obj =>
        {
            callBack?.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetMaterial(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Material>(AdressablePath.Instance.material_path + path);
        hander.WaitForCompletion();
        return hander;
    }

    public AsyncOperationHandle GetGamePrefab(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<GameObject>(AdressablePath.Instance.prefab_game_path + path);
        hander.Completed += obj =>
        {
            callBack?.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetGamePrefab(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<GameObject>(AdressablePath.Instance.prefab_game_path + path);
        hander.WaitForCompletion();
        return hander;
    }

    public AsyncOperationHandle GetUIPrefab(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<GameObject>(AdressablePath.Instance.prefab_ui_path + path);
        hander.Completed += obj =>
        {
            callBack?.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetUIPrefab(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<GameObject>(AdressablePath.Instance.prefab_ui_path + path);
        hander.WaitForCompletion();
        return hander;
    }

    public AsyncOperationHandle GetScene(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadSceneAsync(AdressablePath.Instance.scene_path + path);
        hander.Completed += obj =>
        {
            callBack?.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetScene(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadSceneAsync(AdressablePath.Instance.scene_path + path);
        hander.WaitForCompletion();
        return hander;
    }

    public AsyncOperationHandle GetUISprite(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Sprite>(AdressablePath.Instance.sprite_ui_path + path);
        hander.Completed += obj =>
        {
            callBack.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetUISprite(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Sprite>(path);
        hander.WaitForCompletion();
        return hander;
    }

        public AsyncOperationHandle GetSprites(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Sprite[]>(path);
        hander.WaitForCompletion();
        return hander;
    }

    public AsyncOperationHandle GetGameTexture(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Texture>(AdressablePath.Instance.texture_game_path + path);
        hander.Completed += obj =>
        {
            callBack.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetGameTexture(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Texture>(AdressablePath.Instance.texture_game_path + path);
        hander.WaitForCompletion();
        return hander;
    }

    public AsyncOperationHandle GetUITexture(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Texture>(AdressablePath.Instance.texture_ui_path + path);
        hander.Completed += obj =>
        {
            callBack.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetUITexture(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Texture>(AdressablePath.Instance.texture_ui_path + path);
        hander.WaitForCompletion();
        return hander;
    }

    public AsyncOperationHandle GetTextAsset(string path, Action<AsyncOperationHandle> callBack)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<TextAsset>(path);
        hander.Completed += obj =>
        {
            callBack.Invoke(obj);
        };
        return hander;
    }

    public AsyncOperationHandle GetTextAsset(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<TextAsset>(path);
        hander.WaitForCompletion();
        return hander;
    }
    public AsyncOperationHandle GetRuntimeAnimatorController(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<RuntimeAnimatorController>(path);
        hander.WaitForCompletion();
        return hander;
    }
    public AsyncOperationHandle GetShader(string path)
    {
        AsyncOperationHandle hander = Addressables.LoadAssetAsync<Shader>(path);
        hander.WaitForCompletion();
        return hander;
    }
}
