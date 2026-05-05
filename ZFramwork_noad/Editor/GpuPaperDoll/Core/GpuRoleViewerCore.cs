using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GpuRoleStyleViewer 的核心数据与业务逻辑（不依赖 GUI）
/// 组合各 Manager 模块，提供统一接口
/// </summary>
public class GpuRoleViewerCore : ScriptableObject
{
    // ===== 子模块 =====
    public GpuRoleSlotManager SlotManager { get; private set; }
    public GpuRoleGroupManager GroupManager { get; private set; }
    public GpuRoleExclusiveManager ExclusiveManager { get; private set; }
    public GpuRolePersistence Persistence { get; private set; }

    // ===== 数据 =====
    private GameObject _sourcePrefab;

    // ===== Prefab 根节点变换 =====
    public Vector3 RootPosition { get; set; }
    public Quaternion RootRotation { get; set; }
    public Vector3 RootScale { get; set; } = Vector3.one;

    // ===== 访问器（兼容旧代码） =====
    public GameObject SourcePrefab
    {
        get => _sourcePrefab;
        set => _sourcePrefab = value;
    }
    public List<GpuRoleSlot> SlotDefinitions => SlotManager.SlotDefinitions;
    public List<GpuRoleStyleSlot> StyleSlots => SlotManager.StyleSlots;
    public int NextGroupId
    {
        get => GroupManager.NextGroupId;
        set => GroupManager.NextGroupId = value;
    }
    public Dictionary<int, Sprite> GroupSprites => GroupManager.GroupSprites;
    public IReadOnlyList<GroupDataEntry> Groups => GroupManager.Groups;
    public IReadOnlyList<ExclusiveGroupEntry> ExclusiveGroups => ExclusiveManager.ExclusiveGroups;

    public bool HasData => SlotManager.SlotDefinitions.Count > 0;

    private void OnEnable()
    {
        SlotManager = new GpuRoleSlotManager();
        GroupManager = new GpuRoleGroupManager();
        ExclusiveManager = new GpuRoleExclusiveManager();
        Persistence = new GpuRolePersistence();
    }

    // ===== 联动组操作（委托） =====
    public string GetGroupName(int groupId) => GroupManager.GetGroupName(groupId);
    public void SetGroupName(int groupId, string name) => GroupManager.SetGroupName(groupId, name);
    public int CreateGroup(string name = null) => GroupManager.CreateGroup(name);
    public void RemoveGroup(int groupId) => GroupManager.RemoveGroup(groupId);
    public bool GroupExists(int groupId) => GroupManager.GroupExists(groupId);
    public string GetGroupSpritePath(int groupId) => GroupManager.GetGroupSpritePath(groupId);
    public void SetGroupSpritePath(int groupId, string path) => GroupManager.SetGroupSpritePath(groupId, path);
    public string GetGroupSpriteFolder(int groupId) => GroupManager.GetGroupSpriteFolder(groupId);
    public void SetGroupSpriteFolder(int groupId, string folder) => GroupManager.SetGroupSpriteFolder(groupId, folder);
    public bool TryApplyGroupSpriteToSlots(int groupId, Sprite groupSprite, out List<string> missingSubSprites)
        => GroupManager.TryApplyGroupSpriteToSlots(groupId, groupSprite, StyleSlots, out missingSubSprites);
    public void ApplyGroupSpriteToSlots(int groupId, Sprite groupSprite)
        => GroupManager.TryApplyGroupSpriteToSlots(groupId, groupSprite, StyleSlots, out _);
    public void ClearGroupSprite(int groupId) => GroupManager.ClearGroupSprite(groupId, StyleSlots);
    public bool RandomizeLinkedGroup(int groupId) => GroupManager.RandomizeLinkedGroup(groupId, StyleSlots);

    // ===== 互斥组操作（委托） =====
    public int CreateExclusiveGroup() => ExclusiveManager.CreateExclusiveGroup();
    public string GetExclusiveGroupName(int exclusiveGroupId) => ExclusiveManager.GetExclusiveGroupName(exclusiveGroupId);
    public void SetExclusiveGroupName(int exclusiveGroupId, string name) => ExclusiveManager.SetExclusiveGroupName(exclusiveGroupId, name);
    public void AddGroupToExclusive(int exclusiveGroupId, int groupId) => ExclusiveManager.AddGroupToExclusive(exclusiveGroupId, groupId);
    public void AddSlotToExclusive(int exclusiveGroupId, int slotIndex) => ExclusiveManager.AddSlotToExclusive(exclusiveGroupId, slotIndex);
    public void RemoveGroupFromExclusive(int exclusiveGroupId, int groupId) => ExclusiveManager.RemoveGroupFromExclusive(exclusiveGroupId, groupId);
    public void RemoveSlotFromExclusive(int exclusiveGroupId, int slotIndex) => ExclusiveManager.RemoveSlotFromExclusive(exclusiveGroupId, slotIndex);
    public void DissolveExclusiveGroup(int exclusiveGroupId) => ExclusiveManager.DissolveExclusiveGroup(exclusiveGroupId);
    public List<string> GetExclusiveGroupMemberNames(int exclusiveGroupId)
        => ExclusiveManager.GetExclusiveGroupMemberNames(exclusiveGroupId, StyleSlots, Groups.ToList());
    public void ApplySlotExclusive(int slotIndex) => ExclusiveManager.ApplySlotExclusive(slotIndex, StyleSlots);
    public void ApplyGroupExclusive(int groupId) => ExclusiveManager.ApplyGroupExclusive(groupId, StyleSlots);

