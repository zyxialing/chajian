using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 纯数据驱动的角色预览渲染器（不依赖 Prefab 实例）
/// 根据 slotDefs + styleSlots 直接用 SpriteRenderer 拼出预览
/// </summary>
public class GpuRolePreviewRenderer_Main
{
    private PreviewRenderUtility _previewUtil;
    private GameObject _rootObject;
    private List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
    // slotKey → SpriteRenderer 映射，用于 slotKey 安全匹配
    private Dictionary<string, SpriteRenderer> _rendererBySlotKey = new Dictionary<string, SpriteRenderer>();

        // 记录初始 bounds，动画播放时相机不跟着动
    private Bounds _initialBounds;
    private bool _hasInitialBounds;

    public bool IsValid => _previewUtil != null && _rootObject != null;

    /// <summary>
    /// 构建预览场景
    /// </summary>
    public void Build(List<GpuRoleSlot> slotDefs, List<GpuRoleStyleSlot> styleSlots,
        Vector3 rootPos = default, Quaternion rootRot = default, Vector3 rootScale = default)
    {
        Cleanup();
        if (slotDefs == null || styleSlots == null || slotDefs.Count == 0) return;

        if (rootScale == default) rootScale = Vector3.one;
        if (rootRot == default) rootRot = Quaternion.identity;

        _previewUtil = new PreviewRenderUtility();
        SetupCamera(_previewUtil);

        _rootObject = new GameObject("Preview_Root");
        _rootObject.hideFlags = HideFlags.HideAndDontSave;
        _rootObject.transform.localPosition = Vector3.zero;
        _rootObject.transform.localRotation = Quaternion.identity;
        _rootObject.transform.localScale = Vector3.one;

        // 为每个 slot 创建 SpriteRenderer
        int count = Mathf.Min(slotDefs.Count, styleSlots.Count);
        for (int i = 0; i < count; i++)
        {
            var slot = slotDefs[i];
            var style = styleSlots[i];

            // 从 bindPoseToRoot 矩阵分解位置、旋转、缩放
            // bindPoseToRoot = root.worldToLocalMatrix * transform.localToWorldMatrix
            Vector3 pos;
            Quaternion rot;
            Vector3 scale;
            DecomposeMatrix(slot.bindPoseToRoot, out pos, out rot, out scale);

            GameObject go = new GameObject(slot.slotName);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(_rootObject.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;

                        var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = slot.sortingLayerId;
            // 直接使用预先计算的 internalOrder（数据阶段已算好：sortingOrder * InternalOrderStep + drawOrder）
            // roleBaseOrder 用于角色整体世界排序，暂时为 0
            sr.sortingOrder = slot.internalOrder;
            Debug.Log(style.slotKey+":"+sr.sortingOrder);
            // 应用样式
            bool rendererEnabled = slot.rendererEnabled;
            if (style.sprite != null && rendererEnabled)
            {
                sr.sprite = style.sprite;
                sr.color = style.color;
                sr.enabled = true;
            }
            else
            {
                sr.sprite = null;
                sr.enabled = false;
            }

            _renderers.Add(sr);
            // 按 slotKey 索引，用于 slotKey 安全匹配
            if (!string.IsNullOrEmpty(slot.slotKey) && !_rendererBySlotKey.ContainsKey(slot.slotKey))
                _rendererBySlotKey[slot.slotKey] = sr;
        }

        _previewUtil.AddSingleGO(_rootObject);

        // 记录初始 bounds（动画播放时相机不跟着动）
        _initialBounds = CalculateBounds();
        _hasInitialBounds = true;
    }

    /// <summary>
    /// 更新所有 slot 的样式（不重建 GameObject）
    /// </summary>
    public void ApplyStyle(List<GpuRoleSlot> slotDefs, List<GpuRoleStyleSlot> styleSlots)
    {
        ApplyStyle(slotDefs, styleSlots, -1);
    }

        /// <summary>
    /// 应用动画帧数据到所有 SpriteRenderer 的变换（基于 slotKey 安全匹配）
    /// </summary>
    public void ApplyAnimationFrame(BakedFrameData frameData, List<BakedSlotData> slotKeys)
    {
        if (!IsValid || frameData == null || slotKeys == null) return;

        int count = Mathf.Min(slotKeys.Count, frameData.positions.Count, frameData.rotations.Count, frameData.scales.Count);

        for (int i = 0; i < count; i++)
        {
            string key = slotKeys[i].slotKey;
            if (string.IsNullOrEmpty(key)) continue;

            if (_rendererBySlotKey.TryGetValue(key, out var sr))
            {
                sr.transform.localPosition = frameData.positions[i];
                sr.transform.localRotation = frameData.rotations[i];
                sr.transform.localScale = frameData.scales[i];
            }
        }
    }

    /// <summary>
    /// 按 BakeData.slotKeys 的顺序重新排列 _renderers 列表，确保绘制顺序与烘焙数据一致
    /// </summary>
    public void ReorderBySlotKeys(List<BakedSlotData> slotKeys)
    {
        if (!IsValid || slotKeys == null) return;

        var newOrder = new List<SpriteRenderer>();
        foreach (var slot in slotKeys)
        {
            if (!string.IsNullOrEmpty(slot.slotKey) && _rendererBySlotKey.TryGetValue(slot.slotKey, out var sr))
            {
                newOrder.Add(sr);
            }
        }
        // 补上 slotKeys 中没有的 renderer（保持原顺序）
        foreach (var sr in _renderers)
        {
            if (!newOrder.Contains(sr))
                newOrder.Add(sr);
        }
        _renderers = newOrder;
    }

    /// <summary>
    /// 更新样式，如果 groupId >= 0 则只启用该组的 renderer，禁用其他
    /// </summary>
    public void ApplyStyle(List<GpuRoleSlot> slotDefs, List<GpuRoleStyleSlot> styleSlots, int groupId)
    {
        if (!IsValid) return;

        int count = Mathf.Min(_renderers.Count, slotDefs.Count, styleSlots.Count);
        for (int i = 0; i < count; i++)
        {
            var sr = _renderers[i];
            var slot = slotDefs[i];
            var style = styleSlots[i];

            bool rendererEnabled = slot.rendererEnabled;
            bool canShow = rendererEnabled;

            if (groupId >= 0)
            {
                // 组预览模式：只显示该组的部件
                if (style.linkedGroupId == groupId)
                {
                    if (style.sprite != null && canShow)
                    {
                        sr.sprite = style.sprite;
                        sr.color = style.color;
                        sr.enabled = true;
                    }
                    else
                    {
                        sr.sprite = null;
                        sr.enabled = false;
                    }
                }
                else
                {
                    sr.enabled = false;
                }
            }
            else
            {
                if (style.sprite != null && canShow)
                {
                    sr.sprite = style.sprite;
                    sr.color = style.color;
                    sr.enabled = true;
                }
                else
                {
                    sr.sprite = null;
                    sr.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// 渲染预览纹理
    /// </summary>
    public Texture Render(Rect rect, ref Vector2 drag)
    {
        if (!IsValid) return null;

        // 使用初始 bounds，动画播放时相机不跟着角色位移跑
        Bounds bounds = _hasInitialBounds ? _initialBounds : CalculateBounds();
        float aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(1f, rect.height));
        float size = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect, 0.5f);
        Vector3 center = bounds.center;

        _previewUtil.BeginPreview(rect, GUIStyle.none);
        _previewUtil.camera.orthographicSize = size * 1.25f;
        _previewUtil.camera.transform.position = center + new Vector3(drag.x, drag.y, -10f);
        _previewUtil.camera.transform.rotation = Quaternion.identity;
        _previewUtil.camera.nearClipPlane = 0.01f;
        _previewUtil.camera.farClipPlane = 100f;
        _previewUtil.Render(true, false);
        return _previewUtil.EndPreview();
    }

    public void Cleanup()
    {
        _renderers.Clear();
        _rendererBySlotKey.Clear();
        _hasInitialBounds = false;
        if (_rootObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
            _rootObject = null;
        }
        if (_previewUtil != null)
        {
            _previewUtil.Cleanup();
            _previewUtil = null;
        }
    }

        private void DecomposeMatrix(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        GpuRoleUtility.DecomposeMatrix(matrix, out position, out rotation, out scale);
    }

    private Bounds CalculateBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        foreach (var r in _renderers)
        {
            if (r == null || r.sprite == null || !r.enabled) continue;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
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
