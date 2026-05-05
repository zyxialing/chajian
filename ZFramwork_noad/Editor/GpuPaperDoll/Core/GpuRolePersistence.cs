using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 数据持久化
/// 负责将 GpuRoleViewerCore 的状态保存到 EditorPrefs 和从中恢复
/// </summary>
public class GpuRolePersistence
{
    private const string PrefsKey_PrefabPath = "GpuRoleViewer_PrefabPath";
    private const string PrefsKey_NextGroupId = "GpuRoleViewer_NextGroupId";
    private const string PrefsKey_NextExclusiveGroupId = "GpuRoleViewer_NextExclusiveGroupId";
    private const string PrefsKey_ExclusiveGroupsJson = "GpuRoleViewer_ExclusiveGroups";
    private const string PrefsKey_GroupsJson = "GpuRoleViewer_Groups";
    private const string PrefsKey_SlotsJson = "GpuRoleViewer_Slots";
    private const string PrefsKey_SlotDefsJson = "GpuRoleViewer_SlotDefs";

    /// <summary>
    /// 保存所有数据到 EditorPrefs
    /// </summary>
    public void Save(GameObject sourcePrefab,
        IReadOnlyList<GpuRoleSlot> slotDefinitions,
        IReadOnlyList<GpuRoleStyleSlot> styleSlots,
        IReadOnlyList<GroupDataEntry> groups,
        IReadOnlyList<ExclusiveGroupEntry> exclusiveGroups,
        int nextGroupId,
        int nextExclusiveGroupId)
    {
        // Prefab 路径
        if (sourcePrefab != null)
            EditorPrefs.SetString(PrefsKey_PrefabPath, AssetDatabase.GetAssetPath(sourcePrefab));
        else
            EditorPrefs.DeleteKey(PrefsKey_PrefabPath);

        // ID 计数器
        EditorPrefs.SetInt(PrefsKey_NextGroupId, nextGroupId);
        EditorPrefs.SetInt(PrefsKey_NextExclusiveGroupId, nextExclusiveGroupId);

        // 互斥组
        var exclGroupsForSave = exclusiveGroups.Select(eg => new ExclusiveGroupDataForSave
        {
            exclusiveGroupId = eg.exclusiveGroupId,
            groupName = eg.groupName,
            memberGroupIds = eg.memberGroupIds,
            memberSlotIndices = eg.memberSlotIndices
        }).ToList();
        EditorPrefs.SetString(PrefsKey_ExclusiveGroupsJson,
            JsonUtility.ToJson(new ExclusiveGroupListWrapper { items = exclGroupsForSave }));

        // 槽位定义（只存重建所需的关键数据）
        if (slotDefinitions.Count > 0)
        {
            var defsForSave = slotDefinitions.Select(s => new SlotDefForSave
            {
                slotKey = s.slotKey,
                slotName = s.slotName,
                path = s.path,
                objectName = s.objectName,
                sortingLayerId = s.sortingLayerId,
                sortingOrder = s.sortingOrder,
                rendererEnabled = s.rendererEnabled,
                localPositionX = s.localPosition.x,
                localPositionY = s.localPosition.y,
                localPositionZ = s.localPosition.z,
                localEulerAnglesX = s.localEulerAngles.x,
                localEulerAnglesY = s.localEulerAngles.y,
                localEulerAnglesZ = s.localEulerAngles.z,
                localScaleX = s.localScale.x,
                localScaleY = s.localScale.y,
                localScaleZ = s.localScale.z,
                bindPose00 = s.bindPoseToRoot.m00, bindPose01 = s.bindPoseToRoot.m10,
                bindPose02 = s.bindPoseToRoot.m20, bindPose03 = s.bindPoseToRoot.m30,
                bindPose10 = s.bindPoseToRoot.m01, bindPose11 = s.bindPoseToRoot.m11,
                bindPose12 = s.bindPoseToRoot.m21, bindPose13 = s.bindPoseToRoot.m31,
                bindPose20 = s.bindPoseToRoot.m02, bindPose21 = s.bindPoseToRoot.m12,
                bindPose22 = s.bindPoseToRoot.m22, bindPose23 = s.bindPoseToRoot.m32,
                bindPose30 = s.bindPoseToRoot.m03, bindPose31 = s.bindPoseToRoot.m13,
                bindPose32 = s.bindPoseToRoot.m23, bindPose33 = s.bindPoseToRoot.m33,
            }).ToList();
            EditorPrefs.SetString(PrefsKey_SlotDefsJson,
                JsonUtility.ToJson(new SlotDefListWrapper { items = defsForSave }));
        }

        // 联动组
        var groupsForSave = groups.Select(g => new GroupDataForSave
        {
            groupId = g.groupId,
            groupName = g.groupName,
            groupSpritePath = g.groupSpritePath,
            groupSpriteFolder = g.groupSpriteFolder,
            exclusiveGroupId = g.exclusiveGroupId
        }).ToList();
        EditorPrefs.SetString(PrefsKey_GroupsJson,
            JsonUtility.ToJson(new GroupListWrapper { items = groupsForSave }));

                // 样式槽位
        var slotsForSave = styleSlots.Select(s => new SlotDataForSave
        {
            slotKey = s.slotKey,
            slotName = s.slotName,
            aliasName = s.aliasName,
            spriteFolder = s.spriteFolder,
            spritePath = s.sprite != null ? AssetDatabase.GetAssetPath(s.sprite) : "",
            spriteName = s.sprite != null ? s.sprite.name : "",
            colorR = s.color.r, colorG = s.color.g, colorB = s.color.b, colorA = s.color.a,
            linkedGroupId = s.linkedGroupId,
            linkedSubSpriteName = s.linkedSubSpriteName,
            exclusiveGroupId = s.exclusiveGroupId
        }).ToList();
        EditorPrefs.SetString(PrefsKey_SlotsJson,
            JsonUtility.ToJson(new SlotListWrapper { items = slotsForSave }));
    }

