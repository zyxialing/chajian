using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Config<T> where T :IConfig
{
    private static Dictionary<string, List<T>> excelDatas = new Dictionary<string, List<T>>();
    public static List<T> InitConfig()
    {
        T t = Activator.CreateInstance<T>();
        return t.LoadBytes<T>();
    }

    public static void AddExcelToDic(string name,List<T> excelData)
    {
        if (!excelDatas.ContainsKey(name))
        {
            excelDatas.Add(name, excelData);
        }
    }

    public static List<T> GetExcel()
    {
        string name = typeof(T).Name;
        if (excelDatas.ContainsKey(name))
        {
            return excelDatas[name];
        }
        return null;
    }
}
