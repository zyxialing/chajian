using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GpuRoleTest_RenderAll : MonoBehaviour
{
    public GpuRoleExportData exportData;
    public Shader shader;
    public Camera targetCamera;

    [Header("Sprite PPU")]
    public float ppu = 32f;

    [Header("运行时测试：图集放大倍数 1/2/4")]
    public int atlasScale = 4;

    [Header("放大后是否用 Bilinear，旋转测试建议 true")]
    public bool useBilinearAfterScale = true;

    [Header("调试模式：按 Grid 排列所有 Sprite")]
    public bool debugGridMode = true;

    [Header("Grid 间隔")]
    public float gridSpacing = 2f;

    [Header("是否只测试 6 个 Slot")]
    public bool onlyTestSlots = true;

    public int[] testSlotIndices = { 1, 5, 15, 20, 25, 27 };

    [Header("整体偏移")]
    public Vector3 rootOffset = Vector3.zero;

    private Mesh _quadMesh;
    private Material _material;

    private readonly Dictionary<Texture2D, Texture2D> _scaledTextureCache = new Dictionary<Texture2D, Texture2D>();

    private struct RenderItem
    {
        public int internalOrder;
        public Matrix4x4 matrix;
        public MaterialPropertyBlock mpb;
    }

    private readonly List<RenderItem> _items = new List<RenderItem>();

    private void Start()
    {
        if (exportData == null || shader == null || targetCamera == null)
        {
            Debug.LogError("[GpuRoleTest] 缺少引用");
            return;
        }

        if (atlasScale < 1)
            atlasScale = 1;

        _quadMesh = CreateUnitQuad();

        _material = new Material(shader);
        _material.enableInstancing = true;

        Build();
    }

    private void Build()
    {
        _items.Clear();

        if (debugGridMode)
        {
            List<SpriteUVData> gridUVs = new List<SpriteUVData>();

            for (int i = 0; i < exportData.slots.Count; i++)
            {
                if (!IsTestSlot(i))
                    continue;

                var slot = exportData.slots[i];

                int spriteId = slot.defaultSpriteId;

                if (spriteId < 0 && slot.availableSpriteIds != null && slot.availableSpriteIds.Length > 0)
                    spriteId = slot.availableSpriteIds[0];

                if (spriteId < 0)
                    continue;

                var uv = FindUV(spriteId);

                if (uv != null)
                    gridUVs.Add(uv);
            }

            int cols = Mathf.CeilToInt(Mathf.Sqrt(gridUVs.Count));

            for (int i = 0; i < gridUVs.Count; i++)
            {
                var uv = gridUVs[i];

                int col = i % cols;
                int row = i / cols;

                Vector3 gridPos = new Vector3(col * gridSpacing, -row * gridSpacing, 0f);

                var atlas = exportData.atlases[uv.atlasIndex];

                if (atlas == null || atlas.texture == null)
                    continue;

                Texture2D tex = GetTestTexture(atlas.texture);

                float worldW = uv.cropW / ppu;
                float worldH = uv.cropH / ppu;

                Matrix4x4 m = Matrix4x4.TRS(
                    gridPos,
                    Quaternion.Euler(0f, 0f, 25f), // 专门测试旋转
                    new Vector3(worldW, worldH, 1f)
                );

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mpb.SetTexture("_MainTex", tex);
                mpb.SetVector("_UVRect", new Vector4(uv.uMin, uv.vMin, uv.uMax, uv.vMax));
                mpb.SetVector("_Size", new Vector4(uv.cropW, uv.cropH, 0f, 0f));
                mpb.SetVector("_CropOffset", Vector4.zero);

                _items.Add(new RenderItem
                {
                    internalOrder = i,
                    matrix = m,
                    mpb = mpb
                });
            }

            Debug.Log($"[GpuRoleTest] Grid 模式 count={_items.Count}, atlasScale={atlasScale}");
            return;
        }

        for (int i = 0; i < exportData.slots.Count; i++)
        {
            if (onlyTestSlots && !IsTestSlot(i))
                continue;

            BuildSlot(i);
        }

        _items.Sort((a, b) => a.internalOrder.CompareTo(b.internalOrder));

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            item.matrix.m23 = -i * 0.001f;
            _items[i] = item;
        }

        Debug.Log($"[GpuRoleTest] 拼凑完成 count={_items.Count}, atlasScale={atlasScale}");
    }

    private void BuildSlot(int slotIndex)
    {
        var slot = exportData.slots[slotIndex];

        int spriteId = slot.defaultSpriteId;

        if (spriteId < 0 && slot.availableSpriteIds != null && slot.availableSpriteIds.Length > 0)
            spriteId = slot.availableSpriteIds[0];

        if (spriteId < 0)
            return;

        SpriteUVData uv = FindUV(spriteId);

        if (uv == null)
        {
            Debug.LogWarning($"[GpuRoleTest] 找不到 UV slot={slotIndex}, spriteId={spriteId}");
            return;
        }

        var atlas = exportData.atlases[uv.atlasIndex];

        if (atlas == null || atlas.texture == null)
            return;

        Texture2D tex = GetTestTexture(atlas.texture);

        float worldW = uv.cropW / ppu;
        float worldH = uv.cropH / ppu;

        Vector3 pivotOffset = new Vector3(
            -worldW * uv.pivotX,
            -worldH * uv.pivotY,
            0f
        );

        Matrix4x4 rootMatrix = Matrix4x4.TRS(
            rootOffset,
            Quaternion.identity,
            Vector3.one
        );

        Matrix4x4 slotMatrix = Matrix4x4.TRS(
            slot.localPosition,
            Quaternion.Euler(slot.localEulerAngles),
            slot.localScale
        );

        Matrix4x4 spriteMatrix = Matrix4x4.TRS(
            pivotOffset,
            Quaternion.identity,
            new Vector3(worldW, worldH, 1f)
        );

        Matrix4x4 finalMatrix = rootMatrix * slotMatrix * spriteMatrix;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetTexture("_MainTex", tex);
        mpb.SetVector("_UVRect", new Vector4(uv.uMin, uv.vMin, uv.uMax, uv.vMax));
        mpb.SetVector("_Size", new Vector4(uv.cropW, uv.cropH, 0f, 0f));
        mpb.SetVector("_CropOffset", Vector4.zero);

        _items.Add(new RenderItem
        {
            internalOrder = slot.internalOrder,
            matrix = finalMatrix,
            mpb = mpb
        });

        Debug.Log(
            $"[GpuRoleTest] Slot[{slotIndex}] {slot.slotName}" +
            $" spriteId={spriteId}" +
            $" order={slot.internalOrder}" +
            $" pos={slot.localPosition}" +
            $" rot={slot.localEulerAngles}" +
            $" crop=({uv.cropW},{uv.cropH})" +
            $" pivot=({uv.pivotX},{uv.pivotY})"
        );
    }

    private Texture2D GetTestTexture(Texture2D source)
    {
        if (source == null)
            return null;

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

        Debug.Log($"[GpuRoleTest] 创建放大图集 {source.name}: {source.width}x{source.height} -> {scaled.width}x{scaled.height}");

        return scaled;
    }

    private Texture2D CreateNearestScaledTexture(Texture2D src, int scale)
    {
        Texture2D dst = new Texture2D(
            src.width * scale,
            src.height * scale,
            TextureFormat.RGBA32,
            false
        );

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

    private void LateUpdate()
    {
        if (_quadMesh == null || _material == null || targetCamera == null)
            return;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];

            Graphics.DrawMesh(
                _quadMesh,
                item.matrix,
                _material,
                gameObject.layer,
                targetCamera,
                0,
                item.mpb,
                ShadowCastingMode.Off,
                false
            );
        }
    }

    private bool IsTestSlot(int index)
    {
        if (testSlotIndices == null)
            return false;

        for (int i = 0; i < testSlotIndices.Length; i++)
        {
            if (testSlotIndices[i] == index)
                return true;
        }

        return false;
    }

    private SpriteUVData FindUV(int spriteId)
    {
        if (exportData.spriteUVs == null)
            return null;

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
        mesh.name = "GpuRoleTest_UnitQuad";

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

        mesh.triangles = new int[]
        {
            0, 1, 2,
            2, 1, 3,
        };

        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (_quadMesh != null)
            DestroyImmediate(_quadMesh);

        if (_material != null)
            DestroyImmediate(_material);

        foreach (var kv in _scaledTextureCache)
        {
            if (kv.Value != null)
                DestroyImmediate(kv.Value);
        }

        _scaledTextureCache.Clear();
    }
}