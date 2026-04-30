using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GpuRoleStyleData", menuName = "Gpu Paper Doll/Style Data")]
public class GpuRoleStyleData : ScriptableObject
{
    public GameObject sourcePrefab;
    public string sourcePrefabPath;
    public string generatedAt;
    public List<GpuRoleStyleSlot> slots = new List<GpuRoleStyleSlot>();
}

[Serializable]
public class GpuRoleStyleSlot
{
    public string slotKey;
    public string slotName;
    public string spriteFolder; // 这个槽位的图片目录
    public Sprite sprite;
    public Color color = Color.white;
    public int linkedGroupId = -1; // -1 表示不联动，相同 id 的槽位属于一个联动组
    public string linkedSubSpriteName = ""; // 在联动组大图中对应的子 sprite 名
}

[Serializable]
public class GpuRoleLinkedGroup
{
    public int groupId;
    public string groupName;
    public Sprite groupSprite; // 整张大图
}
