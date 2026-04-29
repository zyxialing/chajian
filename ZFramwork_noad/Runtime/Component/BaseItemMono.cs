using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseItemMono : MonoBehaviour
{
    private void Awake()
    {
        InitNode();
    }

    public virtual void InitNode()
    {

    }

    public virtual void customInitNode()
    {

    }

    public virtual void show(params object[] args)
    {

    }
}