    // ===== 槽位操作（委托） =====
    public GpuRoleStyleSlot GetSlot(int index) => SlotManager.GetSlot(index);
    public List<int> GetSlotIndicesInGroup(int groupId) => SlotManager.GetSlotIndicesInGroup(groupId);
    public List<string> GetSlotNamesInGroup(int groupId) => SlotManager.GetSlotNamesInGroup(groupId);
    public Sprite PickRandomSpriteFromFolder(string folderPath) => SlotManager.PickRandomSpriteFromFolder(folderPath);

    // ===== 设置组（兼容旧代码） =====
    public void SetGroups(List<GroupDataEntry> groups)
    {
        GroupManager.SetGroupsDirect(groups);
    }

    // ===== 核心方法 =====

    /// <summary>
    /// 从 Prefab 加载槽位定义并初始化
    /// </summary>
    public void LoadFromPrefab(GameObject prefab)
    {
        if (prefab == null) return;

        _sourcePrefab = prefab;

        // 重置所有管理器
        GroupManager.Reset();
        ExclusiveManager.Reset();

        // 记录 root 变换
        Transform rootTransform = prefab.transform;
        RootPosition = rootTransform.localPosition;
        RootRotation = rootTransform.localRotation;
        RootScale = rootTransform.localScale;

        // 加载槽位
        SlotManager.LoadFromPrefab(prefab);

        // 自动识别身体组
        GroupManager.AutoDetectBodyGroup(StyleSlots, (idx, subName) =>
        {
            StyleSlots[idx].linkedGroupId = GroupManager.Groups.Last().groupId;
            StyleSlots[idx].linkedSubSpriteName = subName;
        });
    }

    /// <summary>
    /// 清空所有 Sprite
    /// </summary>
    public void ClearAllSprites()
    {
        SlotManager.ClearAllSprites();
        foreach (int key in GroupManager.GroupSprites.Keys.ToList())
        {
            GroupManager.GroupSprites[key] = null;
        }
    }

    // ===== 持久化 =====

    /// <summary>
    /// 保存到 EditorPrefs
    /// </summary>
    public void SaveToEditorPrefs()
    {
        Persistence.Save(_sourcePrefab, SlotManager.SlotDefinitions.ToList(),
            SlotManager.StyleSlots.ToList(),
            GroupManager.Groups.ToList(),
            ExclusiveManager.ExclusiveGroups.ToList(),
            GroupManager.NextGroupId,
            ExclusiveManager.NextExclusiveGroupId);
    }

    /// <summary>
    /// 从 EditorPrefs 恢复
    /// </summary>
    public bool LoadFromEditorPrefs()
    {
        var result = Persistence.Load();
        if (!result.success) return false;

        _sourcePrefab = result.sourcePrefab;
        if (_sourcePrefab == null) return false;

        // 重新从 Prefab 扫描槽位定义（确保 bindPoseToRoot 矩阵正确）
        SlotManager.LoadFromPrefab(_sourcePrefab);

        // 恢复 root 变换
        Transform rootTransform = _sourcePrefab.transform;
        RootPosition = rootTransform.localPosition;
        RootRotation = rootTransform.localRotation;
        RootScale = rootTransform.localScale;

        // 恢复 ID 计数器
        GroupManager.NextGroupId = result.nextGroupId;
        ExclusiveManager.NextExclusiveGroupId = result.nextExclusiveGroupId;

        // 恢复互斥组
        ExclusiveManager.SetExclusiveGroupsDirect(result.loadedExclusiveGroups);

        // 恢复联动组
        GroupManager.SetGroupsDirect(result.loadedGroups);

        // 恢复组 Sprite 缓存
        GroupManager.GroupSprites.Clear();
        foreach (var kvp in result.loadedGroupSprites)
            GroupManager.GroupSprites[kvp.Key] = kvp.Value;

        // 恢复样式槽位（按 slotKey 匹配，因为重新扫描后索引可能变化）
        for (int i = 0; i < SlotManager.StyleSlots.Count; i++)
        {
            var saved = result.loadedStyleSlots.FirstOrDefault(s => s.slotKey == SlotManager.StyleSlots[i].slotKey);
            if (saved != null)
            {
                                SlotManager.StyleSlots[i].spriteFolder = saved.spriteFolder;
                SlotManager.StyleSlots[i].sprite = saved.sprite;
                SlotManager.StyleSlots[i].color = saved.color;
                SlotManager.StyleSlots[i].linkedGroupId = saved.linkedGroupId;
                SlotManager.StyleSlots[i].linkedSubSpriteName = saved.linkedSubSpriteName;
                SlotManager.StyleSlots[i].exclusiveGroupId = saved.exclusiveGroupId;
                SlotManager.StyleSlots[i].aliasName = saved.aliasName;
            }
        }

        // 重新按组大图分配子 sprite
        foreach (var g in result.loadedGroups)
        {
            if (!string.IsNullOrEmpty(g.groupSpritePath))
            {
                Sprite groupSprite = GroupManager.LoadSpriteByPathAndName(g.groupSpritePath, "");
                if (groupSprite != null)
                    GroupManager.TryApplyGroupSpriteToSlots(g.groupId, groupSprite, StyleSlots, out _);
            }
        }

        return _sourcePrefab != null && SlotManager.SlotDefinitions.Count > 0 && SlotManager.StyleSlots.Count > 0;
    }
}
