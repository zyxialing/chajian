using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LayerUtils
{
   public static void SetUILayer(GameObject obj)
    {
        obj.layer = 5;
    }
    public static void SetUILayer(Transform transform)
    {
        transform.gameObject.layer = 5;
    }
}
