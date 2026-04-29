using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BezierUtils
{
    // 二阶贝塞尔曲线，参数对应上方原理内的二阶曲线参数.
    public static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        Vector2 p0p1 = (1 - t) * p0 + t * p1;
        Vector2 p1p2 = (1 - t) * p1 + t * p2;
        Vector2 temp = (1 - t) * p0p1 + t * p1p2;
        return temp;
    }

    // 三阶贝塞尔曲线，参数对应上方原理内的三阶曲线参数.
    public static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        Vector2 temp;
        Vector2 p0p1 = (1 - t) * p0 + t * p1;
        Vector2 p1p2 = (1 - t) * p1 + t * p2;
        Vector2 p2p3 = (1 - t) * p2 + t * p3;
        Vector2 p0p1p2 = (1 - t) * p0p1 + t * p1p2;
        Vector2 p1p2p3 = (1 - t) * p1p2 + t * p2p3;
        temp = (1 - t) * p0p1p2 + t * p1p2p3;
        return temp;
    }

    // 多阶贝塞尔曲线，使用递归实现.
    public static Vector2 Bezier(float t, List<Vector2> p)
    {
        if (p.Count < 2)
            return p[0];
        List<Vector2> newp = new List<Vector2>();
        for (int i = 0; i < p.Count - 1; i++)
        {
            Debug.DrawLine(p[i], p[i + 1]);

            Vector2 p0p1 = (1 - t) * p[i] + t * p[i + 1];
            newp.Add(p0p1);
        }
        return Bezier(t, newp);
    }
}
