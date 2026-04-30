using System.Collections.Generic;
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
    private bool _dirty;

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
        // 根节点保持单位变换，所有子节点用 bindPoseToRoot 计算绝对位置
        _rootObject.transform.localPosition = Vector3.zero;
        _rootObject.transform.localRotation = Quaternion.identity;
        _rootObject.transform.localScale = Vector3.one;

        // 为每个 slot 创建 SpriteRenderer
        int count = Mathf.Min(slotDefs.Count, styleSlots.Count);
        for (int i = 0; i < count; i++)
        {
            var slot = slotDefs[i];
            var style = styleSlots[i];

            // 用 bindPoseToRoot 矩阵计算相对于根节点的位置和旋转
            // bindPoseToRoot = root.worldToLocalMatrix * transform.localToWorldMatrix
            // MultiplyPoint(Vector3.zero) 得到该节点在 root 空间下的位置
            Vector3 pos = slot.bindPoseToRoot.MultiplyPoint(Vector3.zero);
            // 用矩阵变换方向向量来获取在 root 空间下的旋转
            // 取局部坐标轴方向，用 bindPoseToRoot 变换到 root 空间
            Vector3 fwd = slot.bindPoseToRoot.MultiplyVector(Vector3.forward);
            Vector3 up = slot.bindPoseToRoot.MultiplyVector(Vector3.up);
            Quaternion rot = Quaternion.LookRotation(fwd, up);

            GameObject go = new GameObject(slot.slotName);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(_rootObject.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = slot.localScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = slot.sortingLayerId;
            sr.sortingOrder = slot.sortingOrder;

            // 节点是否在预制体中默认可见
            bool defaultVisible = slot.activeInHierarchy && slot.rendererEnabled;
            // VisibleInsideMask 的头发由头盔 sprite 裁切，预览中不显示
            bool skipMasked = slot.maskInteraction == SpriteMaskInteraction.VisibleInsideMask;

            // 应用样式：默认隐藏的节点即使有 sprite 也不显示
            if (style.sprite != null && defaultVisible && !skipMasked)
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
        }

        _previewUtil.AddSingleGO(_rootObject);
        _dirty = false;
    }

    /// <summary>
    /// 更新所有 slot 的样式（不重建 GameObject）
    /// </summary>
    public void ApplyStyle(List<GpuRoleSlot> slotDefs, List<GpuRoleStyleSlot> styleSlots)
    {
        ApplyStyle(slotDefs, styleSlots, -1);
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

            // 节点是否在预制体中默认可见
            bool defaultVisible = slot.activeInHierarchy && slot.rendererEnabled;
            // VisibleInsideMask 的头发由头盔 sprite 裁切，预览中不显示
            bool skipMasked = slot.maskInteraction == SpriteMaskInteraction.VisibleInsideMask;
            bool canShow = defaultVisible && !skipMasked;

            // groupId 过滤：指定组时只显示该组
            if (groupId >= 0)
            {
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
        _dirty = false;
    }

    /// <summary>
    /// 渲染预览纹理
    /// </summary>
    public Texture Render(Rect rect, ref Vector2 drag)
    {
        if (!IsValid) return null;

        Bounds bounds = CalculateBounds();
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
