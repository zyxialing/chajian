using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 联动组组合预览弹出窗口：只展示该组部件组合在一起的样子
/// </summary>
public class GpuRoleGroupPreviewWindow : EditorWindow
{
    private GpuRoleViewerCore _core;
    private GpuRolePreviewRenderer _renderer;
    private Vector2 _drag;
    private int _groupId;
    private bool _initialized;

    public static void Open(int groupId, GpuRoleViewerCore core, GpuRolePreviewRenderer renderer)
    {
        var window = GetWindow<GpuRoleGroupPreviewWindow>("Group Preview");
        window._core = core;
        window._renderer = renderer;
        window._groupId = groupId;
        window._initialized = false;
        window.titleContent = new GUIContent($"Group: {core.GetGroupName(groupId)} (ID {groupId})");
        window.Show();
    }

    private void OnEnable()
    {
        _initialized = false;
    }

    private void OnGUI()
    {
        if (_core == null || !_core.HasData)
        {
            EditorGUILayout.LabelField("No data. Close and reopen.");
            return;
        }
        // 工具条
        EditorGUILayout.LabelField($"Group: {_core.GetGroupName(_groupId)} (ID {_groupId})", EditorStyles.boldLabel);

        // 显示组内槽位
        var indices = _core.GetSlotIndicesInGroup(_groupId);
        foreach (int i in indices)
        {
            var slot = _core.GetSlot(i);
            string sn = slot.sprite != null ? slot.sprite.name : "(none)";
            EditorGUILayout.LabelField($"  {slot.slotName}: {sn}", EditorStyles.miniLabel);
        }

        GUILayout.Space(4);

        // 预览区域
        Rect rect = GUILayoutUtility.GetRect(10, position.width, 200, position.height - 80);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

        // 拖拽
        Event e = Event.current;
        if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) && e.button == 0)
        {
            _drag += e.delta * 0.01f;
            e.Use();
            Repaint();
        }

        Texture tex = _renderer?.RenderGroupPreview(rect, _groupId,
            _core.SlotDefinitions, _core.StyleSlots, ref _drag,
            _core.RootPosition, _core.RootRotation, _core.RootScale);
        if (tex != null)
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        else
            EditorGUI.LabelField(rect, "No preview", EditorStyles.centeredGreyMiniLabel);

        // 刷新
        Repaint();
    }
}

