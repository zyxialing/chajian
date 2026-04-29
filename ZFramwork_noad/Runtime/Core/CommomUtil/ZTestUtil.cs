using System;
using System.IO;
using UnityEngine;
namespace ZFramework {

public class ZTestUtil
    {
        /// <summary>
        /// 打开文件夹或者网站
        /// </summary>
        /// <param name="path"></param>
       public static void Test_WriteStringToTxt(string fileName,string content)
        {
            File.WriteAllText(Application.dataPath+"/" + fileName + ".txt", content);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        /// <summary>
        /// object转出json文件测试
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="isWriteToDataPath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string Test_ObjectToJson(object obj,bool isWriteToDataPath,string fileName)
        {
           string str = JsonUtility.ToJson(obj);
            if (isWriteToDataPath)
            {
                Test_WriteStringToTxt(fileName, str);
            }
            return str;
        }
       


    }
}