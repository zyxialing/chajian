using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum CameraShakeType
{
    shake_1
}
public class CameraUtils
{

    public static Camera CreateCamer(string name)
    {
        Camera camera = new GameObject(name, new Type[] { typeof(Camera) }).GetComponent<Camera>();
        var listenerObj = new GameObject("audioListener");
        listenerObj.transform.SetParent(camera.transform);
        listenerObj.transform.localPosition = Vector3.forward * 100;
        listenerObj.AddComponent<AudioListener>();

        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        return camera;
    }
    public static void SetUICameraParma(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.Nothing;
        camera.cullingMask = 1 << 5;
        camera.depth = 9000;
        camera.orthographic = true;
        camera.transform.position = Vector3.back * 10000;
    }

    public static void SetSceneCameraParma(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.cullingMask = 1 << 0;
        camera.depth = 100;
        camera.transform.position = Vector3.zero;
    }




    #region 相机动画
    public static void ShakeCamera(Camera camera,CameraShakeType shakeType)
    {
       
    }
    #endregion
}
