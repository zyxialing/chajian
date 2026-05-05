using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GPU 角色导出数据
/// 包含图集、UV 映射、Slot 定义、组配置、动画数据等所有运行时需要的数据
/// 一个文件搞定所有
/// </summary>
[CreateAssetMenu(fileName = "GpuRoleExportData", menuName = "Gpu Paper Doll/Export Data")]
public class GpuRoleExportData : ScriptableObject
{
    public string prefabName;
    public List<AtlasData> atlases = new List<AtlasData>();
    public List<SpriteUVData> spriteUVs = new List<SpriteUVData>();
    public List<SlotExportData> slots = new List<SlotExportData>();
    public List<GroupExportData> groups = new List<GroupExportData>();
    public List<AnimExportData> animations = new List<AnimExportData>();
    public Texture2D combinedAnimDataTex;
    public int combinedAnimDataTexWidth;
    public int combinedAnimDataTexHeight;
}

[Serializable]
public class AnimExportData
{
    public string animName;
    public float frameRate = 30f;
    public float length;
    public int totalFrames;
    public List<BakedSlotData> slotKeys = new List<BakedSlotData>();
    public List<BakedFrameData> frames = new List<BakedFrameData>();
    public Texture2D animDataTex; // GPU 动画数据纹理：宽=slotCount, 高=totalFrames, RGBAHalf
    public int animDataTexWidth;
    public int animDataTexHeight;
    public int animDataTexY;
}

[Serializable]
public class AtlasData
{
    public string name;
    public Texture2D texture;
    public int width;
    public int height;
}

[Serializable]
public class SpriteUVData
{
    public int spriteId;
    public string spriteName;
    public int atlasIndex;
    public float uMin, vMin, uMax, vMax;       // 在图集中的 UV（裁剪后的）
    public float originalWidth, originalHeight;  // 原始 Sprite 大小
    public float cropX, cropY;                   // 裁剪偏移（像素）
    public float cropW, cropH;                   // 裁剪后大小（像素）
    public float pivotX, pivotY;                 // 原始 Sprite 的 Pivot（归一化）
    public Vector2[] meshVertices;               // Tight Mesh 顶点（裁剪后坐标）
    public Vector2[] meshUVs;                    // Tight Mesh UV
    public ushort[] meshTriangles;               // Tight Mesh 三角形索引
    [NonSerialized] public Sprite sourceSprite;  // 原始 Sprite 引用（仅 Editor 下使用，不序列化）
}

[Serializable]
public class SlotExportData
{
    public int slotId;
    public string slotKey;
    public string slotName;
    public string aliasName;           // 别名，用于生成代码变量名
    public int defaultSpriteId;        // 默认 Sprite 的 ID
    public int[] availableSpriteIds;   // 所有可选的 Sprite ID（按顺序，运行时按索引选）
    public bool canBeEmpty;            // 是否可以不渲染
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale;
    public int sortingOrder;
    public int sortingLayerId;
    public string sortingLayerName;
    public int internalOrder; // sortingOrder * InternalOrderStep + drawOrder，用于精确排序
}

[Serializable]
public class GroupExportData
{
    public int groupId;
    public string groupName;
    public int[] slotIndices;              // 组内包含的 slot 索引
    public List<GroupVariant> variants = new List<GroupVariant>();  // 方案列表
}

[Serializable]
public class GroupVariant
{
    public string variantName;   // 方案名，如 "body_1"
    public int[] spriteIds;      // 每个 slot 对应的 Sprite ID（与 slotIndices 顺序一致）
}
