using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeUtils
{
    static float _startTime = float.MinValue;

    public static void SetStartTime(float time)
    {
        if(_startTime == float.MinValue)
        {
            _startTime = time;
        }
    }
}
