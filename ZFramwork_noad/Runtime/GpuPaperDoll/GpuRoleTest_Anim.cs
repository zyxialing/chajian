using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 最简单的动画测试脚本
/// 播放导出数据中的第一个动画
/// 支持通过 Config 配置角色外观
/// </summary>
public class GpuRoleTest_Anim : MonoBehaviour
{
    public GpuRoleExportData exportData;
    public Shader shader;
    public Camera targetCamera;

    [Header("Sprite PPU")]
    public float ppu = 32f;

    [Header("运行时测试：图集放大倍数 1/2/4")]
    public int atlasScale = 4;

    [Header("放大后是否用 Bilinear")]
    public bool useBilinearAfterScale = true;

    [Header("角色配置（运行时动态切换）")]
    public int[] slotSpriteIndices; // 按 slot 索引选 Sprite，-1 用默认

    [Header("动画")]
    public int animIndex = 0;
    public float playbackSpeed = 1f;

    [Header("整体偏移")]
    public Vector3 rootOffset = Vector3.zero;

    private Material _material;
    private Dictionary<Texture2D, Texture2D> _scaledTextureCache = new Dictionary<Texture2D, Texture2D>();
    private Dictionary<int, Mesh> _spriteMeshCache = new Dictionary<int, Mesh>(); // spriteId → Tight Mesh

    private AnimExportData _anim;
    private int _currentFrame;
    private float _timer;

    // SlotKey → Slot 索引的映射
    private Dictionary<string, int> _slotIndexByKey = new Dictionary<string, int>();

    // 每个 Slot 的渲染数据
    private struct SlotRenderState
    {
        public int internalOrder;
        public int spriteId;
        public Matrix4x4 matrix;
        public MaterialPropertyBlock mpb;
        public bool visible;
    }

    private SlotRenderState[] _slots;

    private void Start()
    {
        if (exportData == null || shader == null || targetCamera == null)
        {
            Debug.LogError("[AnimTest] 缺少引用");
            return;
        }

        if (exportData.animations == null || exportData.animations.Count == 0)
        {
            Debug.LogError("[AnimTest] 没有动画数据");
            return;
        }

        if (animIndex < 0 || animIndex >= exportData.animations.Count)
        {
            Debug.LogError($"[AnimTest] 动画索引 {animIndex} 超出范围 (0-{exportData.animations.Count - 1})");
            return;
        }

        if (atlasScale < 1) atlasScale = 1;

        _anim = exportData.animations[animIndex];
        if (_anim.frames == null || _anim.frames.Count == 0)
        {
            Debug.LogError("[AnimTest] 动画没有帧数据");
            return;
        }

        _material = new Material(shader);
        _material.enableInstancing = true;

        // 建立 SlotKey → 索引映射
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            _slotIndexByKey[exportData.slots[i].slotKey] = i;
        }

        // 初始化 Slot 渲染状态
        _slots = new SlotRenderState[exportData.slots.Count];

        // 应用第 0 帧
        ApplyFrame(0);

