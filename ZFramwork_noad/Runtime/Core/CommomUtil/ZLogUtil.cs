using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZLogUtil 
{
    public static void Log(object message)
    {
        if (ZDefine.ZLog&&ZDefine.ZLogNormal)
        {
            Debug.Log(message);
        }
    }
    public static void LogWarning(object message)
    {
        if (ZDefine.ZLog && ZDefine.ZLogWarning)
        {
            Debug.LogWarning(message);
        }
    }
    public static void LogError(object message)
    {
        if (ZDefine.ZLog && ZDefine.ZLogError)
        {
            Debug.LogError(message);
        }
    }
}
