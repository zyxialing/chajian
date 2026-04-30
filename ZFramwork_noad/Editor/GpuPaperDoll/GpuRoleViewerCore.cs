using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GpuRoleStyleViewer 的核心数据与业务逻辑（不依赖 GUI）
/// 持久化存储避免数据丢失
/// </summary>
public class GpuRoleViewerCore : ScriptableObject
{
    // ===== 持久化数据 =====
    private GameObject _sourcePrefab;
    private List<GpuRoleSlot> _slotDefinitions = new List<GpuRoleSlot>();
    private List<GpuRoleStyleSlot> _styleSlots = new List<GpuRoleStyleSlot>();
    private int _nextGroupId = 1;
    private List<GroupDataEntry> _groups = new List<GroupDataEntry>();

    // ===== 运行时临时数据（不参与持久化，但跟随 core 存续） =====
    [NonSerialized] private Dictionary<int, Sprite> _groupSprites = new Dictionary<int, Sprite>();

    // ===== Prefab 根节点变换（用于重建预览时定位） =====
    public Vector3 RootPosition { get; private set; }
    public Quaternion RootRotation { get; private set; }
    public Vector3 RootScale { get; private set; } = Vector3.one;

    // ===== 访问器 =====
    public GameObject SourcePrefab
    {
        get => _sourcePrefab;
        set => _sourcePrefab = value;
    }
    public List<GpuRoleSlot> SlotDefinitions => _slotDefinitions;
    public List<GpuRoleStyleSlot> StyleSlots => _styleSlots;
    public int NextGroupId
    {
        get => _nextGroupId;
        set => _nextGroupId = value;
    }
    public Dictionary<int, Sprite> GroupSprites => _groupSprites;

    public bool HasData => _sourcePrefab != null && _slotDefinitions.Count > 0;

    // ===== 组操作 =====
    public IReadOnlyList<GroupDataEntry> Groups => _groups;

    public void SetGroups(List<GroupDataEntry> groups)
    {
        _groups = groups;
    }

