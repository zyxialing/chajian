using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持久化存储 GpuRoleStyleViewer 的完整状态
/// 避免因重编译、窗口关闭等操作导致数据丢失
/// </summary>
public class GpuRoleViewerData : ScriptableObject
{
    public GameObject sourcePrefab;
    public string sourcePrefabPath;
    public List<GpuRoleSlot> slotDefinitions = new List<GpuRoleSlot>();
    public List<GpuRoleStyleSlot> styleSlots = new List<GpuRoleStyleSlot>();

    // 联动组数据
    public int nextGroupId = 1;
    public List<GroupData> groups = new List<GroupData>();

    [System.Serializable]
    public class GroupData
    {
        public int groupId;
        public string groupName;
        public string groupSpritePath;
    }
}
