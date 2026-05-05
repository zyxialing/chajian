using UnityEditor;
using UnityEngine;

/// <summary>
/// GpuRoleStyleViewer 的预览区域
/// </summary>
public partial class GpuRoleStyleViewer
{
    private Vector2 _previewDrag;

        private void DrawPreviewArea()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(400), GUILayout.ExpandHeight(true));

        GUILayout.Label("Preview", EditorStyles.boldLabel);

        if (GUILayout.Button("Open GPU Export Inspector", GUILayout.Height(24)))
        {
            GpuRoleExportInspectorWindow.Open(_core, _animBakeData);
        }

        // 用 GUILayoutUtility.GetRect 获取剩余空间
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (rect.width < 10) rect.width = 380;
        if (rect.height < 10) rect.height = 400;
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

                // 优先显示直接预览（烘焙前）
        if (_animPreviewInstance != null && _animDirectClip != null)
        {
            DrawAnimDirectPreview(rect);

            string status = _animDirectPlaying
                ? $"Direct: {_animDirectClip.name}  |  {_animDirectTime:F2}s / {_animDirectClip.length:F2}s"
                : $"Direct: {_animDirectClip.name} (stopped)";
            EditorGUI.DropShadowLabel(new Rect(rect.x + 4, rect.y + rect.height - 22, rect.width - 8, 20), status);
        }
        else if (!_core.HasData)
        {
            GUI.Label(rect, "Assign a prefab and load slots.");
        }
        else if (_renderer == null || !_renderer.HasMainPreview)
        {
            GUI.Label(rect, "Click 'Load From Prefab'.");
        }
        else
        {
            // 鼠标拖拽
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) && e.button == 0)
            {
                _previewDrag += e.delta * 0.01f;
                e.Use();
                Repaint();
            }

            Texture tex = _renderer.RenderMainPreview(rect, ref _previewDrag);
            if (tex != null)
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);

                        // 预览区域底部显示动画播放状态
            if (_animBakeData != null)
            {
                var currentAnim = GetCurrentAnimData();
                if (currentAnim != null)
                {
                    int totalFrames = currentAnim.totalFrames;
                    int currentFrame = Mathf.Clamp(Mathf.RoundToInt(_animTime * currentAnim.frameRate), 0, totalFrames - 1);
                    string status = _animPlaying
                        ? $"Anim: {currentAnim.animName}  |  Frame {currentFrame}/{totalFrames}"
                        : $"Anim: {currentAnim.animName} (stopped)";
                    EditorGUI.DropShadowLabel(new Rect(rect.x + 4, rect.y + rect.height - 22, rect.width - 8, 20), status);
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void RebuildPreview()
    {
        _renderer?.CleanupAll();
        if (_core.HasData)
            _renderer?.BuildMainPreview(_core.SlotDefinitions, _core.StyleSlots,
                _core.RootPosition, _core.RootRotation, _core.RootScale);
    }
}
