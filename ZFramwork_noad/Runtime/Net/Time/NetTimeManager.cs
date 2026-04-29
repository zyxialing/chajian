using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class NetTimeManager:Singleton<NetTimeManager>
{
    private NetTimeManager()
    {

    }

    #region 获取时间的地址 +++++++ 大厂网址
    private const string url1 = "https://www.baidu.com";
    //private const string url2 = "https://blog.csdn.net";
    //private const string url3 = "https://www.jianshu.com";
    //private const string url4 = "https://github.com";
    //private const string url5 = "https://www.sohu.com";

    #endregion

    /// <summary>
    /// 初始化时间管理器
    /// </summary>
    /// <param name="mono"></param>
    public void InitNetTime(MonoBehaviour mono)
    {
        mono.StartCoroutine(GetNetTime(url1));
    }

    IEnumerator GetNetTime(string url)
    {
        while (true)
        {
            //Debug.Log("开始获取"+url +"的服务器时间（GMT DATE）");
            UnityWebRequest WebRequest = new UnityWebRequest(url);
            yield return WebRequest.SendWebRequest();

            //网页加载完成  并且下载过程中没有错误   string.IsNullOrEmpty 判断字符串是否是null 或者是" ",如果是返回true
            //WebRequest.error  下载过程中如果出现下载错误  会返回错误信息 如果下载没有完成那么将会阻塞到下载完成
            if (WebRequest.isDone && string.IsNullOrEmpty(WebRequest.error))
            {
                Dictionary<string, string> resHeaders = WebRequest.GetResponseHeaders();
                foreach (var item in resHeaders)
                {
                    ZLogUtil.LogError(item.Key + item.Value);
                }

                //北京时间
                //Debug.Log(FormattingGMT(value).ToString());
            }
            yield return new WaitForSeconds(1);
        }
    }

}

