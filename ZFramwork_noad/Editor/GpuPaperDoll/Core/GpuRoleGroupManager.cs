using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 联动组管理
/// 负责联动组的创建、删除、槽位分配、大图分配、随机等
/// </summary>
public class GpuRoleGroupManager
{
    private int _nextGroupId = 1;
    private List<GroupDataEntry> _groups = new List<GroupDataEntry>();
    private Dictionary<int, Sprite> _groupSprites = new Dictionary<int, Sprite>();

    public IReadOnlyList<GroupDataEntry> Groups => _groups;
    public Dictionary<int, Sprite> GroupSprites => _groupSprites;
    public int NextGroupId
    {
        get => _nextGroupId;
        set => _nextGroupId = value;
    }

    /// <summary>
    /// 重置所有组数据
    /// </summary>
    public void Reset()
    {
        _nextGroupId = 1;
        _groups.Clear();
        _groupSprites.Clear();
    }

    /// <summary>
    /// 直接设置组列表（用于从持久化恢复）
    /// </summary>
    public void SetGroupsDirect(List<GroupDataEntry> groups)
    {
        _groups = new List<GroupDataEntry>(groups);
        foreach (var g in _groups)
        {
            if (g.groupId >= _nextGroupId)
                _nextGroupId = g.groupId + 1;
        }
    }

    /// <summary>
    /// 创建新联动组
    /// </summary>
    public int CreateGroup(string name = null)
    {
        int id = _nextGroupId++;
        _groups.Add(new GroupDataEntry { groupId = id, groupName = name ?? $"Group {id}" });
        _groupSprites[id] = null;
        return id;
    }

    /// <summary>
    /// 删除联动组
    /// </summary>
    public void RemoveGroup(int groupId)
    {
        _groups.RemoveAll(x => x.groupId == groupId);
        _groupSprites.Remove(groupId);
    }

    /// <summary>
    /// 检查联动组是否存在
    /// </summary>
    public bool GroupExists(int groupId) => _groups.Any(x => x.groupId == groupId);

