using UnityEngine;
using UnityEditor;
using System;

namespace ZFramework
{
    public class ZExporter
    {
        /// <summary>
        /// 以递归的方式导出unitypackage
        /// </summary>
        /// <param name="filePath">需要导出的文件或者文件夹例如："Assets/ZFramework"</param>
        /// <param name="fialName">导出的路径，必须以.unitypackage结尾</param>
        private static void ZFExportPackage(string filePath, string fialName)
        {
            AssetDatabase.ExportPackage(filePath, fialName, ExportPackageOptions.Recurse);
        }



        [MenuItem("ZFramework/Editor/1.生成unitypackage/仅仅生成unitypackage")]
        private static void ZFrameworkClick()
        {
            string exportFilePath = "Assets/ZFramework";
            string fileName = "ZFramework_" + ZCommomUtil.ZGetDataTimeString("yyyy_MM_dd_hh_mm") + ".unitypackage";
            Debug.Log("-------------------------------------------------------------------------------");
            Debug.Log("需要打包的文件夹：" + exportFilePath);
            Debug.Log("打包的名称：" + fileName);
            try
            {
                ZFExportPackage(exportFilePath, fileName);
                Debug.Log("打包成功！(σ°∀°)σ..:*☆哎哟不错哦");
                Debug.Log("-------------------------------------------------------------------------------");
            }
            catch (Exception ex)
            {
                Debug.LogError("打包错误：" + ex.Message);
                Debug.LogError("(●°u°●)​ 此刻我的内心是崩溃的");
                Debug.Log("-------------------------------------------------------------------------------");
            }
        }
        [MenuItem("ZFramework/Editor/1.生成unitypackage/生成unitypackage且打开文件夹")]
        private static void ZFrameworkClick1()
        {
            string exportFilePath = "Assets/ZFramework";
            string fileName = "ZFramework_" + ZCommomUtil.ZGetDataTimeString("yyyy_MM_dd_hh_mm") + ".unitypackage";
            Debug.Log("-------------------------------------------------------------------------------");
            Debug.Log("需要打包的文件夹：" + exportFilePath);
            Debug.Log("打包的名称：" + fileName);
            try
            {
                ZFExportPackage(exportFilePath, fileName);
                Debug.Log("打包成功！(σ°∀°)σ..:*☆哎哟不错哦");
                Debug.Log("-------------------------------------------------------------------------------");
                ZCommomUtil.OpenURL("");
            }
            catch (Exception ex)
            {
                Debug.LogError("打包错误：" + ex.Message);
                Debug.LogError("(●°u°●)​ 此刻我的内心是崩溃的");
                Debug.Log("-------------------------------------------------------------------------------");
            }
        }
    } 
}
