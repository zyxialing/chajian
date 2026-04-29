using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace ZFramework {

    public class ZStringUtil
    {
       public static List<string> ArrayStringToList(string[] strs)
        {
            List<string> tempList = new List<string>();
            if (strs == null)
            {
                return tempList;
            }
            for (int i = 0; i < strs.Length; i++)
            {
                tempList.Add(strs[i]);
            }
            return tempList;
        }
        public static List<int> ArrayStringToIntList(string[] strs)
        {
            List<int> tempList = new List<int>();
            if (strs == null)
            {
                return tempList;
            }
            for (int i = 0; i < strs.Length; i++)
            {
                tempList.Add(int.Parse(strs[i]));
            }
            return tempList;
        }
        public static List<float> ArrayStringToFloatList(string[] strs)
        {
            List<float> tempList = new List<float>();
            if (strs == null)
            {
                return tempList;
            }
            for (int i = 0; i < strs.Length; i++)
            {
                tempList.Add(float.Parse(strs[i]));
            }
            return tempList;
        }
        public static List<long> ArrayStringToLongList(string[] strs)
        {
            List<long> tempList = new List<long>();
            if (strs == null)
            {
                return tempList;
            }
            for (int i = 0; i < strs.Length; i++)
            {
                tempList.Add(long.Parse(strs[i]));
            }
            return tempList;
        }

        public static SerV2 StringToSerV2(string str)
        {
            str = str.Replace('\'', '\"');
            return JsonUtility.FromJson<SerV2>(str);
        }
        public static SerV3 StringToSerV3(string str)
        {
            str = str.Replace('\'', '\"');
            return JsonUtility.FromJson<SerV3>(str);
        }
        public static SerV4 StringToSerV4(string str)
        {
            str = str.Replace('\'', '\"');
            return JsonUtility.FromJson<SerV4>(str);
        }
    }
}