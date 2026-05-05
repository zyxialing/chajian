using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 互斥组管理
/// 负责互斥组的创建、解散、成员管理、互斥逻辑
/// </summary>
public class GpuRoleExclusiveManager
{
    private int _nextExclusiveGroupId = 1;
    private List<ExclusiveGroupEntry> _exclusiveGroups = new List<ExclusiveGroupEntry>();

    public IReadOnlyList<ExclusiveGroupEntry> ExclusiveGroups => _exclusiveGroups;
    public int NextExclusiveGroupId
    {
        get => _nextExclusiveGroupId;
        set => _nextExclusiveGroupId = value;
    }

    /// <summary>
    /// 重置所有互斥组数据
    /// </summary>
    public void Reset()
    {
        _nextExclusiveGroupId = 1;
        _exclusiveGroups.Clear();
    }

    /// <summary>
    /// 直接设置互斥组列表（用于从持久化恢复）
    /// </summary>
    public void SetExclusiveGroupsDirect(List<ExclusiveGroupEntry> groups)
    {
        _exclusiveGroups = new List<ExclusiveGroupEntry>(groups);
        foreach (var eg in _exclusiveGroups)
        {
            if (eg.exclusiveGroupId >= _nextExclusiveGroupId)
                _nextExclusiveGroupId = eg.exclusiveGroupId + 1;
        }
    }

    /// <summary>
    /// 创建一个互斥组
    /// </summary>
    public int CreateExclusiveGroup()
    {
        int id = _nextExclusiveGroupId++;
        _exclusiveGroups.Add(new ExclusiveGroupEntry
        {
            exclusiveGroupId = id,
            groupName = $"Exclusive Group {id}",
            memberGroupIds = new List<int>(),
            memberSlotIndices = new List<int>()
        });
        return id;
    }

    /// <summary>
    /// 获取互斥组名称
    /// </summary>
    public string GetExclusiveGroupName(int exclusiveGroupId)
    {
        var eg = _exclusiveGroups.Find(x => x.exclusiveGroupId == exclusiveGroupId);
        return eg != null ? eg.groupName : $"Exclusive Group {exclusiveGroupId}";
    }

    /// <summary>
    /// 设置互斥组名称
    /// </summary>
    public void SetExclusiveGroupName(int exclusiveGroupId, string name)
    {
        var eg = _exclusiveGroups.Find(x => x.exclusiveGroupId == exclusiveGroupId);
        if (eg != null) eg.groupName = name;
    }

    /// <summary>
    /// 将联动组加入互斥组
    /// </summary>
    public void AddGroupToExclusive(int exclusiveGroupId, int groupId)
    {
        var eg = _exclusiveGroups.Find(x => x.exclusiveGroupId == exclusiveGroupId);
        if (eg != null && !eg.memberGroupIds.Contains(groupId))
        {
            eg.memberGroupIds.Add(groupId);
            // 从其他互斥组中移除该 group
            foreach (var other in _exclusiveGroups)
            {
                if (other.exclusiveGroupId != exclusiveGroupId)
                    other.memberGroupIds.Remove(groupId);
            }
        }
    }

    /// <summary>
    /// 将独立槽位加入互斥组
    /// </summary>
    public void AddSlotToExclusive(int exclusiveGroupId, int slotIndex)
    {
        var eg = _exclusiveGroups.Find(x => x.exclusiveGroupId == exclusiveGroupId);
        if (eg != null && !eg.memberSlotIndices.Contains(slotIndex))
        {
            eg.memberSlotIndices.Add(slotIndex);
            // 从其他互斥组中移除该 slot
            foreach (var other in _exclusiveGroups)
            {
                if (other.exclusiveGroupId != exclusiveGroupId)
                    other.memberSlotIndices.Remove(slotIndex);
            }
        }
    }

    /// <summary>
    /// 从互斥组中移除联动组
    /// </summary>
    public void RemoveGroupFromExclusive(int exclusiveGroupId, int groupId)
    {
        var eg = _exclusiveGroups.Find(x => x.exclusiveGroupId == exclusiveGroupId);
        if (eg != null) eg.memberGroupIds.Remove(groupId);
    }

    /// <summary>
    /// 从互斥组中移除独立槽位
    /// </summary>
    public void RemoveSlotFromExclusive(int exclusiveGroupId, int slotIndex)
    {
        var eg = _exclusiveGroups.Find(x => x.exclusiveGroupId == exclusiveGroupId);
        if (eg != null) eg.memberSlotIndices.Remove(slotIndex);
    }

    /// <summary>
    /// 解散互斥组
    /// </summary>
    public void DissolveExclusiveGroup(int exclusiveGroupId)
    {
        _exclusiveGroups.RemoveAll(x => x.exclusiveGroupId == exclusiveGroupId);
    }

