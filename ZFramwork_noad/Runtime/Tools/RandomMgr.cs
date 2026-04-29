using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomMgr:Singleton<RandomMgr>
{
    private RandomMgr()
    {

    }
    public void Init()
    {
        Random.InitState(DateTime.Now.Millisecond);
    }
    public void Init(int id)
    {
        Random.InitState(id);
    }
    public static int Range(int minInclusive, int maxExclusive)
    {
        return Random.Range(minInclusive, maxExclusive);
    }
    public static float Range(float minInclusive, float maxInclusive)
    {
        return Random.Range(minInclusive, maxInclusive);
    }
    public static float GetValue()
    {
        return Random.value;
    }

    public static float GetRandomCD(float cd)
    {
        return Random.Range(0.8f,1.2f)*cd;
    }
}
