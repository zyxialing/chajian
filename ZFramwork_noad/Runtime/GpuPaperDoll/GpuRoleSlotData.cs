using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GpuRoleSlotData", menuName = "Gpu Paper Doll/Slot Data")]
public class GpuRoleSlotData : ScriptableObject
{
    public GameObject sourcePrefab;
    public string sourcePrefabPath;
    public string generatedAt;
    public List<GpuRoleSlot> slots = new List<GpuRoleSlot>();
}

[Serializable]
public class GpuRoleSlot
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
    public Color color;

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
    public Matrix4x4 bindPoseToRoot;
    public SpriteMaskInteraction maskInteraction;
}
