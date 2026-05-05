using UnityEditor;
using UnityEngine;

/// <summary>
/// 根据 spriteId 预览 Sprite 图片的测试窗口
/// 用于验证导出数据中 spriteId 对应的图片是否正确
/// </summary>
public class GpuSpritePreviewWindow : EditorWindow
{
    private GpuRoleExportData _exportData;
    private int _spriteId = 0;
    private Vector2 _scrollPos;

    [MenuItem("ZFramework/Window/GPURole Sprite Preview")]
    public static void Open()
    {
        GetWindow<GpuSpritePreviewWindow>("Sprite Preview");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sprite 预览测试", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _exportData = (GpuRoleExportData)EditorGUILayout.ObjectField("Export Data", _exportData, typeof(GpuRoleExportData), false);
        EditorGUILayout.Space();

        if (_exportData == null)
        {
            EditorGUILayout.HelpBox("请指定 ExportData", MessageType.Info);
            return;
        }

        _spriteId = EditorGUILayout.IntField("Sprite ID", _spriteId);

        // 查找 spriteId 对应的 UV 数据
        var uv = _exportData.spriteUVs.Find(u => u.spriteId == _spriteId);
        if (uv == null)
        {
            EditorGUILayout.HelpBox($"未找到 spriteId={_spriteId}", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"Sprite Name: {uv.spriteName}");
        EditorGUILayout.LabelField($"Atlas Index: {uv.atlasIndex}");
        EditorGUILayout.LabelField($"UV: ({uv.uMin:F3}, {uv.vMin:F3}) - ({uv.uMax:F3}, {uv.vMax:F3})");
        EditorGUILayout.LabelField($"Crop: {uv.cropW}x{uv.cropH}");
        EditorGUILayout.LabelField($"Original: {uv.originalWidth}x{uv.originalHeight}");

        // 显示 sourceSprite 引用
        if (uv.sourceSprite != null)
        {
            EditorGUILayout.ObjectField("Source Sprite", uv.sourceSprite, typeof(Sprite), false);
        }
        else
        {
            EditorGUILayout.LabelField("Source Sprite: null (未保存引用)");
        }

        EditorGUILayout.Space();

        Texture2D atlasTex = null;

        if (uv.atlasIndex >= 0 && uv.atlasIndex < _exportData.atlases.Count)
        {
            var atlas = _exportData.atlases[uv.atlasIndex];
            if (atlas != null)
                atlasTex = atlas.texture;
        }

        if (atlasTex == null)
        {
            EditorGUILayout.HelpBox($"未找到 atlasIndex={uv.atlasIndex} 的图集纹理", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.ObjectField("Atlas Texture", atlasTex, typeof(Texture2D), false);

            Rect rect = GUILayoutUtility.GetRect(240, 240);
            if (rect.width > 10 && rect.height > 10)
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

                Rect texCoords = new Rect(
                    uv.uMin,
                    uv.vMin,
                    uv.uMax - uv.uMin,
                    uv.vMax - uv.vMin
                );

                float aspect = Mathf.Max(0.001f, (float)uv.cropW / Mathf.Max(1, uv.cropH));

                float drawW;
                float drawH;

                if (aspect > 1f)
                {
                    drawW = rect.width * 0.8f;
                    drawH = drawW / aspect;
                }
                else
                {
                    drawH = rect.height * 0.8f;
                    drawW = drawH * aspect;
                }

                Rect drawRect = new Rect(
                    rect.x + (rect.width - drawW) * 0.5f,
                    rect.y + (rect.height - drawH) * 0.5f,
                    drawW,
                    drawH
                );

                GUI.DrawTextureWithTexCoords(drawRect, atlasTex, texCoords, true);
            }
        }

        EditorGUILayout.Space();

        // 显示所有 spriteId 列表
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));
        EditorGUILayout.LabelField("所有 Sprite 列表:", EditorStyles.boldLabel);
        foreach (var u in _exportData.spriteUVs)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"ID={u.spriteId}", GUILayout.Width(80)))
            {
                _spriteId = u.spriteId;
            }
            EditorGUILayout.LabelField(u.spriteName, GUILayout.Width(150));
            EditorGUILayout.LabelField($"atlas={u.atlasIndex}, crop={u.cropW}x{u.cropH}");
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}