    /// <summary>
    /// 从 EditorPrefs 恢复数据
    /// </summary>
    public LoadResult Load()
    {
        LoadResult result = new LoadResult();

        if (!EditorPrefs.HasKey(PrefsKey_SlotsJson))
            return result;

        // 版本检测：没有 slotDefs 说明是旧格式
        if (!EditorPrefs.HasKey(PrefsKey_SlotDefsJson))
        {
            ClearAllPrefs();
            return result;
        }

        // 恢复 Prefab
        string prefabPath = EditorPrefs.GetString(PrefsKey_PrefabPath, "");
        result.sourcePrefab = !string.IsNullOrEmpty(prefabPath)
            ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
            : null;
        if (result.sourcePrefab == null) return result;

        // 恢复 ID 计数器
        result.nextGroupId = EditorPrefs.GetInt(PrefsKey_NextGroupId, 1);
        result.nextExclusiveGroupId = EditorPrefs.GetInt(PrefsKey_NextExclusiveGroupId, 1);

        // 恢复互斥组
        string exclGroupsJson = EditorPrefs.GetString(PrefsKey_ExclusiveGroupsJson, "");
        if (!string.IsNullOrEmpty(exclGroupsJson))
        {
            var exclGroupsWrapper = JsonUtility.FromJson<ExclusiveGroupListWrapper>(exclGroupsJson);
            if (exclGroupsWrapper?.items != null)
            {
                foreach (var eg in exclGroupsWrapper.items)
                {
                    var entry = new ExclusiveGroupEntry
                    {
                        exclusiveGroupId = eg.exclusiveGroupId,
                        groupName = eg.groupName,
                        memberGroupIds = eg.memberGroupIds ?? new List<int>(),
                        memberSlotIndices = eg.memberSlotIndices ?? new List<int>()
                    };
                    // 直接添加到内部列表（绕过 CreateExclusiveGroup 的 ID 自增）
                    result.loadedExclusiveGroups.Add(entry);
                }
            }
        }

        // 恢复槽位定义
        string defsJson = EditorPrefs.GetString(PrefsKey_SlotDefsJson, "");
        if (!string.IsNullOrEmpty(defsJson))
        {
            var defWrapper = JsonUtility.FromJson<SlotDefListWrapper>(defsJson);
            if (defWrapper?.items != null)
            {
                foreach (var d in defWrapper.items)
                {
                    var slot = new GpuRoleSlot
                    {
                        slotKey = d.slotKey,
                        slotName = d.slotName,
                        path = d.path,
                        objectName = d.objectName,
                        sortingLayerId = d.sortingLayerId,
                        sortingOrder = d.sortingOrder,
                        rendererEnabled = d.rendererEnabled,
                        localPosition = new Vector3(d.localPositionX, d.localPositionY, d.localPositionZ),
                        localEulerAngles = new Vector3(d.localEulerAnglesX, d.localEulerAnglesY, d.localEulerAnglesZ),
                        localScale = new Vector3(d.localScaleX, d.localScaleY, d.localScaleZ),
                        bindPoseToRoot = new Matrix4x4(
                            new Vector4(d.bindPose00, d.bindPose01, d.bindPose02, d.bindPose03),
                            new Vector4(d.bindPose10, d.bindPose11, d.bindPose12, d.bindPose13),
                            new Vector4(d.bindPose20, d.bindPose21, d.bindPose22, d.bindPose23),
                            new Vector4(d.bindPose30, d.bindPose31, d.bindPose32, d.bindPose33)
                        ),
                    };
                    result.loadedSlotDefs.Add(slot);
                }
            }
        }

        // 恢复联动组
        string groupsJson = EditorPrefs.GetString(PrefsKey_GroupsJson, "{}");
        var groupWrapper = JsonUtility.FromJson<GroupListWrapper>(groupsJson);
        if (groupWrapper?.items != null)
        {
            foreach (var g in groupWrapper.items)
            {
                result.loadedGroups.Add(new GroupDataEntry
                {
                    groupId = g.groupId,
                    groupName = g.groupName,
                    groupSpritePath = g.groupSpritePath,
                    groupSpriteFolder = g.groupSpriteFolder,
                    exclusiveGroupId = g.exclusiveGroupId
                });
            }
        }

        // 恢复样式槽位
        string slotsJson = EditorPrefs.GetString(PrefsKey_SlotsJson, "{}");
        var slotWrapper = JsonUtility.FromJson<SlotListWrapper>(slotsJson);
        if (slotWrapper?.items != null)
        {
            foreach (var sd in slotWrapper.items)
            {
                Sprite sprite = LoadSpriteByPathAndName(sd.spritePath, sd.spriteName);
                                result.loadedStyleSlots.Add(new GpuRoleStyleSlot
                {
                    slotKey = sd.slotKey,
                    slotName = sd.slotName,
                    aliasName = sd.aliasName,
                    spriteFolder = sd.spriteFolder,
                    sprite = sprite,
                    color = new Color(sd.colorR, sd.colorG, sd.colorB, sd.colorA),
                    linkedGroupId = sd.linkedGroupId,
                    linkedSubSpriteName = sd.linkedSubSpriteName,
                    exclusiveGroupId = sd.exclusiveGroupId
                });
            }
        }

        // 恢复组 Sprite 缓存
        foreach (var g in result.loadedGroups)
        {
            if (!string.IsNullOrEmpty(g.groupSpritePath))
            {
                var gs = LoadSpriteByPathAndName(g.groupSpritePath, "");
                if (gs != null)
                    result.loadedGroupSprites[g.groupId] = gs;
            }
        }

        result.success = true;
        return result;
    }

