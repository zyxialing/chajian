using System;
using UnityEngine;
namespace ZFramework
{

    public partial class ZCommomUtil
    {
        /// <summary>
        /// 打开文件夹或者网站
        /// </summary>
        /// <param name="path"></param>
        public static void OpenURL(string path)
        {
            Application.OpenURL(path);
        }

        /// <summary>
        /// 以格式返回时间的字符串
        /// </summary>
        /// <param name="format">默认值yyyy_MM_dd_hh_mm_ss</param>
        /// <returns></returns>
        public static string ZGetDataTimeString(string format = "yyyy_MM_dd_hh_mm_ss")
        {
            return DateTime.Now.ToString(format);
        }

        public static Vector3 MeragePosAndHeight(Transform transform, float role_height)
        {
            return new Vector3(transform.position.x,transform.position.y,role_height);
        }

    }
}