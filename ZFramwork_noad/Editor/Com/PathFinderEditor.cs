using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFinderEditor
{
    /// <summary>
    /// 获取预制体资源路径。
    /// </summary>
    /// <param name="gameObject"></param>
    /// <returns></returns>
    public static string GetPrefabAssetPath(GameObject gameObject,out string name)
    {
#if UNITY_EDITOR
        // Project中的Prefab是Asset不是Instance
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            // 预制体资源就是自身
            name = gameObject.name;
            return UnityEditor.AssetDatabase.GetAssetPath(gameObject);
        }

        // Scene中的Prefab Instance是Instance不是Asset
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject))
        {
            // 获取预制体资源
            var prefabAsset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            
            name = prefabAsset.name;
            return UnityEditor.AssetDatabase.GetAssetPath(prefabAsset);
        }

        // PrefabMode中的GameObject既不是Instance也不是Asset
        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
        if (prefabStage != null)
        {
            // 预制体资源：prefabAsset = prefabStage.prefabContentsRoot
            name = gameObject.name;
            return prefabStage.assetPath;
        }
#endif

        // 不是预制体
        name = "";
        return null;
    }
}
