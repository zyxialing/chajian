using UnityEngine;
using System;

[Serializable]
public struct SerV4
{
    public float x;
    public float y;
    public float z;
    public float w;


    public Vector3 ToVector4()
    {
        return new Vector4(x, y, z,w);
    }
}
