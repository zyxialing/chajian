using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

//[CustomEditor(typeof(ExampleMain))]
public class ExampleMainEditor : Editor
{

    //ExampleMain _target;
    ///// <summary>
    ///// 填入：Assets/ZFramework/Examples/ExampleRes/Audio
    ///// 填入：Assets/ZFramework/Examples/ExampleRes/CommonAudio
    ///// 填入：Assets/ZFramework/Examples/ExampleRes/Prefab
    ///// 填入：Assets/ZFramework/Examples/ExampleRes/SpriteUI
    ///// </summary>
    //public List<string> _paths;
    //public List<string> _toPaths;
    //public string scenePath;
    //public string toScenePath;
    //private void OnEnable()
    //{
    //    _target = target as ExampleMain;
    //    _paths = new List<string>();
    //    _toPaths = new List<string>();

    //    scenePath = "Assets/ZFramework/Examples";
    //    toScenePath = "Assets/Game/AssetDynamic/Scene";

    //    _paths.Add("Assets/ZFramework/Examples/ExampleRes/Audio");
    //    _paths.Add("Assets/ZFramework/Examples/ExampleRes/CommonAudio");
    //    _paths.Add("Assets/ZFramework/Examples/ExampleRes/Prefab");
    //    _paths.Add("Assets/ZFramework/Examples/ExampleRes/SpriteUI");

    //    _toPaths.Add("Assets/Game/AssetDynamic/Audio");
    //    _toPaths.Add("Assets/Game/AssetDynamic/Audio/Common");
    //    _toPaths.Add("Assets/Game/AssetDynamic/Prefab/Game");
    //    _toPaths.Add("Assets/Game/AssetDynamic/Sprite/UI");
      
    //}

    //public override void OnInspectorGUI()
    //{
    //    base.OnInspectorGUI();
    //    EditorGUILayout.Space();
    //    if (GUILayout.Button("复制资源到动态assets"))
    //    {
    //        DirectoryInfo info = new DirectoryInfo(scenePath);
    //        FileInfo[] files = info.GetFiles("*.unity*",SearchOption.AllDirectories);
    //        for (int i = 0; i < files.Length; i++)
    //        {
    //            if (!files[i].FullName.EndsWith(".meta"))
    //            {
    //                string targetPath = toScenePath + "/" + files[i].Name;
    //                Debug.Log(files[i].FullName);
    //                files[i].CopyTo(targetPath, true);

    //            }

    //        }

    //        for (int i = 0; i < _paths.Count; i++)
    //        {
    //            DirectoryInfo tempInfo = new DirectoryInfo(_paths[i]);
    //            FileInfo[] tempFiles = tempInfo.GetFiles();
    //            for (int m = 0; m < tempFiles.Length; m++)
    //            {
    //                if (!tempFiles[m].FullName.EndsWith(".meta"))
    //                {

    //                    string targetPath = _toPaths[i] + "/" + tempFiles[m].Name;
    //                    Debug.Log(tempFiles[m].FullName);
    //                    tempFiles[m].CopyTo(targetPath, true);
    //                }

    //            }
    //        }
    //        AssetDatabase.Refresh();
    //    }
    //    //EditorGUILayout.LabelField("-------------------------------------------------");
    //    //EditorGUILayout.Space();
    //    //if (GUILayout.Button("TestB"))
    //    //{
           
    //    //}
    //    EditorGUILayout.Space();

    //}
}