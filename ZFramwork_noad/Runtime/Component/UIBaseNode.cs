using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class UIBaseNode : MonoBehaviour
{
    public List<UINodeInfo> nodes = new List<UINodeInfo>();

    public UINodeInfo[] getNodes()
    {
        return nodes.ToArray();
    }
}

[System.Serializable]
public class UINodeInfo
{
    public Transform transform;
    public string tag = "Node Name";
    public string type = string.Empty;
    public bool useMultiTypes = false;
    public bool foldout = false;
    public List<string> types = new List<string>();
}
