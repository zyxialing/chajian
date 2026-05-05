using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// GpuPaperDoll 公共工具方法
/// 提取各模块中重复的路径、矩阵等工具方法
/// </summary>
public static class GpuRoleUtility
{
    public const int InternalOrderStep = 100; // 同一个 sortingOrder 下最多支持 100 个部件

    /// <summary>
    /// 从根节点到目标节点的路径（/ 分隔）
    /// </summary>
    public static string GetPath(Transform root, Transform target)
    {
        if (target == root) return root.name;
        var names = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root) break;
            current = current.parent;
        }
        return string.Join("/", names.ToArray());
    }

    /// <summary>
    /// 构建 slotKey（点号分隔，去掉根节点名）
    /// </summary>
    public static string BuildSlotKey(Transform root, Transform target)
    {
        return GetPath(root, target)
            .Replace(root.name + "/", "")
            .Replace("/", ".")
            .Replace(" ", "")
            .Trim('.');
    }

    /// <summary>
    /// 构建 slotName
    /// </summary>
    public static string BuildSlotName(Transform transform, SpriteRenderer renderer)
    {
        string name = transform.name.Trim();
        if (string.IsNullOrEmpty(name))
            name = renderer.sprite != null ? renderer.sprite.name : "Slot";
        return name;
    }

    /// <summary>
    /// 获取节点深度（相对于根节点）
    /// </summary>
    public static int GetDepth(Transform root, Transform target)
    {
        int depth = 0;
        Transform current = target;
        while (current != null && current != root)
        {
            depth++;
            current = current.parent;
        }
        return depth;
    }

    /// <summary>
    /// 判断 Transform 在层级中是否激活
    /// </summary>
    public static bool IsActiveInHierarchy(Transform t)
    {
        Transform current = t;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                return false;
            current = current.parent;
        }
        return true;
    }

    /// <summary>
    /// 按 / 分隔的路径查找 Transform
    /// </summary>
    public static Transform FindTransformByPath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string[] parts = path.Split('/');
        Transform current = root;
        for (int i = 1; i < parts.Length; i++)
        {
            if (current == null) return null;
            current = current.Find(parts[i]);
        }
        return current;
    }

    /// <summary>
    /// 从矩阵分解位置、旋转、缩放
    /// </summary>
    public static void DecomposeMatrix(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = matrix.GetColumn(3);

        Vector3 col0 = matrix.GetColumn(0);
        Vector3 col1 = matrix.GetColumn(1);
        Vector3 col2 = matrix.GetColumn(2);

        float scaleX = col0.magnitude;
        float scaleY = col1.magnitude;
        float scaleZ = col2.magnitude;

        float det = matrix.determinant;
        if (det < 0f)
        {
            scaleX = -scaleX;
            col0 = -col0;
        }

        scale = new Vector3(scaleX, scaleY, scaleZ);

        Vector3 right = col0 / Mathf.Max(Mathf.Abs(scaleX), 0.0001f);
        Vector3 up = col1 / Mathf.Max(Mathf.Abs(scaleY), 0.0001f);
        Vector3 fwd = col2 / Mathf.Max(Mathf.Abs(scaleZ), 0.0001f);

        rotation = Quaternion.LookRotation(fwd, up);
    }

    /// <summary>
    /// 按图集路径 + sprite 名称加载 Sprite
    /// </summary>
    public static Sprite LoadSpriteByPathAndName(string path, string spriteName)
    {
        if (string.IsNullOrEmpty(path)) return null;

#if UNITY_EDITOR
        var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        if (sprites.Length == 0) return null;

        if (!string.IsNullOrEmpty(spriteName))
        {
            var matched = Array.Find(sprites, s =>
                string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase));
            if (matched != null) return matched;
        }
        return sprites[0];
#else
        return null;
#endif
    }

    /// <summary>
    /// 从文件夹随机选一个 Sprite
    /// </summary>
    public static Sprite PickRandomSpriteFromFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return null;

#if UNITY_EDITOR
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath)) return null;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        if (guids.Length == 0) return null;
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[UnityEngine.Random.Range(0, guids.Length)]);
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }

    /// <summary>
    /// 获取相对 Assets 的路径
    /// </summary>
    public static string GetRelativePath(string fullPath)
    {
        string dp = UnityEngine.Application.dataPath;
        return fullPath.StartsWith(dp) ? "Assets" + fullPath.Substring(dp.Length) : null;
    }
}
