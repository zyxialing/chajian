using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GpuRoleStyleData", menuName = "Gpu Paper Doll/Style Data")]
public class GpuRoleStyleData : ScriptableObject
{
    public string generatedAt;
    public List<GpuRoleSlotDefData> slotDefs = new List<GpuRoleSlotDefData>();
    public List<GpuRoleStyleSlot> slots = new List<GpuRoleStyleSlot>();
    public List<GpuRoleLinkedGroup> groups = new List<GpuRoleLinkedGroup>();
    public List<GpuRoleExclusiveGroupData> exclusiveGroups = new List<GpuRoleExclusiveGroupData>();
}

[Serializable]
public class GpuRoleSlotDefData
{
    public int slotId;
    public string slotKey;
    public string slotName;
    public string objectName;
    public string path;
    public string parentPath;
    public int depth;
    public bool activeSelf;
    public bool activeInHierarchy;
    public bool rendererEnabled;
    public bool defaultVisible;
    public int sortingLayerId;
    public string sortingLayerName;
    public int sortingOrder;
    public Color color = Color.white;
    public string spriteName;
    public string spriteAssetPath;
    public string spriteGuid;
    public Vector2 spriteRectSize;
    public Vector2 spritePivotPixels;
    public Vector2 spritePivotNormalized;
    public Vector2 spriteBoundsSize;
    public float pixelsPerUnit;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale;
    // bindPoseToRoot 矩阵（16个float）
    public float bindPose00, bindPose01, bindPose02, bindPose03;
    public float bindPose10, bindPose11, bindPose12, bindPose13;
    public float bindPose20, bindPose21, bindPose22, bindPose23;
    public float bindPose30, bindPose31, bindPose32, bindPose33;
    public SpriteMaskInteraction maskInteraction;
    public int drawOrder;
    public int internalOrder;
}

[Serializable]
public class GpuRoleExclusiveGroupData
{
    public int exclusiveGroupId;
    public string groupName;
    public List<int> memberGroupIds = new List<int>();
    public List<int> memberSlotIndices = new List<int>();
}

[Serializable]
public class GpuRoleStyleSlot
{
    public string slotKey;
    public string slotName;
    public string aliasName; // 别名，用于生成代码变量名（默认等于 slotKey，可修改）
    public string spriteFolder; // 这个槽位的图片目录
    public Sprite sprite;
    public Color color = Color.white;
    public int linkedGroupId = -1; // -1 表示不联动，相同 id 的槽位属于一个联动组
    public string linkedSubSpriteName = ""; // 在联动组大图中对应的子 sprite 名
    public int exclusiveGroupId = -1; // -1 表示不互斥，相同 id 的槽位属于一个互斥组
}

[Serializable]
public class GpuRoleLinkedGroup
{
    public int groupId;
    public string groupName;
    public Sprite groupSprite; // 整张大图
    public string groupSpritePath; // 大图路径
    public string groupSpriteFolder; // 组目录
}