    /// <summary>
    /// 获取联动组名称
    /// </summary>
    public string GetGroupName(int groupId)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        return g != null ? g.groupName : $"Group {groupId}";
    }

    /// <summary>
    /// 设置联动组名称
    /// </summary>
    public void SetGroupName(int groupId, string name)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        if (g != null) g.groupName = name;
    }

    /// <summary>
    /// 获取联动组大图路径
    /// </summary>
    public string GetGroupSpritePath(int groupId)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        return g?.groupSpritePath ?? "";
    }

    /// <summary>
    /// 设置联动组大图路径
    /// </summary>
    public void SetGroupSpritePath(int groupId, string path)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        if (g != null) g.groupSpritePath = path;
    }

    /// <summary>
    /// 获取联动组目录
    /// </summary>
    public string GetGroupSpriteFolder(int groupId)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        return g?.groupSpriteFolder ?? "";
    }

    /// <summary>
    /// 设置联动组目录
    /// </summary>
    public void SetGroupSpriteFolder(int groupId, string folder)
    {
        var g = _groups.Find(x => x.groupId == groupId);
        if (g != null) g.groupSpriteFolder = folder;
    }

    /// <summary>
    /// 自动检测身体组（Body, Arm_L, Arm_R, Foot_L, Foot_R, Head）
    /// 返回创建的组 ID，如果未检测到则返回 -1
    /// </summary>
    public int AutoDetectBodyGroup(IReadOnlyList<GpuRoleStyleSlot> styleSlots, Action<int, string> onAssignSlot)
    {
        string[] bodyPartNames = { "Body", "Arm_L", "Arm_R", "Foot_L", "Foot_R", "Head" };
        List<int> matched = new List<int>();

        for (int i = 0; i < styleSlots.Count; i++)
        {
            if (bodyPartNames.Any(bp => string.Equals(styleSlots[i].slotName.Trim(), bp, StringComparison.OrdinalIgnoreCase)))
                matched.Add(i);
        }

        if (matched.Count >= 2)
        {
            int gId = CreateGroup("Body Group");
            foreach (int idx in matched)
            {
                onAssignSlot?.Invoke(idx, styleSlots[idx].slotName);
            }
            return gId;
        }
        return -1;
    }

    /// <summary>
    /// 将一张大图按子 Sprite 名分配到组内各槽位
    /// </summary>
    public bool TryApplyGroupSpriteToSlots(int groupId, Sprite groupSprite,
        IReadOnlyList<GpuRoleStyleSlot> styleSlots, out List<string> missingSubSprites)
    {
        missingSubSprites = new List<string>();
        if (groupSprite == null) return false;

        string spritePath = AssetDatabase.GetAssetPath(groupSprite);
        Sprite[] allSubSprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .ToArray();

        _groupSprites[groupId] = groupSprite;

        for (int i = 0; i < styleSlots.Count; i++)
        {
            if (styleSlots[i].linkedGroupId != groupId) continue;

            string subName = styleSlots[i].linkedSubSpriteName;
            if (!string.IsNullOrEmpty(subName))
            {
                var matched = allSubSprites.FirstOrDefault(sp =>
                    string.Equals(sp.name, subName, StringComparison.OrdinalIgnoreCase));
                styleSlots[i].sprite = matched;
                if (matched == null)
                    missingSubSprites.Add(subName);
            }
            else
            {
                styleSlots[i].sprite = null;
            }
        }

        return true;
    }

    /// <summary>
    /// 清空组内所有槽位的 Sprite
    /// </summary>
    public void ClearGroupSprite(int groupId, IReadOnlyList<GpuRoleStyleSlot> styleSlots)
    {
        _groupSprites[groupId] = null;
        SetGroupSpritePath(groupId, "");

        for (int i = 0; i < styleSlots.Count; i++)
        {
            if (styleSlots[i].linkedGroupId != groupId) continue;
            styleSlots[i].sprite = null;
            styleSlots[i].color = Color.white;
        }
    }

    /// <summary>
    /// 从组目录或槽位目录随机选大图
    /// </summary>
    public bool RandomizeLinkedGroup(int groupId, IReadOnlyList<GpuRoleStyleSlot> styleSlots)
    {
        string folderPath = "";

        // 优先用组目录
        var g = _groups.Find(x => x.groupId == groupId);
        if (g != null)
            folderPath = g.groupSpriteFolder;

        // fallback 到槽位目录
        if (string.IsNullOrEmpty(folderPath))
        {
            for (int i = 0; i < styleSlots.Count; i++)
            {
                if (styleSlots[i].linkedGroupId == groupId && !string.IsNullOrEmpty(styleSlots[i].spriteFolder))
                {
                    folderPath = styleSlots[i].spriteFolder;
                    break;
                }
            }
        }

        // fallback 到当前组大图所在目录
        if (string.IsNullOrEmpty(folderPath) && g != null && !string.IsNullOrEmpty(g.groupSpritePath))
        {
            string dir = System.IO.Path.GetDirectoryName(g.groupSpritePath);
            if (!string.IsNullOrEmpty(dir))
            {
                dir = dir.Replace("\\", "/");
                if (AssetDatabase.IsValidFolder(dir))
                    folderPath = dir;
            }
        }

        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return false;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        if (guids.Length == 0) return false;

        string path = AssetDatabase.GUIDToAssetPath(guids[UnityEngine.Random.Range(0, guids.Length)]);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) return false;

        // 用子 Sprite 名称匹配槽位的 linkedSubSpriteName
        string spritePath = AssetDatabase.GetAssetPath(sprite);
        Sprite[] allSubSprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .ToArray();

        _groupSprites[groupId] = sprite;

        for (int i = 0; i < styleSlots.Count; i++)
        {
            if (styleSlots[i].linkedGroupId != groupId) continue;

            string subName = styleSlots[i].linkedSubSpriteName;
            if (!string.IsNullOrEmpty(subName))
            {
                var matched = allSubSprites.FirstOrDefault(sp =>
                    string.Equals(sp.name, subName, StringComparison.OrdinalIgnoreCase));
                styleSlots[i].sprite = matched;
            }
            else
            {
                styleSlots[i].sprite = null;
            }
        }

        SetGroupSpritePath(groupId, path);
        return true;
    }

        public Sprite LoadSpriteByPathAndName(string path, string spriteName)
    {
        return GpuRoleUtility.LoadSpriteByPathAndName(path, spriteName);
    }
}
