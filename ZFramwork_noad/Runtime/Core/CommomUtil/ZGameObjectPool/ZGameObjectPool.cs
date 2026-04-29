using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate GameObject PoolAction();

public class ZGameObjectPool
{
    static Dictionary<string, Stack<GameObject>> pools = new Dictionary<string, Stack<GameObject>>();

    public static void Init(string name, PoolAction callBack)
    {
        if (pools.ContainsKey(name))
        {
            Stack<GameObject> pool = pools[name];
            pool.Push(callBack.Invoke());
        }
        else
        {
            Stack<GameObject> pool = new Stack<GameObject>();
            pools.Add(name, pool);
            pool.Push(callBack.Invoke());
        }
    }

    public static GameObject Pop(string name,PoolAction callBack)
    {
        if (pools.ContainsKey(name))
        {
            Stack<GameObject> pool = pools[name];
            if (pool.Count > 0)
            {
                return pool.Pop();
            }
            else
            {
                return callBack.Invoke();
            }
        }
        else
        {
            Stack<GameObject> pool = new Stack<GameObject>();
            pools.Add(name, pool);
            return callBack.Invoke();
        }
    }
    public static void Push(string name, GameObject obj,bool hide = true)
    {
        if (obj == null)
        {
            return;
        }
        if (hide)
        {
            obj.SetActive(false);
        }
        if (pools.ContainsKey(name))
        {
            Stack<GameObject> pool = pools[name];
            pool.Push(obj);
        }
        else
        {
            Stack<GameObject> pool = new Stack<GameObject>();
            pool.Push(obj);
            pools.Add(name, pool);
        }
    }

    public static int GetCount(string name)
    {
        if (pools.ContainsKey(name))
        {
            return pools[name].Count;
        }
        else
        {
            return 0;
        }
    }
    public static void ClearPool(string name)
    {
        if (pools.ContainsKey(name))
        {
            pools.Remove(name);
        }
    }
    public static void ClearPools()
    {
        pools.Clear();
    }
}