    /// <summary>
    /// 清除所有 EditorPrefs 数据
    /// </summary>
    public void ClearAllPrefs()
    {
        EditorPrefs.DeleteKey(PrefsKey_PrefabPath);
        EditorPrefs.DeleteKey(PrefsKey_NextGroupId);
        EditorPrefs.DeleteKey(PrefsKey_NextExclusiveGroupId);
        EditorPrefs.DeleteKey(PrefsKey_ExclusiveGroupsJson);
        EditorPrefs.DeleteKey(PrefsKey_GroupsJson);
        EditorPrefs.DeleteKey(PrefsKey_SlotsJson);
        EditorPrefs.DeleteKey(PrefsKey_SlotDefsJson);
    }

        private Sprite LoadSpriteByPathAndName(string path, string spriteName)
    {
        return GpuRoleUtility.LoadSpriteByPathAndName(path, spriteName);
    }

    // ===== 加载结果 =====
    public class LoadResult
    {
        public bool success;
        public GameObject sourcePrefab;
        public int nextGroupId = 1;
        public int nextExclusiveGroupId = 1;
        public List<GpuRoleSlot> loadedSlotDefs = new List<GpuRoleSlot>();
        public List<GpuRoleStyleSlot> loadedStyleSlots = new List<GpuRoleStyleSlot>();
        public List<GroupDataEntry> loadedGroups = new List<GroupDataEntry>();
        public List<ExclusiveGroupEntry> loadedExclusiveGroups = new List<ExclusiveGroupEntry>();
        public Dictionary<int, Sprite> loadedGroupSprites = new Dictionary<int, Sprite>();
    }

    // ===== JSON 序列化辅助类 =====
    [Serializable]
    private class SlotDefForSave
    {
        public string slotKey, slotName, path, objectName;
        public int sortingLayerId, sortingOrder;
        public bool rendererEnabled;
        public float localPositionX, localPositionY, localPositionZ;
        public float localEulerAnglesX, localEulerAnglesY, localEulerAnglesZ;
        public float localScaleX = 1f, localScaleY = 1f, localScaleZ = 1f;
        public float bindPose00, bindPose01, bindPose02, bindPose03;
        public float bindPose10, bindPose11, bindPose12, bindPose13;
        public float bindPose20, bindPose21, bindPose22, bindPose23;
        public float bindPose30, bindPose31, bindPose32, bindPose33;
    }
    [Serializable]
    private class SlotDefListWrapper { public List<SlotDefForSave> items; }
    [Serializable]
    private class GroupDataForSave { public int groupId; public string groupName, groupSpritePath, groupSpriteFolder; public int exclusiveGroupId = -1; }
    [Serializable]
    private class GroupListWrapper { public List<GroupDataForSave> items; }
    [Serializable]
    private class SlotDataForSave { public string slotKey, slotName, aliasName, spriteFolder, spritePath, spriteName; public float colorR, colorG, colorB, colorA; public int linkedGroupId; public string linkedSubSpriteName; public int exclusiveGroupId = -1; }
    [Serializable]
    private class SlotListWrapper { public List<SlotDataForSave> items; }
    [Serializable]
    private class ExclusiveGroupDataForSave { public int exclusiveGroupId; public string groupName; public List<int> memberGroupIds; public List<int> memberSlotIndices; }
    [Serializable]
    private class ExclusiveGroupListWrapper { public List<ExclusiveGroupDataForSave> items; }
}
