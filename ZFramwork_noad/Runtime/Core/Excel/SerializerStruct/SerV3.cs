using UnityEngine;
using System;

[Serializable]
public struct SerV3 
{
    public float x;
    public float y;
    public float z;


    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
