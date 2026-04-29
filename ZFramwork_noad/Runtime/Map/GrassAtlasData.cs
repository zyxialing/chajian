using UnityEngine;

[CreateAssetMenu(fileName = "GrassAtlasData", menuName = "Grass/AtlasData")]
public class GrassAtlasData : ScriptableObject
{
    public Vector4[] uvs;   // xy = 起点，zw = size
    public Vector2[] sizes; // 像素尺寸
}