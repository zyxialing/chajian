using System;
using UnityEngine;

[Serializable]
public class GrassData
{
    public ColorData[] palette;
    public GrassTemplateData[] templates;
}

[Serializable]
public class GrassTemplateData
{
    public PixelData[] pixels;
}

[Serializable]
public struct PixelData
{
    public sbyte x;
    public sbyte y;
    public byte colorIndex;
}

[Serializable]
public struct ColorData
{
    public float r, g, b;
}

public enum GrassDistributionType
{
    Scatter,   // 均匀分散（点状）
    Natural,   // 自然分布（默认）
    Cluster    // 成块分布（你要的）
}