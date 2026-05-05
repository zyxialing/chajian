using System;
using System.Collections.Generic;

/// <summary>
/// 互斥组数据条目
/// </summary>
[Serializable]
public class ExclusiveGroupEntry
{
    public int exclusiveGroupId;
    public string groupName;
    public List<int> memberGroupIds = new List<int>();
    public List<int> memberSlotIndices = new List<int>();
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
    public int exclusiveGroupId = -1;
}
