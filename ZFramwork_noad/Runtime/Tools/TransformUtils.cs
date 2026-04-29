using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformUtils
{
    public static void TransformWorldNormalize(GameObject obj)
    {
        Transform transform = obj.transform;
        transform.position = Vector3.zero;
        transform.eulerAngles = Vector3.zero;
        transform.localScale = Vector3.one;
    }

    public static void TransformLocalNormalize(GameObject obj, GameObject parentObj)
    {
        Transform transform = obj.transform;
        Transform parent = parentObj.transform;
        transform.SetParent(parent);
        transform.localPosition = Vector3.zero;
        transform.localEulerAngles = Vector3.zero;
        transform.localScale = Vector3.one;
    }

    public static void TransformLocalNormalize(GameObject obj, Transform parent)
    {
        Transform transform = obj.transform;
        transform.SetParent(parent);
        transform.localPosition = Vector3.zero;
        transform.localEulerAngles = Vector3.zero;
        transform.localScale = Vector3.one;
    }

}
