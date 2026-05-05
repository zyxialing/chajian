using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GpuRoleAgent 编辑器预览渲染器
/// 直接用图集纹理 + UV 渲染，和运行时一致，不依赖 Sprite 加载
/// 从动画第一帧读取 slot 颜色
/// </summary>
public class GpuRoleAgentPreview
{
    private PreviewRenderUtility _previewUtil;
    private GameObject _rootObject;
    private List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private List<MeshFilter> _meshFilters = new List<MeshFilter>();
    private List<Material> _materials = new List<Material>();
    private Dictionary<string, MeshRenderer> _rendererBySlotKey = new Dictionary<string, MeshRenderer>();
    private Bounds _initialBounds;
    private bool _hasInitialBounds;

    // 缓存 slot 颜色（从动画第一帧读取）
    private Color[] _frameColors;
    private List<BakedSlotData> _slotKeys;

    public bool IsValid => _previewUtil != null && _rootObject != null;

    /// <summary>
    /// 构建预览
    /// </summary>
    public void Build(GpuRoleExportData exportData, int[] spriteIds, bool[] visible, Color color, float scale)
    {
        Cleanup();
        if (exportData == null || exportData.slots == null) return;

        _previewUtil = new PreviewRenderUtility();
        SetupCamera(_previewUtil);

        _rootObject = new GameObject("Preview_Root");
        _rootObject.hideFlags = HideFlags.HideAndDontSave;
        _rootObject.transform.localPosition = Vector3.zero;
        _rootObject.transform.localRotation = Quaternion.identity;
        _rootObject.transform.localScale = Vector3.one * scale;

        // 从动画第一帧读取 slot 颜色
        ReadFirstFrameColors(exportData);

        for (int i = 0; i < exportData.slots.Count; i++)
        {
            var slot = exportData.slots[i];

            GameObject go = new GameObject(slot.slotName);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(_rootObject.transform, false);
            go.transform.localPosition = slot.localPosition + new Vector3(0f, 0f, -slot.internalOrder * 0.001f);
            go.transform.localRotation = Quaternion.Euler(slot.localEulerAngles);
            go.transform.localScale = slot.localScale;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            bool isVisible = i < visible.Length && visible[i];
            int spriteId = i < spriteIds.Length ? spriteIds[i] : -1;

            if (isVisible && spriteId >= 0)
            {
                Color slotColor = GetSlotColor(i, exportData, color);
                SetPreviewSprite(exportData, mf, mr, spriteId, slotColor);
            }
            else
            {
                mr.enabled = false;
            }

            _meshFilters.Add(mf);
            _renderers.Add(mr);
            if (!string.IsNullOrEmpty(slot.slotKey) && !_rendererBySlotKey.ContainsKey(slot.slotKey))
                _rendererBySlotKey[slot.slotKey] = mr;
        }

        _previewUtil.AddSingleGO(_rootObject);

        _initialBounds = CalculateBounds();
        _hasInitialBounds = true;
    }

    /// <summary>
    /// 更新预览（不重建 GameObject）
    /// </summary>
    public void UpdatePreview(GpuRoleExportData exportData, int[] spriteIds, bool[] visible, Color color, float scale)
    {
        if (!IsValid) return;

        _rootObject.transform.localScale = Vector3.one * scale;

        // 从动画第一帧读取 slot 颜色
        ReadFirstFrameColors(exportData);

        for (int i = 0; i < _renderers.Count && i < exportData.slots.Count; i++)
        {
            var mr = _renderers[i];
            var mf = _meshFilters[i];

            bool isVisible = i < visible.Length && visible[i];
            int spriteId = i < spriteIds.Length ? spriteIds[i] : -1;

            if (isVisible && spriteId >= 0)
            {
                Color slotColor = GetSlotColor(i, exportData, color);
                SetPreviewSprite(exportData, mf, mr, spriteId, slotColor);
            }
            else
            {
                mr.enabled = false;
            }
        }

        _initialBounds = CalculateBounds();
    }

    /// <summary>
    /// 从动画第一帧读取 slot 颜色
    /// </summary>
    private void ReadFirstFrameColors(GpuRoleExportData exportData)
    {
        _frameColors = null;
        _slotKeys = null;

        if (exportData == null || exportData.animations == null || exportData.animations.Count == 0)
            return;

        var anim = exportData.animations[0];
        if (anim.frames == null || anim.frames.Count == 0)
            return;

        var frame = anim.frames[0];
        if (frame.colors == null || frame.colors.Count == 0)
            return;

        _frameColors = frame.colors.ToArray();
        _slotKeys = anim.slotKeys;
    }

