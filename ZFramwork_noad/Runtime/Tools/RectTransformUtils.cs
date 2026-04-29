using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RectTransformUtils
{
    public static void SetStretch(GameObject obj)
    {
        var rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = Vector2.one * 0.5f;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    public static void SetStretchBottomLeft(GameObject obj)
    {
        var rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.one*0.5f;
        rectTransform.anchorMax = Vector2.one * 0.5f;
        rectTransform.pivot = Vector2.zero;
        rectTransform.localPosition = Vector2.zero;
    }

    public static void SetStretchRight(GameObject obj,float width = 20)
    {
        var rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.right;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 20);

    }

    public static void SetStretchButtom(GameObject obj, float height = 20)
    {
        var rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.right;
        rectTransform.pivot = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20);

    }
}