    /// <summary>
    /// 获取互斥组内的成员描述列表
    /// </summary>
    public List<string> GetExclusiveGroupMemberNames(int exclusiveGroupId, IReadOnlyList<GpuRoleStyleSlot> styleSlots, IReadOnlyList<GroupDataEntry> groups)
    {
        List<string> names = new List<string>();
        var eg = _exclusiveGroups.Find(x => x.exclusiveGroupId == exclusiveGroupId);
        if (eg == null) return names;

        foreach (var gId in eg.memberGroupIds)
        {
            var g = groups.FirstOrDefault(x => x.groupId == gId);
            if (g != null)
                names.Add($"[Group] {g.groupName}");
            else
                names.Add($"[Group] (deleted {gId})");
        }

        foreach (var idx in eg.memberSlotIndices)
        {
            if (idx >= 0 && idx < styleSlots.Count)
                names.Add($"[Slot] {styleSlots[idx].slotName}");
            else
                names.Add($"[Slot] (invalid {idx})");
        }
        return names;
    }

    /// <summary>
    /// 当某个 slot 的 sprite 发生变化时，检查互斥组并隐藏同组其他成员
    /// </summary>
    public void ApplySlotExclusive(int slotIndex, IReadOnlyList<GpuRoleStyleSlot> styleSlots)
    {
        if (slotIndex < 0 || slotIndex >= styleSlots.Count) return;
        var slot = styleSlots[slotIndex];
        if (slot.sprite == null) return;

        foreach (var eg in _exclusiveGroups)
        {
            if (eg.memberSlotIndices.Contains(slotIndex))
            {
                // 隐藏同互斥组的其他 slot
                foreach (var idx in eg.memberSlotIndices)
                {
                    if (idx != slotIndex)
                        styleSlots[idx].sprite = null;
                }
                // 隐藏同互斥组的其他 group 的所有 slot
                foreach (var gId in eg.memberGroupIds)
                {
                    for (int i = 0; i < styleSlots.Count; i++)
                    {
                        if (styleSlots[i].linkedGroupId == gId)
                            styleSlots[i].sprite = null;
                    }
                }
                break;
            }
        }
    }

    /// <summary>
    /// 当某个联动组的 sprite 发生变化时，检查互斥组并隐藏同组其他成员
    /// </summary>
    public void ApplyGroupExclusive(int groupId, IReadOnlyList<GpuRoleStyleSlot> styleSlots)
    {
        // 检查该组是否有任何 sprite 非 null
        bool hasSprite = false;
        for (int i = 0; i < styleSlots.Count; i++)
        {
            if (styleSlots[i].linkedGroupId == groupId && styleSlots[i].sprite != null)
            {
                hasSprite = true;
                break;
            }
        }
        if (!hasSprite) return;

        foreach (var eg in _exclusiveGroups)
        {
            if (eg.memberGroupIds.Contains(groupId))
            {
                // 隐藏同互斥组的其他 group 的所有 slot
                foreach (var gId in eg.memberGroupIds)
                {
                    if (gId == groupId) continue;
                    for (int i = 0; i < styleSlots.Count; i++)
                    {
                        if (styleSlots[i].linkedGroupId == gId)
                            styleSlots[i].sprite = null;
                    }
                }
                // 隐藏同互斥组的其他 slot
                foreach (var idx in eg.memberSlotIndices)
                {
                    styleSlots[idx].sprite = null;
                }
                break;
            }
        }
    }

    /// <summary>
    /// 从槽位数据重建互斥组（用于加载旧数据兼容）
    /// </summary>
    public void RebuildFromSlotData(IReadOnlyList<GpuRoleStyleSlot> styleSlots)
    {
        Reset();

        // 收集所有用到的互斥组 ID
        Dictionary<int, List<int>> slotMap = new Dictionary<int, List<int>>();
        for (int i = 0; i < styleSlots.Count; i++)
        {
            int egId = styleSlots[i].exclusiveGroupId;
            if (egId >= 0)
            {
                if (!slotMap.ContainsKey(egId))
                    slotMap[egId] = new List<int>();
                slotMap[egId].Add(i);
            }
        }

        // 为每个互斥组 ID 创建 ExclusiveGroupEntry
        foreach (var kvp in slotMap)
        {
            int newId = CreateExclusiveGroup();
            // 修正 ID 为原始值
            var entry = _exclusiveGroups.Find(e => e.exclusiveGroupId == newId);
            if (entry != null)
            {
                entry.exclusiveGroupId = kvp.Key;
                foreach (var idx in kvp.Value)
                    entry.memberSlotIndices.Add(idx);
            }
            // 更新 nextId 防止冲突
            if (kvp.Key >= _nextExclusiveGroupId)
                _nextExclusiveGroupId = kvp.Key + 1;
        }
    }
}
