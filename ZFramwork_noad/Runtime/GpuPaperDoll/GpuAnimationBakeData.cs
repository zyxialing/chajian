using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GpuAnimBake", menuName = "Gpu Paper Doll/Animation Bake Data")]
public class GpuAnimationBakeData : ScriptableObject
{
    // ????????????????????
    public string animName;
    public float frameRate = 30f;
    public float length;
    public int totalFrames;
    public List<BakedSlotData> slotKeys = new List<BakedSlotData>();
    public List<BakedFrameData> frames = new List<BakedFrameData>();

    // ??????
    public List<SingleAnimationData> animations = new List<SingleAnimationData>();
}

[Serializable]
public class SingleAnimationData
{
    public string animName;
    public float frameRate = 30f;
    public float length;
    public int totalFrames;
    public List<BakedFrameData> frames = new List<BakedFrameData>();
}

[Serializable]
public class BakedSlotData
{
    public string slotKey;
    public string slotName;
    public string slotPath; // ??????? transform ??·????/ ?????
    public int drawOrder;   // ?決?????????????? prefab ?????
    public int internalOrder; // ?????????????????????sortingOrder * InternalOrderStep + drawOrder
}

[Serializable]
public class BakedFrameData
{
    public List<Vector3> positions = new List<Vector3>();
    public List<Quaternion> rotations = new List<Quaternion>();
    public List<Vector3> scales = new List<Vector3>();
    public List<Color> colors = new List<Color>(); // 每帧每个 slot 的颜色
}
