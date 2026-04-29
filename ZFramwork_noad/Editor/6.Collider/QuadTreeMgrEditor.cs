using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(QuadTreeMgr))]
public class QuadTreeMgrEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        QuadTreeMgr mgr = (QuadTreeMgr)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Toggle Gizmos"))
        {
            ZDefine._ShowTuadTreeGizmos = !ZDefine._ShowTuadTreeGizmos;
        }

        if (GUILayout.Button("Print Count"))
        {
            Debug.Log(mgr.GetCount());
        }
    }
}