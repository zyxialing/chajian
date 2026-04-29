using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// CSharp对象缓存
/// </summary>
/// <typeparam name="T"></typeparam>
public class TObjectPool<T> where T : IClear, new()
{
    private static Stack<T> _freeStack;

    //private static void OnCleanMemory()
    //{
    //    if (_freeStack == null) return;

    //    foreach (var item in _freeStack)
    //        (item as IDisposable)?.Dispose();
    //    _freeStack.Clear();
    //    _freeStack = null;
    //}

    public static T AutoCreate()
    {
        if (_freeStack != null && _freeStack.Count > 0)
        {
            return _freeStack.Pop();
        }
        return Activator.CreateInstance<T>();
    }

    public static void Recycle(T tObj)
    {
        if (tObj != null)
        {
            tObj.Clear();

            if (_freeStack == null) _freeStack = new Stack<T>(1);
            if (CheckObjectIsInStack(tObj))
            {
                _freeStack.Push(tObj);
            }
        }
    }

    private static bool CheckObjectIsInStack(T tObj)
    {
        foreach (var item in _freeStack)
        {
            if(object.ReferenceEquals(item, tObj))
            {
                Debug.LogError("【TObjectPool】发现重复回收啦！！业务需要注意一下！对象类型：" + tObj.GetType());
                return false;
            }
        }
        return true;
    }
}