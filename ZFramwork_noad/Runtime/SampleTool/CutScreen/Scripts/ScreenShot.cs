using UnityEngine;
using System.Collections;
using System.IO;
using Unity.Collections;

public class ScreenShot : MonoBehaviour
{

    public string fileName = "";
    [ReadOnly]
    public string outPutPath = "";
    public bool START = false;

    private void Start()
    {
        outPutPath = Application.dataPath.Replace("Assets", "截图");
        if (!Directory.Exists(outPutPath))
        {
            Directory.CreateDirectory(outPutPath);
        }
        outPutPath += "/";
    }

    public void Update()
    {
        if (START)
        {
            string path = outPutPath + fileName + "_" + ZFramework.ZCommomUtil.ZGetDataTimeString() + ".jpg";
            Debug.Log(path);
            ScreenCapture.CaptureScreenshot(path);
            START = false;
        }
    }


}