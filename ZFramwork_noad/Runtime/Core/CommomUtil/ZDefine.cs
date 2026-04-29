using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 通用的常量，开关，宏都在这里
/// </summary>
public class ZDefine
{
    #region 开关
    /// <summary>
    /// 日志统一开关
    /// </summary>
    public const bool ZLog= true;
    public const bool ZLogNormal= true;
    public const bool ZLogWarning= true;
    public const bool ZLogError= true;
    public const bool _ShowPathGizmos = true;
    public static bool _ShowTuadTreeGizmos = true;
    public const bool _ShowColliderGizmos = false;
    #endregion
    /// <summary>
    /// true表示竖屏,false表示横屏
    /// </summary>
    public const bool Portrait = true;
    public static Vector2 StandardScreen = new Vector2(1920, 1080);


    public const float sceneCameraSize = 7f;

}
