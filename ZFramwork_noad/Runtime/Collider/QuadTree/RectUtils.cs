using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RectUtils
{

    public static bool IsContain(Rect rect1,Rect rect2)
    {
        return rect1.Overlaps(rect2);
    }
    public static bool IsFullContain(Rect bigBox,Rect littleBox)
    {
        Vector2 pos = littleBox.position;
        Vector2 topRightPos = littleBox.position + littleBox.size;
        bool isContain = bigBox.Contains(pos) && bigBox.Contains(topRightPos);
        return isContain;
    }
}
