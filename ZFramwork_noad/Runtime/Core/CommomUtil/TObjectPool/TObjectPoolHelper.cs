using System;
using System.Collections;
using System.Collections.Generic;
public class TObjectPoolHelper
{
    /// <summary>
    /// 修剪TObjectPool对象数组，
    /// </summary>
    /// <param name="list"></param>
    /// <param name="count"></param>
    /// <typeparam name="T"></typeparam>
    public static void TrimList<T>(List<T> list, int count = 0) where T : TObjectPool<T>, IClear, new()
    {
        if (list == null) return;

        count = Math.Max(0, count);
        if (list.Count > count)
        {
            for (int i = list.Count - 1; i >= count; i--)
            {
                TObjectPool<T>.Recycle(list[i]);
                list.RemoveAt(i);
            }
        }
    }

    /// <summary>
    ///  清理可回收对象
    /// </summary>
    /// <param name="target"></param>
    /// <typeparam name="T"></typeparam>
    public static void ClearObject<T>(T target) where T : TObjectPool<T>, IClear, new()
    {
        if (target == null) return;
        TObjectPool<T>.Recycle(target);
    }

    /// <summary>
    /// 清理可回收对象数组
    /// </summary>
    /// <param name="list"></param>
    /// <typeparam name="T"></typeparam>
    public static void ClearList<T>(List<T> list) where T : TObjectPool<T>, IClear, new()
    {
        if (list == null) return;
        TrimList(list, 0);
        list.Clear();
    }
}