    public string GetGroupName(int groupId)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        return g != null ? g.groupName : $"Group {groupId}";
    }

    public void SetGroupName(int groupId, string name)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        if (g != null) g.groupName = name;
    }

    public int CreateGroup(string name = null)
    {
        int id = _nextGroupId++;
        _groups.Add(new GroupDataEntry { groupId = id, groupName = name ?? $"Group {id}" });
        _groupSprites[id] = null;
        return id;
    }

    public void RemoveGroup(int groupId)
    {
        _groups.RemoveAll(x => x.groupId == groupId);
        _groupSprites.Remove(groupId);
    }

    public bool GroupExists(int groupId) => _groups.Any(x => x.groupId == groupId);

    public string GetGroupSpritePath(int groupId)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        return g?.groupSpritePath ?? "";
    }

    public void SetGroupSpritePath(int groupId, string path)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        if (g != null) g.groupSpritePath = path;
    }

    // ===== 槽位操作 =====
    public GpuRoleStyleSlot GetSlot(int index)
    {
        return index >= 0 && index < _styleSlots.Count ? _styleSlots[index] : null;
    }

    public List<int> GetSlotIndicesInGroup(int groupId)
    {
        List<int> indices = new List<int>();
        for (int i = 0; i < _styleSlots.Count; i++)
        {
            if (_styleSlots[i].linkedGroupId == groupId)
                indices.Add(i);
        }
        return indices;
    }

    public List<string> GetSlotNamesInGroup(int groupId)
    {
        return GetSlotIndicesInGroup(groupId).Select(i => _styleSlots[i].slotName).ToList();
    }

    // ===== 功能方法 =====

    /// <summary>
    /// 从 Prefab 加载槽位定义并初始化
    /// </summary>
    public void LoadFromPrefab(GameObject prefab)
    {
        if (prefab == null) return;

        _sourcePrefab = prefab;
        _styleSlots.Clear();
        _slotDefinitions.Clear();
        _nextGroupId = 1;
        _groups.Clear();
        _groupSprites.Clear();

        // 记录 root 变换
        Transform rootTransform = prefab.transform;
        RootPosition = rootTransform.localPosition;
        RootRotation = rootTransform.localRotation;
        RootScale = rootTransform.localScale;

        // 扫描槽位
        _slotDefinitions = ScanPrefabSlots(prefab);

        // 创建临时实例获取默认 sprite
        GameObject tempInstance = Instantiate(prefab);
        SpriteRenderer[] tempRenderers = tempInstance.GetComponentsInChildren<SpriteRenderer>(true);
        Transform tempRoot = tempInstance.transform;
        Array.Sort(tempRenderers, (a, b) => string.CompareOrdinal(GetPath(tempRoot, a.transform), GetPath(tempRoot, b.transform)));

        for (int i = 0; i < _slotDefinitions.Count; i++)
        {
            Sprite defaultSprite = null;
            Color defaultColor = Color.white;
            if (i < tempRenderers.Length)
            {
                defaultSprite = tempRenderers[i].sprite;
                defaultColor = tempRenderers[i].color;
            }

            _styleSlots.Add(new GpuRoleStyleSlot
            {
                slotKey = _slotDefinitions[i].slotKey,
                slotName = _slotDefinitions[i].slotName,
                spriteFolder = "",
                sprite = defaultSprite,
                color = defaultColor,
                linkedGroupId = -1,
                linkedSubSpriteName = _slotDefinitions[i].slotName,
            });
        }

        DestroyImmediate(tempInstance);

        // 自动识别身体组
        AutoDetectBodyGroup();
    }

    private void AutoDetectBodyGroup()
    {
        string[] bodyPartNames = { "Body", "Arm_L", "Arm_R", "Foot_L", "Foot_R", "Head" };
        List<int> matched = new List<int>();

        for (int i = 0; i < _styleSlots.Count; i++)
        {
            if (bodyPartNames.Any(bp => string.Equals(_styleSlots[i].slotName.Trim(), bp, StringComparison.OrdinalIgnoreCase)))
                matched.Add(i);
        }

        if (matched.Count >= 2)
        {
            int gId = _nextGroupId++;
            _groups.Add(new GroupDataEntry { groupId = gId, groupName = "Body Group" });
            _groupSprites[gId] = null;

            foreach (int idx in matched)
            {
                _styleSlots[idx].linkedGroupId = gId;
                _styleSlots[idx].linkedSubSpriteName = _styleSlots[idx].slotName;
            }
        }
    }

    /// <summary>
    /// 将一张大图按子 sprite 名分配到组内各槽位
    /// </summary>
    public void ApplyGroupSpriteToSlots(int groupId, Sprite groupSprite)
    {
        TryApplyGroupSpriteToSlots(groupId, groupSprite, out _);
    }

    public bool TryApplyGroupSpriteToSlots(int groupId, Sprite groupSprite, out List<string> missingSubSprites)
    {
        missingSubSprites = new List<string>();
        if (groupSprite == null) return false;

        string spritePath = AssetDatabase.GetAssetPath(groupSprite);
        Sprite[] allSubSprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .ToArray();

        HashSet<string> requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _styleSlots.Count; i++)
        {
            if (_styleSlots[i].linkedGroupId != groupId) continue;

            string subName = _styleSlots[i].linkedSubSpriteName;
            if (!string.IsNullOrEmpty(subName))
            {
                requiredNames.Add(subName);
            }
        }

        foreach (string subName in requiredNames)
        {
            if (FindSubSpriteByName(allSubSprites, subName) == null)
            {
                missingSubSprites.Add(subName);
            }
        }

        if (missingSubSprites.Count > 0)
        {
            return false;
        }

        _groupSprites[groupId] = groupSprite;

        for (int i = 0; i < _styleSlots.Count; i++)
        {
            if (_styleSlots[i].linkedGroupId != groupId) continue;

            string subName = _styleSlots[i].linkedSubSpriteName;
            _styleSlots[i].sprite = string.IsNullOrEmpty(subName)
                ? groupSprite
                : FindSubSpriteByName(allSubSprites, subName);
            _styleSlots[i].color = Color.white;
        }

        return true;
    }

    /// <summary>
    /// 清空组内所有槽位的 sprite，表示该组部件不显示
    /// </summary>
    public void ClearGroupSprite(int groupId)
    {
        _groupSprites[groupId] = null;
        SetGroupSpritePath(groupId, "");

        for (int i = 0; i < _styleSlots.Count; i++)
        {
            if (_styleSlots[i].linkedGroupId != groupId) continue;
            _styleSlots[i].sprite = null;
            _styleSlots[i].color = Color.white;
        }
    }

    /// <summary>
    /// 从组目录或组内第一个有目录的槽位随机选大图
    /// </summary>
    public bool RandomizeLinkedGroup(int groupId)
    {
        string folderPath = "";

        // 优先用组目录
        var g = _groups.Find(x => x.groupId == groupId);
        if (g != null)
            folderPath = g.groupSpriteFolder;

        // fallback 到槽位目录
        if (string.IsNullOrEmpty(folderPath))
        {
            for (int i = 0; i < _styleSlots.Count; i++)
            {
                if (_styleSlots[i].linkedGroupId == groupId && !string.IsNullOrEmpty(_styleSlots[i].spriteFolder))
                {
                    folderPath = _styleSlots[i].spriteFolder;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return false;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        if (guids.Length == 0) return false;

        string path = AssetDatabase.GUIDToAssetPath(guids[UnityEngine.Random.Range(0, guids.Length)]);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) return false;

        if (!TryApplyGroupSpriteToSlots(groupId, sprite, out _))
        {
            return false;
        }

        SetGroupSpritePath(groupId, path);
        return true;
    }

    /// <summary>
    /// 随机独立槽位
    /// </summary>
    public Sprite PickRandomSpriteFromFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        if (guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[UnityEngine.Random.Range(0, guids.Length)]);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// 清空所有 Sprite
    /// </summary>
    public void ClearAllSprites()
    {
        foreach (var slot in _styleSlots)
        {
            slot.sprite = null;
            slot.color = Color.white;
        }
        foreach (int key in _groupSprites.Keys.ToList())
        {
            _groupSprites[key] = null;
        }
    }

    private Sprite FindSubSpriteByName(Sprite[] sprites, string targetName)
    {
        return sprites.FirstOrDefault(sp => string.Equals(sp.name, targetName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 按图集路径 + sprite 名称加载 Sprite
    /// 解决同一张图有多个 Sprite 时 LoadAssetAtPath 只返回第一个的问题
    /// </summary>
    private Sprite LoadSpriteByPathAndName(string path, string spriteName)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        if (sprites.Length == 0) return null;

        if (!string.IsNullOrEmpty(spriteName))
        {
            var matched = sprites.FirstOrDefault(s =>
                string.Equals(s.name, spriteName, System.StringComparison.OrdinalIgnoreCase));
            if (matched != null) return matched;
        }
        return sprites[0];
    }

    // ===== 扫描方法 =====
    /// <summary>
    /// 手动遍历父链判断是否在层级中可见（不依赖 activeInHierarchy，避免 prefab 资源中的异常行为）
    /// </summary>
    private bool IsActiveInHierarchy(Transform t)
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

    private List<GpuRoleSlot> ScanPrefabSlots(GameObject prefab)
    {
        List<GpuRoleSlot> slots = new List<GpuRoleSlot>();
        Transform root = prefab.transform;
        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        Array.Sort(renderers, (a, b) => string.CompareOrdinal(GetPath(root, a.transform), GetPath(root, b.transform)));

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            Transform transform = renderer.transform;
            Sprite sprite = renderer.sprite;
            string spritePath = sprite != null ? AssetDatabase.GetAssetPath(sprite) : string.Empty;

            slots.Add(new GpuRoleSlot
            {
                slotId = i,
                slotKey = BuildSlotKey(root, transform),
                slotName = BuildSlotName(transform, renderer),
                objectName = transform.name,
                path = GetPath(root, transform),
                parentPath = transform.parent != null ? GetPath(root, transform.parent) : string.Empty,
                depth = GetDepth(root, transform),
                activeSelf = transform.gameObject.activeSelf,
                activeInHierarchy = IsActiveInHierarchy(transform),
                rendererEnabled = renderer.enabled,
                defaultVisible = transform.gameObject.activeSelf && renderer.enabled && sprite != null,
                sortingLayerId = renderer.sortingLayerID,
                sortingLayerName = renderer.sortingLayerName,
                sortingOrder = renderer.sortingOrder,
                color = renderer.color,
                spriteName = sprite != null ? sprite.name : string.Empty,
                spriteAssetPath = spritePath,
                spriteGuid = !string.IsNullOrEmpty(spritePath) ? AssetDatabase.AssetPathToGUID(spritePath) : string.Empty,
                spriteRectSize = sprite != null ? sprite.rect.size : Vector2.zero,
                spritePivotPixels = sprite != null ? sprite.pivot : Vector2.zero,
                spritePivotNormalized = sprite != null ? new Vector2(sprite.pivot.x / Mathf.Max(1f, sprite.rect.width), sprite.pivot.y / Mathf.Max(1f, sprite.rect.height)) : Vector2.zero,
                spriteBoundsSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.zero,
                pixelsPerUnit = sprite != null ? sprite.pixelsPerUnit : 0f,
                localPosition = transform.localPosition,
                localEulerAngles = transform.localEulerAngles,
                localScale = transform.localScale,
                bindPoseToRoot = root.worldToLocalMatrix * transform.localToWorldMatrix,
                maskInteraction = renderer.maskInteraction,
            });
        }
        return slots;
    }

    private string BuildSlotName(Transform transform, SpriteRenderer renderer)
    {
        string name = transform.name.Trim();
        if (string.IsNullOrEmpty(name))
            name = renderer.sprite != null ? renderer.sprite.name : "Slot";
        return name;
    }

    private string BuildSlotKey(Transform root, Transform transform)
    {
        return GetPath(root, transform)
            .Replace(root.name + "/", "")
            .Replace("/", ".")
            .Replace(" ", "")
            .Trim('.');
    }

    private string GetPath(Transform root, Transform target)
    {
        if (target == root) return root.name;
        Stack<string> names = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root) break;
            current = current.parent;
        }
        return string.Join("/", names.ToArray());
    }

    private int GetDepth(Transform root, Transform target)
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

    // ====================================================================
    // EditorPrefs 持久化
    // ====================================================================
    private const string PrefsKey_PrefabPath = "GpuRoleViewer_PrefabPath";
    private const string PrefsKey_NextGroupId = "GpuRoleViewer_NextGroupId";
    private const string PrefsKey_GroupsJson = "GpuRoleViewer_Groups";
    private const string PrefsKey_SlotsJson = "GpuRoleViewer_Slots";
    private const string PrefsKey_SlotDefsJson = "GpuRoleViewer_SlotDefs";

    public void SaveToEditorPrefs()
    {
        if (_sourcePrefab != null)
            EditorPrefs.SetString(PrefsKey_PrefabPath, AssetDatabase.GetAssetPath(_sourcePrefab));
        else
            EditorPrefs.DeleteKey(PrefsKey_PrefabPath);

        EditorPrefs.SetInt(PrefsKey_NextGroupId, _nextGroupId);

        // 保存 slotDefinitions（只存重建 key/name/path 所需的数据）
        if (_slotDefinitions.Count > 0)
        {
            var defsForSave = _slotDefinitions.Select(s => new SlotDefForSave
            {
                slotKey = s.slotKey,
                slotName = s.slotName,
                path = s.path,
                objectName = s.objectName
            }).ToList();
            EditorPrefs.SetString(PrefsKey_SlotDefsJson, JsonUtility.ToJson(new SlotDefListWrapper { items = defsForSave }));
        }

        // 保存组
        var groupsForSave = _groups.Select(g => new GroupDataForSave
        {
            groupId = g.groupId,
            groupName = g.groupName,
            groupSpritePath = g.groupSpritePath,
            groupSpriteFolder = g.groupSpriteFolder
        }).ToList();
        EditorPrefs.SetString(PrefsKey_GroupsJson, JsonUtility.ToJson(new GroupListWrapper { items = groupsForSave }));

        // 保存每个槽位的关键数据
        var slotsForSave = _styleSlots.Select(s => new SlotDataForSave
        {
            slotKey = s.slotKey,
            slotName = s.slotName,
            spriteFolder = s.spriteFolder,
            spritePath = s.sprite != null ? AssetDatabase.GetAssetPath(s.sprite) : "",
            spriteName = s.sprite != null ? s.sprite.name : "",
            colorR = s.color.r, colorG = s.color.g, colorB = s.color.b, colorA = s.color.a,
            linkedGroupId = s.linkedGroupId,
            linkedSubSpriteName = s.linkedSubSpriteName
        }).ToList();
        EditorPrefs.SetString(PrefsKey_SlotsJson, JsonUtility.ToJson(new SlotListWrapper { items = slotsForSave }));
    }

    public bool LoadFromEditorPrefs()
    {
        if (!EditorPrefs.HasKey(PrefsKey_SlotsJson))
            return false;

        // 版本检测：如果没有 slotDefs 数据说明是旧格式，清空并返回 false
        if (!EditorPrefs.HasKey(PrefsKey_SlotDefsJson))
        {
            EditorPrefs.DeleteKey(PrefsKey_PrefabPath);
            EditorPrefs.DeleteKey(PrefsKey_NextGroupId);
            EditorPrefs.DeleteKey(PrefsKey_GroupsJson);
            EditorPrefs.DeleteKey(PrefsKey_SlotsJson);
            return false;
        }

        string prefabPath = EditorPrefs.GetString(PrefsKey_PrefabPath, "");
        _sourcePrefab = !string.IsNullOrEmpty(prefabPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : null;
        if (_sourcePrefab == null) return false;

        _nextGroupId = EditorPrefs.GetInt(PrefsKey_NextGroupId, 1);
        _groupSprites.Clear();

        // 恢复 root 变换
        Transform rootTransform = _sourcePrefab.transform;
        RootPosition = rootTransform.localPosition;
        RootRotation = rootTransform.localRotation;
        RootScale = rootTransform.localScale;

        // 恢复 slotDefinitions（直接从 EditorPrefs 恢复，不重新扫描）
        _slotDefinitions.Clear();
        string defsJson = EditorPrefs.GetString(PrefsKey_SlotDefsJson, "");
        if (!string.IsNullOrEmpty(defsJson))
        {
            var defWrapper = JsonUtility.FromJson<SlotDefListWrapper>(defsJson);
            if (defWrapper?.items != null)
            {
                foreach (var d in defWrapper.items)
                {
                    _slotDefinitions.Add(new GpuRoleSlot
                    {
                        slotKey = d.slotKey,
                        slotName = d.slotName,
                        path = d.path,
                        objectName = d.objectName
                    });
                }
            }
        }

        // 恢复组
        _groups.Clear();
        string groupsJson = EditorPrefs.GetString(PrefsKey_GroupsJson, "{}");
        var groupWrapper = JsonUtility.FromJson<GroupListWrapper>(groupsJson);
        if (groupWrapper?.items != null)
        {
            foreach (var g in groupWrapper.items)
            {
                _groups.Add(new GroupDataEntry { groupId = g.groupId, groupName = g.groupName, groupSpritePath = g.groupSpritePath, groupSpriteFolder = g.groupSpriteFolder });
                if (!string.IsNullOrEmpty(g.groupSpritePath))
                {
                    var gs = LoadSpriteByPathAndName(g.groupSpritePath, "");
                    if (gs != null) _groupSprites[g.groupId] = gs;
                }
            }
        }

        // 恢复槽位数据
        _styleSlots.Clear();
        string slotsJson = EditorPrefs.GetString(PrefsKey_SlotsJson, "{}");
        var slotWrapper = JsonUtility.FromJson<SlotListWrapper>(slotsJson);
        if (slotWrapper?.items != null)
        {
            foreach (var sd in slotWrapper.items)
            {
                Sprite sprite = LoadSpriteByPathAndName(sd.spritePath, sd.spriteName);
                _styleSlots.Add(new GpuRoleStyleSlot
                {
                    slotKey = sd.slotKey,
                    slotName = sd.slotName,
                    spriteFolder = sd.spriteFolder,
                    sprite = sprite,
                    color = new Color(sd.colorR, sd.colorG, sd.colorB, sd.colorA),
                    linkedGroupId = sd.linkedGroupId,
                    linkedSubSpriteName = sd.linkedSubSpriteName
                });
            }
        }

        // 重新按组大图分配子 sprite
        foreach (var g in _groups)
        {
            if (!string.IsNullOrEmpty(g.groupSpritePath))
            {
                Sprite groupSprite = LoadSpriteByPathAndName(g.groupSpritePath, "");
                if (groupSprite != null)
                    ApplyGroupSpriteToSlots(g.groupId, groupSprite);
            }
        }

        // 如果 slotDefinitions 缺少位置数据（从旧版本 EditorPrefs 恢复），用 prefab 重新扫描
        if (_slotDefinitions.Count > 0 && _slotDefinitions[0].bindPoseToRoot == Matrix4x4.zero)
        {
            _slotDefinitions = ScanPrefabSlots(_sourcePrefab);
        }

        return _sourcePrefab != null && _slotDefinitions.Count > 0 && _styleSlots.Count > 0;
    }

    // ==== JSON 序列化辅助类 ====
    [Serializable]
    private class SlotDefForSave { public string slotKey; public string slotName; public string path; public string objectName; }
    [Serializable]
    private class SlotDefListWrapper { public List<SlotDefForSave> items; }
    [Serializable]
    private class GroupDataForSave { public int groupId; public string groupName; public string groupSpritePath; public string groupSpriteFolder; }
    [Serializable]
    private class GroupListWrapper { public List<GroupDataForSave> items; }
    [Serializable]
    private class SlotDataForSave { public string slotKey; public string slotName; public string spriteFolder; public string spritePath; public string spriteName; public float colorR, colorG, colorB, colorA; public int linkedGroupId; public string linkedSubSpriteName; }
    [Serializable]
    private class SlotListWrapper { public List<SlotDataForSave> items; }
}

/// <summary>
/// 组数据条目（可序列化）
/// </summary>
[Serializable]
public class GroupDataEntry
{
    public int groupId;
    public string groupName;
    public string groupSpritePath;
    public string groupSpriteFolder;
}
