using UnityEngine;
using System;

[Serializable]
public struct SerV2
{
    public float x;
    public float y;
    public SerV2(float x=0,float y=0)
    {
        this.x = x;
        this.y = y;
    }
    public Vector3 ToVector2()
    {
        return new Vector2(x, y);
    }

    public override string ToString()
    {
        return string.Format("{\"x\":{0},\"y\":{1}}", x, y);
    }
}
