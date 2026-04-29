using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(UINodePanel))]
public class UINodePanelEditor : Editor
{
    UINodePanel _target;

    ReorderableList nodeList;
    SerializedProperty basePanelProperty;
    SerializedProperty basePanelVOProperty;
    void OnEnable()
    {
        _target = target as UINodePanel;

        SerializedProperty nodesProperty = serializedObject.FindProperty("nodes");
        basePanelProperty = serializedObject.FindProperty("basePanel");
        basePanelVOProperty = serializedObject.FindProperty("basePanelVO");
        nodeList = new ReorderableList(_target.nodes, typeof(UINodeInfo));
        nodeList.elementHeightCallback = index => UINodeInfoEditorDrawer.GetElementHeight(_target.nodes[index], _target);
        nodeList.drawElementCallback = (Rect rect, int index, bool selected, bool focused) =>
        {
            UINodeInfoEditorDrawer.DrawElement(rect, _target.nodes[index], _target, _target);
        };
        nodeList.drawHeaderCallback = (Rect rect) =>
        {
            GUI.Label(rect, "UI Node List");
        };
        nodeList.onRemoveCallback = (ReorderableList list) =>
        {
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            EditorUtility.SetDirty(_target);
        };
        nodeList.onAddCallback = (ReorderableList list) =>
        {
            UINodeInfo newNode = new UINodeInfo();
            _target.nodes.Add(newNode);
            EditorUtility.SetDirty(_target);
        };
    }

    private Vector2 scrollView;
    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying)
        {
            scrollView = GUILayout.BeginScrollView(scrollView);
            string name = "";
            string path = PathFinderEditor.GetPrefabAssetPath(Selection.activeGameObject, out name);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("更新脚本"))
            {
                ScriptCreater.CreatePanelClassName(path, name, Selection.activeGameObject.GetComponent<UINodePanel>());
            }

            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("打开UIPanel窗口"))
            {
                var window = UnityEditor.EditorWindow.GetWindow(typeof(UIBaseNodeWindow), true);

                window.BeginWindows();
            }
            string nameVO = name + "VO";
            //FindBack();

            EditorGUILayout.Space();
            if (_target.basePanel == null || _target.basePanel.name != name)
            {
                string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UI") + "/";
                dir = dir + name + ".cs";
                _target.basePanel = AssetDatabase.LoadAssetAtPath<TextAsset>(dir);
            }

            path = PathFinderEditor.GetPrefabAssetPath(Selection.activeGameObject, out name);
            if (_target.basePanelVO == null || _target.basePanelVO.name != nameVO)
            {
                string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UIVO/") + "/";
                dir = dir + name + "VO.cs";
                _target.basePanelVO = AssetDatabase.LoadAssetAtPath<TextAsset>(dir);
            }


            serializedObject.Update();
            nodeList.DoLayoutList();
            DrawScriptAssetField(basePanelProperty, "Base Panel");
            DrawScriptAssetField(basePanelVOProperty, "Base Panel VO");
            serializedObject.ApplyModifiedProperties();
            GUILayout.EndScrollView();
        }
        
    }

    private void DrawScriptAssetField(SerializedProperty property, string label)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(property, new GUIContent(label));
        bool enabled = property.objectReferenceValue != null;
        EditorGUI.BeginDisabledGroup(!enabled);
        if (GUILayout.Button("打开", GUILayout.Width(48f)))
        {
            AssetDatabase.OpenAsset(property.objectReferenceValue);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    public string strIpt = "";

    /// <summary>
    /// nodes节点名字 锁定节点Obj
    /// </summary>
    public void FindBack()
    {
        try
        {
            strIpt = GUILayout.TextField(strIpt);
            if (!string.IsNullOrEmpty(strIpt))
            {
                foreach (var item in _target.nodes)
                {
                    if (item.tag == strIpt && item.transform != null)
                    {
                        Selection.activeGameObject = item.transform.gameObject;
                    }
                }
                strIpt = "";
            }
        }
        catch
        {
            Debug.Log("又来了,但不影响");
        }
       
    }
}
