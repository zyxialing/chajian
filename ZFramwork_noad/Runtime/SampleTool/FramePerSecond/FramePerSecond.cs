using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FramePerSecond : MonoBehaviour
{

    public static FramePerSecond instance;
    public Rect Rect = new Rect(5, 5, 100, 25);
    public bool Display = true;


    float LastTime;
    int FPS, Number;
    public int fps = 30;

    void Start()
    {
        instance = this;
        uIStyle = new GUIStyle();
        uIStyle.normal.textColor = Color.green;
        uIStyle.fontSize = 30;
    }


    void Update()
    {
        if (FPS < 30)
        {
            uIStyle.normal.textColor = Color.red;
        }
        else
        {
            uIStyle.normal.textColor = Color.green;
        }

            if (Time.time - LastTime > 1)
            {
                LastTime = Time.time;
                FPS = Number;
                Number = 0;
            }
            else
            {
                Number++;
            }
  
    }

    GUIStyle uIStyle;
    void OnGUI()
    {
        if (Display)
        {
            GUI.Label(Rect, "FPS:" + FPS.ToString(), uIStyle);
        }
    }

}