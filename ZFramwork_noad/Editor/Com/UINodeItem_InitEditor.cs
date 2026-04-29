using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(UINodeItem_Init))]
public class UINodeItem_InitEditor : Editor
{
    UINodeItem_Init _target;

    ReorderableList nodeList;
    SerializedProperty basePanelProperty;
    SerializedProperty basePanelVOProperty;
    void OnEnable()
    {
        _target = target as UINodeItem_Init;

        SerializedProperty nodesProperty = serializedObject.FindProperty("nodes");
        basePanelProperty = serializedObject.FindProperty("basePanel");
        basePanelVOProperty = serializedObject.FindProperty("basePanelVO");
        nodeList = new ReorderableList(_target.nodes, typeof(string));

        nodeList.drawElementCallback = (Rect rect, int index, bool selected, bool focused) =>
        {
            nodeList.elementHeight = 85;

            rect.x += 5;
            rect.y += 5;
            rect.width -= 5;
            rect.height = 75;

            GUI.Box(rect, "", EditorStyles.helpBox);

            rect.x += 5;
            rect.y += 5;
            rect.width -= 10;
            rect.height = 18;

            UINodeInfo node = _target.nodes[index];

            string tag = EditorGUI.TextField(rect, "Node Tag", node.tag).Trim();
            if (tag != node.tag)
            {
                node.tag = tag;
                EditorUtility.SetDirty(_target);
            }

            rect.y += 22;
            Transform transform = (Transform)EditorGUI.ObjectField(rect, "Transform", node.transform, typeof(Transform), true);
            if (transform != node.transform)
            {
                node.transform = transform;
                string tagName = node.tag;
                if ((string.IsNullOrEmpty(tagName) || tagName.Equals("Node Name")) && transform != null)
                {
                    tagName = transform.gameObject.name;
                    if (char.IsUpper(tagName[0]))
                    {
                        if (tagName.Length > 1)
                            tagName = char.ToLower(tagName[0]) + tagName.Substring(1);
                        else
                            tagName = char.ToLower(tagName[0]).ToString();
                    }
                    node.tag = tagName;
                }
                EditorUtility.SetDirty(_target);
            }

            rect.y += 22;
            if (node.transform)
            {
                Component[] cpns = node.transform.GetComponents<Component>();

                List<string> names = new List<string>();

                foreach (var cpn in cpns)
                {
                    var name = cpn.GetType().FullName;
                    if (name != "UnityEngine.CanvasRenderer")
                    {
                        names.Add(name);
                    }
                }
                names.Add("UnityEngine.GameObject");

                int select = names.IndexOf(node.type);
                int newselect = EditorGUI.Popup(rect, "Type", select, names.ToArray());

                if (newselect == -1)
                {
                    newselect = 0;
                }

                if (newselect != select)
                {
                    node.type = names[newselect];
                    EditorUtility.SetDirty(_target);
                }
            }
            else
            {
                EditorGUI.LabelField(rect, "从Prefab内部拖入一个UI节点");
            }
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
        if (Application.isPlaying)
        {
            return;
        }
        scrollView = GUILayout.BeginScrollView(scrollView);
        string name = "";
        string path = PathFinderEditor.GetPrefabAssetPath(Selection.activeGameObject, out name);
        if (GUILayout.Button("更新脚本"))
        {
            ScriptCreater.CreateItem_InitClassName(path,name,Selection.activeGameObject.GetComponent<UINodeItem_Init>());
        }
        string nameVO = name + "VO";

        if (_target.GetComponent(Selection.activeGameObject.name) == null)
        {
            Type className = System.Reflection.Assembly.Load("Assembly-CSharp").GetType(Selection.activeGameObject.name);
            if (className != null)
            {
                Selection.activeGameObject.AddComponent(className);
            }
        }
        //FindBack();

        EditorGUILayout.Space();
        if (_target.basePanel == null || _target.basePanel.name != name)
        {
            string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UI")+"/";
            dir = dir +"others/" + name + ".cs";
            _target.basePanel= AssetDatabase.LoadAssetAtPath<TextAsset>(dir);
        }
        path = PathFinderEditor.GetPrefabAssetPath(Selection.activeGameObject, out name);
        if (_target.basePanelVO == null || _target.basePanelVO.name != nameVO)
        {
            string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UIVO") + "/";
            dir = dir + "others/" + name + "VO.cs";
            ZLogUtil.LogError(dir);
            _target.basePanelVO = AssetDatabase.LoadAssetAtPath<TextAsset>(dir);
        }

        serializedObject.Update();
        nodeList.DoLayoutList();
        EditorGUILayout.PropertyField(basePanelProperty);
        EditorGUILayout.PropertyField(basePanelVOProperty);
        serializedObject.ApplyModifiedProperties();
        GUILayout.EndScrollView();
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