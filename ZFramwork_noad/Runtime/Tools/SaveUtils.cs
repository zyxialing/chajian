using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveUtils
{

    public static int GetIntForKey(string key, int defValue = 0)
    {
       return Storage.Instance.GetIntForKey(key, defValue);
    }
    public static void SetIntForKey(string key, int vaule = 0)
    {
        Storage.Instance.SetIntForKey(key, vaule);
    }
    public static string GetStringForKey(string key, string defValue = "")
    {
        return Storage.Instance.GetStringForKey(key, defValue);
    }
    public static void SetStringForKey(string key, string vaule = "")
    {
        Storage.Instance.SetStringForKey(key, vaule);
    }
    public static float GetFloatForKey(string key, float defVaule = 0f)
    {
        return Storage.Instance.GetFloatForKey(key, defVaule);
    }
    public static void SetFloatForKey(string key, float vaule = 0)
    {
        Storage.Instance.SetFloatForKey(key, vaule);
    }
    public static bool GetBoolForKey(string key, bool defVaule = false)
    {
        return Storage.Instance.GetBoolForKey(key, defVaule);
    }
    public static void SetBoolForKey(string key, bool vaule = false)
    {
        Storage.Instance.SetBoolForKey(key, vaule);
    }
}