    /// <summary>
    /// 获取 slot 颜色（从动画第一帧读取），乘以整体颜色
    /// </summary>
    private Color GetSlotColor(int slotIndex, GpuRoleExportData exportData, Color baseColor)
    {
        if (_frameColors == null || _slotKeys == null)
            return baseColor;

        if (slotIndex < 0 || slotIndex >= exportData.slots.Count)
            return baseColor;

        string slotKey = exportData.slots[slotIndex].slotKey;
        for (int ci = 0; ci < _slotKeys.Count; ci++)
        {
            if (_slotKeys[ci].slotKey == slotKey && ci < _frameColors.Length)
                return baseColor * _frameColors[ci];
        }

        return baseColor;
    }

    /// <summary>
    /// 渲染预览纹理
    /// </summary>
    public Texture Render(Rect rect, ref Vector2 drag, float zoom = 1f)
    {
        if (!IsValid) return null;

        Bounds bounds = _hasInitialBounds ? _initialBounds : CalculateBounds();
        float aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(1f, rect.height));
        float size = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect, 0.5f);
        Vector3 center = bounds.center;

        _previewUtil.BeginPreview(rect, GUIStyle.none);
        _previewUtil.camera.orthographicSize = size * 1.25f / zoom;
        _previewUtil.camera.transform.position = center + new Vector3(drag.x, drag.y, -10f);
        _previewUtil.camera.transform.rotation = Quaternion.identity;
        _previewUtil.camera.nearClipPlane = 0.01f;
        _previewUtil.camera.farClipPlane = 100f;
        _previewUtil.Render(true, false);
        return _previewUtil.EndPreview();
    }

    public void Cleanup()
    {
        // 释放材质
        foreach (var mat in _materials)
        {
            if (mat != null)
                Object.DestroyImmediate(mat);
        }
        _materials.Clear();
        _meshFilters.Clear();
        _renderers.Clear();
        _rendererBySlotKey.Clear();
        _hasInitialBounds = false;

        _frameColors = null;
        _slotKeys = null;

        if (_rootObject != null)
        {
            Object.DestroyImmediate(_rootObject);
            _rootObject = null;
        }
        if (_previewUtil != null)
        {
            _previewUtil.Cleanup();
            _previewUtil = null;
        }
    }

    /// <summary>
    /// 根据 spriteId 设置 slot 的 mesh 和材质
    /// </summary>
    private bool SetPreviewSprite(GpuRoleExportData exportData, MeshFilter mf, MeshRenderer mr, int spriteId, Color color)
    {
        var uv = exportData.spriteUVs.Find(u => u.spriteId == spriteId);
        if (uv == null)
        {
            mr.enabled = false;
            return false;
        }

        if (uv.atlasIndex < 0 || uv.atlasIndex >= exportData.atlases.Count)
        {
            mr.enabled = false;
            return false;
        }

        var atlas = exportData.atlases[uv.atlasIndex];
        if (atlas == null || atlas.texture == null)
        {
            mr.enabled = false;
            return false;
        }

        mf.sharedMesh = CreatePreviewMesh(uv);

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = atlas.texture;
        mat.color = color;
        mat.hideFlags = HideFlags.HideAndDontSave;

        mr.sharedMaterial = mat;
        mr.enabled = true;

        _materials.Add(mat);

        return true;
    }

    /// <summary>
    /// 根据 UV 数据创建 quad mesh
    /// </summary>
    private Mesh CreatePreviewMesh(SpriteUVData uv)
    {
        Mesh mesh = new Mesh();
        mesh.name = $"PreviewMesh_{uv.spriteId}_{uv.spriteName}";

        // 世界大小：和运行时一致，cropW/32 x cropH/32
        float worldW = uv.cropW / 32f;
        float worldH = uv.cropH / 32f;

        Vector3 pivotOffset = new Vector3(
            -worldW * uv.pivotX,
            -worldH * uv.pivotY,
            0f
        );

        Vector3[] vertices =
        {
            pivotOffset,
            pivotOffset + new Vector3(worldW, 0f, 0f),
            pivotOffset + new Vector3(0f, worldH, 0f),
            pivotOffset + new Vector3(worldW, worldH, 0f)
        };

        Vector2[] uvs =
        {
            new Vector2(uv.uMin, uv.vMin),
            new Vector2(uv.uMax, uv.vMin),
            new Vector2(uv.uMin, uv.vMax),
            new Vector2(uv.uMax, uv.vMax)
        };

        int[] tris = { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        return mesh;
    }

    private Bounds CalculateBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        foreach (var mr in _renderers)
        {
            if (mr == null || !mr.enabled) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            if (!hasBounds) { bounds = mr.bounds; hasBounds = true; }
            else bounds.Encapsulate(mr.bounds);
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one * 2f);
    }

    private void SetupCamera(PreviewRenderUtility util)
    {
        util.camera.orthographic = true;
        util.camera.clearFlags = CameraClearFlags.Color;
        util.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        util.lights[0].intensity = 1f;
        util.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
        util.lights[1].intensity = 0.5f;
    }
}
