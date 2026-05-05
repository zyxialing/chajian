using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 纯数据驱动的角色预览渲染器
/// 不依赖 Prefab 实例化，根据 slotDefs + styleSlots 直接渲染 Sprite
/// </summary>
public class GpuRolePreviewRenderer
{
    private GpuRolePreviewRenderer_Main _main;
    private Dictionary<int, GpuRolePreviewRenderer_Main> _groupPreviews = new Dictionary<int, GpuRolePreviewRenderer_Main>();
    private Dictionary<int, Vector2> _groupPreviewDrags = new Dictionary<int, Vector2>(); // 修复 Bug 5：每个组独立拖拽

    public bool HasMainPreview => _main != null && _main.IsValid;

    public GpuRolePreviewRenderer()
    {
        _main = new GpuRolePreviewRenderer_Main();
    }

    /// <summary>
    /// 构建/重建主预览
    /// </summary>
    public void BuildMainPreview(List<GpuRoleSlot> slotDefs, List<GpuRoleStyleSlot> styleSlots,
        Vector3 rootPos = default, Quaternion rootRot = default, Vector3 rootScale = default)
    {
        if (rootScale == default) rootScale = Vector3.one;
        if (rootRot == default) rootRot = Quaternion.identity;
        _main.Build(slotDefs, styleSlots, rootPos, rootRot, rootScale);
    }

    /// <summary>
    /// 更新主预览样式
    /// </summary>
    public void UpdateMainPreview(List<GpuRoleSlot> slotDefs, List<GpuRoleStyleSlot> styleSlots)
    {
        if (_main != null && _main.IsValid)
            _main.ApplyStyle(slotDefs, styleSlots);
    }

        /// <summary>
    /// 渲染主预览
    /// </summary>
    public Texture RenderMainPreview(Rect rect, ref Vector2 drag)
    {
        return _main?.Render(rect, ref drag);
    }

        /// <summary>
        /// 应用动画帧到主预览（基于 slotKey 安全匹配）
        /// </summary>
        public void ApplyAnimationFrame(BakedFrameData frameData, List<BakedSlotData> slotKeys)
        {
            _main?.ApplyAnimationFrame(frameData, slotKeys);
        }

        /// <summary>
        /// 按 BakeData.slotKeys 的顺序重新排列 renderer 列表
        /// </summary>
        public void ReorderBySlotKeys(List<BakedSlotData> slotKeys)
        {
            _main?.ReorderBySlotKeys(slotKeys);
        }

    /// <summary>
    /// 组预览 — 每个组独立 PreviewRenderUtility，不跟主预览耦合
    /// </summary>
    public Texture RenderGroupPreview(Rect rect, int groupId, List<GpuRoleSlot> slotDefs,
        List<GpuRoleStyleSlot> styleSlots, ref Vector2 drag,
        Vector3 rootPos = default, Quaternion rootRot = default, Vector3 rootScale = default)
    {
        if (!_groupPreviews.TryGetValue(groupId, out var groupMain))
        {
            groupMain = new GpuRolePreviewRenderer_Main();
            if (rootScale == default) rootScale = Vector3.one;
            if (rootRot == default) rootRot = Quaternion.identity;
            groupMain.Build(slotDefs, styleSlots, rootPos, rootRot, rootScale);
            _groupPreviews[groupId] = groupMain;
        }

        // 每次渲染前都应用样式，确保只显示该组的部件
        groupMain.ApplyStyle(slotDefs, styleSlots, groupId);

        // 每个组独立拖拽
        if (!_groupPreviewDrags.ContainsKey(groupId))
            _groupPreviewDrags[groupId] = Vector2.zero;

        Vector2 groupDrag = _groupPreviewDrags[groupId];
        Texture result = groupMain.Render(rect, ref groupDrag);
        _groupPreviewDrags[groupId] = groupDrag;

        return result;
    }

    /// <summary>
    /// 标记组预览需要重建
    /// </summary>
    public void MarkGroupPreviewDirty(int groupId)
    {
        if (_groupPreviews.TryGetValue(groupId, out var groupMain))
        {
            groupMain.Cleanup();
            _groupPreviews.Remove(groupId);
        }
        _groupPreviewDrags.Remove(groupId);
    }

    public void CleanupAll()
    {
        _main?.Cleanup();
        foreach (var kvp in _groupPreviews)
        {
            kvp.Value.Cleanup();
        }
        _groupPreviews.Clear();
        _groupPreviewDrags.Clear();
    }
}