        Debug.Log($"[AnimTest] 动画: {_anim.animName} 总帧数: {_anim.totalFrames} 帧率: {_anim.frameRate}");
    }

    private void Update()
    {
        if (_anim == null || _anim.frames == null || _anim.frames.Count == 0) return;

        _timer += Time.deltaTime * playbackSpeed;
        float frameDuration = 1f / _anim.frameRate;

        if (_timer >= frameDuration)
        {
            _timer -= frameDuration;
            _currentFrame++;

            if (_currentFrame >= _anim.frames.Count)
                _currentFrame = 0; // 循环

            ApplyFrame(_currentFrame);
        }
    }

    private void ApplyFrame(int frameIndex)
    {
        var frame = _anim.frames[frameIndex];

        for (int i = 0; i < _anim.slotKeys.Count; i++)
        {
            var slotKey = _anim.slotKeys[i];

            if (!_slotIndexByKey.TryGetValue(slotKey.slotKey, out int slotIdx))
                continue;

            if (slotIdx < 0 || slotIdx >= exportData.slots.Count)
                continue;

            var slotData = exportData.slots[slotIdx];

            // 取该帧的位置/旋转/缩放/颜色
            Vector3 pos = frame.positions[i];
            Quaternion rot = frame.rotations[i];
            Vector3 scale = frame.scales[i];
            Color slotColor = i < frame.colors.Count ? frame.colors[i] : Color.white;

            // 根据 Config 选择 Sprite
            int spriteId = GetSpriteIdFromConfig(slotData, slotIdx);

            if (spriteId < 0)
            {
                _slots[slotIdx].visible = false;
                continue;
            }

            if (spriteId == -2)
            {
                _slots[slotIdx].visible = false;
                continue;
            }

            SpriteUVData uv = FindUV(spriteId);
            if (uv == null)
            {
                _slots[slotIdx].visible = false;
                continue;
            }

            var atlas = exportData.atlases[uv.atlasIndex];
            if (atlas == null || atlas.texture == null)
            {
                _slots[slotIdx].visible = false;
                continue;
            }

            Texture2D tex = GetTestTexture(atlas.texture);

            float worldW = uv.cropW / ppu;
            float worldH = uv.cropH / ppu;

            Vector3 pivotOffset = new Vector3(
                -worldW * uv.pivotX,
                -worldH * uv.pivotY,
                0f
            );

            Matrix4x4 rootMatrix = Matrix4x4.TRS(rootOffset, Quaternion.identity, Vector3.one);
            Matrix4x4 slotMatrix = Matrix4x4.TRS(pos, rot, scale);
            Matrix4x4 spriteMatrix = Matrix4x4.TRS(pivotOffset, Quaternion.identity, new Vector3(worldW, worldH, 1f));
            Matrix4x4 finalMatrix = rootMatrix * slotMatrix * spriteMatrix;

            // Z 偏移排序
            finalMatrix.m23 = -slotData.internalOrder * 0.001f;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_MainTex", tex);
            mpb.SetVector("_UVRect", new Vector4(uv.uMin, uv.vMin, uv.uMax, uv.vMax));
            mpb.SetVector("_Size", new Vector4(uv.cropW, uv.cropH, 0f, 0f));
            mpb.SetVector("_CropOffset", Vector4.zero);
            mpb.SetColor("_Color", slotColor);

            _slots[slotIdx] = new SlotRenderState
            {
                internalOrder = slotData.internalOrder,
                spriteId = spriteId,
                matrix = finalMatrix,
                mpb = mpb,
                visible = true
            };
        }
    }

    private void LateUpdate()
    {
        if (_material == null || targetCamera == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].visible) continue;

            Mesh mesh = GetOrCreateSpriteMesh(_slots[i].spriteId);
            if (mesh == null) continue;

            Graphics.DrawMesh(
                mesh,
                _slots[i].matrix,
                _material,
                gameObject.layer,
                targetCamera,
                0,
                _slots[i].mpb,
                ShadowCastingMode.Off,
                false
            );
        }
    }

    private Mesh GetOrCreateSpriteMesh(int spriteId)
    {
        if (spriteId < 0) return null;

        if (_spriteMeshCache.TryGetValue(spriteId, out Mesh cached))
            return cached;

        // 查找 UV 数据
        SpriteUVData uv = FindUV(spriteId);
        if (uv == null) return null;

        Mesh mesh = new Mesh();
        mesh.name = $"SpriteMesh_{spriteId}";

        if (uv.meshVertices != null && uv.meshVertices.Length > 0 && uv.meshTriangles != null && uv.meshTriangles.Length > 0)
        {
            // 使用 Tight Mesh
            // 顶点需要转换到裁剪后图片的归一化坐标（0-1 范围），因为 Quad 是 0-1 的
            Vector3[] verts = new Vector3[uv.meshVertices.Length];
            for (int i = 0; i < uv.meshVertices.Length; i++)
            {
                verts[i] = new Vector3(
                    uv.meshVertices[i].x / uv.cropW,
                    uv.meshVertices[i].y / uv.cropH,
                    0
                );
            }

            mesh.vertices = verts;
            mesh.uv = uv.meshUVs;
            mesh.triangles = System.Array.ConvertAll(uv.meshTriangles, t => (int)t);
        }
        else
        {
            // 没有 Tight Mesh，退回 FullRect Quad
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
        }

        mesh.RecalculateBounds();
        _spriteMeshCache[spriteId] = mesh;
        return mesh;
    }

    /// <summary>
    /// 根据 slotSpriteIndices 数组获取 Slot 应该使用的 SpriteId
    /// </summary>
    private int GetSpriteIdFromConfig(SlotExportData slotData, int slotIndex)
    {
        // 默认用 defaultSpriteId
        int defaultId = slotData.defaultSpriteId;
        if (defaultId < 0 && slotData.availableSpriteIds != null && slotData.availableSpriteIds.Length > 0)
            defaultId = slotData.availableSpriteIds[0];

        // 如果 slotSpriteIndices 数组中有配置，用配置的索引选 Sprite
        if (slotSpriteIndices != null && slotIndex < slotSpriteIndices.Length)
        {
            int idx = slotSpriteIndices[slotIndex];
            if (idx < 0 || idx >= slotData.availableSpriteIds.Length)
                return -2; // 返回 -2 表示不显示
            return slotData.availableSpriteIds[idx];
        }

        return defaultId;
    }

    private Texture2D GetTestTexture(Texture2D source)
    {
        if (source == null) return null;

        if (atlasScale <= 1)
        {
            source.filterMode = useBilinearAfterScale ? FilterMode.Bilinear : FilterMode.Point;
            source.wrapMode = TextureWrapMode.Clamp;
            source.anisoLevel = 0;
            return source;
        }

        if (_scaledTextureCache.TryGetValue(source, out Texture2D cached))
            return cached;

        Texture2D scaled = CreateNearestScaledTexture(source, atlasScale);
        scaled.filterMode = useBilinearAfterScale ? FilterMode.Bilinear : FilterMode.Point;
        scaled.wrapMode = TextureWrapMode.Clamp;
        scaled.anisoLevel = 0;

        _scaledTextureCache[source] = scaled;
        Debug.Log($"[AnimTest] 放大图集 {source.name}: {source.width}x{source.height} -> {scaled.width}x{scaled.height}");
        return scaled;
    }

    private Texture2D CreateNearestScaledTexture(Texture2D src, int scale)
    {
        Texture2D dst = new Texture2D(src.width * scale, src.height * scale, TextureFormat.RGBA32, false);
        Color32[] srcPixels = src.GetPixels32();
        Color32[] dstPixels = new Color32[dst.width * dst.height];

        for (int y = 0; y < dst.height; y++)
        {
            int sy = y / scale;
            for (int x = 0; x < dst.width; x++)
            {
                int sx = x / scale;
                dstPixels[y * dst.width + x] = srcPixels[sy * src.width + sx];
            }
        }

        dst.SetPixels32(dstPixels);
        dst.Apply(false, false);
        return dst;
    }

    private SpriteUVData FindUV(int spriteId)
    {
        if (exportData.spriteUVs == null) return null;
        for (int i = 0; i < exportData.spriteUVs.Count; i++)
        {
            if (exportData.spriteUVs[i].spriteId == spriteId)
                return exportData.spriteUVs[i];
        }
        return null;
    }

    private Mesh CreateUnitQuad()
    {
        Mesh mesh = new Mesh();
        mesh.name = "AnimTest_Quad";

        mesh.vertices = new Vector3[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
        };

        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };

        mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (_material != null) DestroyImmediate(_material);

        foreach (var kv in _scaledTextureCache)
        {
            if (kv.Value != null) DestroyImmediate(kv.Value);
        }
        _scaledTextureCache.Clear();

        foreach (var kv in _spriteMeshCache)
        {
            if (kv.Value != null) DestroyImmediate(kv.Value);
        }
        _spriteMeshCache.Clear();
    }
}
