using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(GpuRoleAgent))]
public class GpuRoleAgentEditor : Editor
{
    private SerializedProperty _exportData;
    private SerializedProperty _animIndex;
    private SerializedProperty _playbackSpeed;
    private SerializedProperty _playOnEnable;
    private SerializedProperty _color;
    private SerializedProperty _scale;
    private SerializedProperty _showDebugLog;
    private SerializedProperty _initialGroupVariants;
    private SerializedProperty _initialIndependentSlotSpriteIds;

    private GpuRoleAgent _agent;
    private GpuRoleExportData _lastExportData;

    // 预览
    private GpuRoleAgentPreview _preview;
    private Vector2 _previewDrag;
    private float _previewZoom = 1f;

    // 缓存当前 slot 状态（用于编辑器预览）
    private int[] _previewSlotSpriteIds;
    private bool[] _previewSlotVisible;

    private const bool DebugGroupPreview = true;

    private string GetSpriteName(int spriteId)
    {
        if (_agent == null || _agent.exportData == null || _agent.exportData.spriteUVs == null)
            return "NO_EXPORT_DATA";

        for (int i = 0; i < _agent.exportData.spriteUVs.Count; i++)
        {
            var uv = _agent.exportData.spriteUVs[i];
            if (uv.spriteId == spriteId)
                return uv.spriteName;
        }

        return "SPRITE_NOT_FOUND";
    }

    private void OnEnable()
    {
        _exportData = serializedObject.FindProperty("exportData");
        _animIndex = serializedObject.FindProperty("animIndex");
        _playbackSpeed = serializedObject.FindProperty("playbackSpeed");
        _playOnEnable = serializedObject.FindProperty("playOnEnable");
        _color = serializedObject.FindProperty("color");
        _scale = serializedObject.FindProperty("scale");
        _showDebugLog = serializedObject.FindProperty("showDebugLog");
        _initialGroupVariants = serializedObject.FindProperty("initialGroupVariants");
        _initialIndependentSlotSpriteIds = serializedObject.FindProperty("initialIndependentSlotSpriteIds");

        _agent = (GpuRoleAgent)target;
        _lastExportData = _agent.exportData;

        _preview = new GpuRoleAgentPreview();
    }

    private void OnDisable()
    {
        if (_preview != null)
        {
            _preview.Cleanup();
            _preview = null;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_exportData);
        EditorGUILayout.Space();

        if (_agent.exportData == null)
        {
            EditorGUILayout.HelpBox("请指定 ExportData", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // 如果 ExportData 变了，刷新
        if (_lastExportData != _agent.exportData)
        {
            _lastExportData = _agent.exportData;
            RefreshPreview();
            Repaint();
        }

        // 预览窗口
        DrawPreviewArea();

        DrawAnimationSection();
        EditorGUILayout.Space();

        DrawGroupVariantsSection();
        EditorGUILayout.Space();

        DrawIndependentSlotsSection();
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(_color);
        EditorGUILayout.PropertyField(_scale);
        EditorGUILayout.PropertyField(_showDebugLog);

        serializedObject.ApplyModifiedProperties();

        // 如果预览无效，在 Inspector 重绘时尝试刷新
        if (_preview != null && !_preview.IsValid && _agent.exportData != null)
        {
            RefreshPreview();
        }
    }

    private void DrawPreviewArea()
    {
        if (_agent.exportData == null) return;

        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetRect(300, 300);
        if (rect.width < 10 || rect.height < 10) return;

        // 处理拖拽
        Event e = Event.current;
        if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) && e.button == 0)
        {
            _previewDrag += e.delta * 0.01f;
            e.Use();
            Repaint();
        }

        // 滚轮缩放
        if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
        {
            _previewZoom *= e.delta.y > 0 ? 0.9f : 1.1f;
            _previewZoom = Mathf.Clamp(_previewZoom, 0.2f, 5f);
            e.Use();
            Repaint();
        }

        if (_preview != null && _preview.IsValid)
        {
            Texture tex = _preview.Render(rect, ref _previewDrag, _previewZoom);
            if (tex != null)
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }
        else
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            EditorGUI.LabelField(rect, "请先导出图集和动画", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            });
        }
    }

    private void RefreshPreview()
    {
        if (_agent.exportData == null) return;
        if (_preview == null) return;

        // 确保 serializedObject 是最新的
        serializedObject.Update();

        // 构建预览 slot 状态
        BuildPreviewSlotState();

        _preview.Build(
            _agent.exportData,
            _previewSlotSpriteIds,
            _previewSlotVisible,
            _agent.color,
            _agent.scale
        );
    }

    private void BuildPreviewSlotState()
    {
        var exportData = _agent.exportData;
        if (exportData == null || exportData.slots == null) return;

        // 确保 SerializedProperty 是最新的
        serializedObject.Update();

        _previewSlotSpriteIds = new int[exportData.slots.Count];
        _previewSlotVisible = new bool[exportData.slots.Count];

        // 先设置默认值
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            _previewSlotSpriteIds[i] = exportData.slots[i].defaultSpriteId;
            _previewSlotVisible[i] = true;
        }

        // 应用 Group Variants
        for (int g = 0; g < exportData.groups.Count; g++)
        {
            var group = exportData.groups[g];

            string variantName = "";
            if (_initialGroupVariants != null && g < _initialGroupVariants.arraySize)
            {
                variantName = _initialGroupVariants.GetArrayElementAtIndex(g).stringValue;
            }

            if (DebugGroupPreview)
            {
                Debug.Log(
                    $"[GroupPreview] group[{g}] id={group.groupId}, name={group.groupName}, " +
                    $"selectedVariant={variantName}, variantCount={(group.variants != null ? group.variants.Count : 0)}, " +
                    $"slotCount={(group.slotIndices != null ? group.slotIndices.Length : 0)}"
                );
            }

            if (string.IsNullOrEmpty(variantName))
            {
                // None：隐藏整组
                for (int si = 0; si < group.slotIndices.Length; si++)
                    _previewSlotVisible[group.slotIndices[si]] = false;
                continue;
            }

            var variant = group.variants != null
                ? group.variants.Find(v => v.variantName == variantName)
                : null;

            if (variant == null)
            {
                Debug.LogWarning(
                    $"[GroupPreview] group={group.groupName} 找不到 variant={variantName}"
                );
                continue;
            }

            if (variant.spriteIds == null)
            {
                Debug.LogWarning(
                    $"[GroupPreview] group={group.groupName}, variant={variantName} spriteIds is null"
                );
                continue;
            }

            int count = Mathf.Min(group.slotIndices.Length, variant.spriteIds.Length);

            for (int i = 0; i < count; i++)
            {
                int slotIndex = group.slotIndices[i];
                int spriteId = variant.spriteIds[i];

                string slotKey = slotIndex >= 0 && slotIndex < exportData.slots.Count
                    ? exportData.slots[slotIndex].slotKey
                    : "INVALID_SLOT";

                if (DebugGroupPreview)
                {
                    Debug.Log(
                        $"[GroupPreviewApply] group={group.groupName}, variant={variantName}, " +
                        $"i={i}, slotIndex={slotIndex}, slotKey={slotKey}, " +
                        $"spriteId={spriteId}, spriteName={GetSpriteName(spriteId)}"
                    );
                }

                if (slotIndex < 0 || slotIndex >= _previewSlotSpriteIds.Length)
                {
                    Debug.LogWarning(
                        $"[GroupPreview] slotIndex 越界: group={group.groupName}, slotIndex={slotIndex}"
                    );
                    continue;
                }

                _previewSlotSpriteIds[slotIndex] = spriteId;
                _previewSlotVisible[slotIndex] = spriteId >= 0;
            }
        }

        // 应用独立 Slot - 直接从 agent 的 public 字段读取
        var independentIndices = GetIndependentSlotIndices(exportData);
        for (int i = 0; i < independentIndices.Count && i < _agent.initialIndependentSlotSpriteIds.Length; i++)
        {
            int slotIdx = independentIndices[i];
            int spriteId = _agent.initialIndependentSlotSpriteIds[i];
            if (spriteId >= 0)
            {
                _previewSlotSpriteIds[slotIdx] = spriteId;
                _previewSlotVisible[slotIdx] = true;
            }
            else
            {
                _previewSlotVisible[slotIdx] = false;
            }
        }
    }

    private List<int> GetIndependentSlotIndices(GpuRoleExportData exportData)
    {
        HashSet<int> groupSlotSet = new HashSet<int>();
        if (exportData.groups != null)
        {
            for (int g = 0; g < exportData.groups.Count; g++)
            {
                if (exportData.groups[g].slotIndices != null)
                {
                    for (int si = 0; si < exportData.groups[g].slotIndices.Length; si++)
                        groupSlotSet.Add(exportData.groups[g].slotIndices[si]);
                }
            }
        }

        List<int> result = new List<int>();
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            if (!groupSlotSet.Contains(i))
                result.Add(i);
        }
        return result;
    }

    private void DrawAnimationSection()
    {
        EditorGUILayout.LabelField("动画", EditorStyles.boldLabel);

        var anims = _agent.exportData.animations;
        if (anims == null || anims.Count == 0)
        {
            EditorGUILayout.HelpBox("没有动画数据", MessageType.Info);
            return;
        }

        string[] animNames = new string[anims.Count];
        for (int i = 0; i < anims.Count; i++)
            animNames[i] = anims[i].animName;

        int currentAnim = Mathf.Clamp(_animIndex.intValue, 0, anims.Count - 1);
        int newAnim = EditorGUILayout.Popup("初始动画", currentAnim, animNames);
        if (newAnim != currentAnim)
        {
            _animIndex.intValue = newAnim;
            RefreshPreview();
            Repaint();
        }

        EditorGUILayout.PropertyField(_playbackSpeed);
        EditorGUILayout.PropertyField(_playOnEnable);
    }

    private void DrawGroupVariantsSection()
    {
        var groups = _agent.exportData.groups;
        if (groups == null || groups.Count == 0) return;

        EditorGUILayout.LabelField("初始 Group Variants", EditorStyles.boldLabel);

        // 确保数组长度足够
        if (_initialGroupVariants.arraySize != groups.Count)
        {
            _initialGroupVariants.arraySize = groups.Count;
            serializedObject.ApplyModifiedProperties();
        }

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            if (group.variants == null || group.variants.Count == 0) continue;

            string[] variantNames = new string[group.variants.Count + 1];
            variantNames[0] = "None (隐藏)";
            for (int v = 0; v < group.variants.Count; v++)
                variantNames[v + 1] = group.variants[v].variantName;

            // 获取当前值
            SerializedProperty elementProp = _initialGroupVariants.GetArrayElementAtIndex(g);
            string currentValue = elementProp.stringValue;

            // 找到当前选中的索引（+1 因为第0项是None）
            int currentIdx = 0;
            for (int v = 0; v < group.variants.Count; v++)
            {
                if (group.variants[v].variantName == currentValue)
                {
                    currentIdx = v + 1;
                    break;
                }
            }

            int newIdx = EditorGUILayout.Popup(group.groupName, currentIdx, variantNames);
            if (newIdx != currentIdx)
            {
                if (newIdx == 0)
                    elementProp.stringValue = "";
                else
                    elementProp.stringValue = variantNames[newIdx];
                serializedObject.ApplyModifiedProperties();

                // 立即刷新预览：销毁重建
                serializedObject.Update();
                BuildPreviewSlotState();
                if (_preview != null)
                {
                    _preview.Cleanup();
                    _preview = null;
                }
                _preview = new GpuRoleAgentPreview();
                _preview.Build(_agent.exportData, _previewSlotSpriteIds, _previewSlotVisible, _agent.color, _agent.scale);
                Repaint();
            }
        }
    }

    private void DrawIndependentSlotsSection()
    {
        var slots = _agent.exportData.slots;
        var groups = _agent.exportData.groups;

        if (slots == null || slots.Count == 0) return;

        // 找出不属于任何 group 的 slot
        HashSet<int> groupSlotSet = new HashSet<int>();
        if (groups != null)
        {
            for (int g = 0; g < groups.Count; g++)
            {
                if (groups[g].slotIndices != null)
                {
                    for (int si = 0; si < groups[g].slotIndices.Length; si++)
                        groupSlotSet.Add(groups[g].slotIndices[si]);
                }
            }
        }

        List<int> independentSlotIndices = new List<int>();
        for (int i = 0; i < slots.Count; i++)
        {
            if (!groupSlotSet.Contains(i))
                independentSlotIndices.Add(i);
        }

        if (independentSlotIndices.Count == 0) return;

        EditorGUILayout.LabelField("初始独立 Slot", EditorStyles.boldLabel);

        // 确保数组长度足够
        if (_initialIndependentSlotSpriteIds.arraySize != independentSlotIndices.Count)
        {
            _initialIndependentSlotSpriteIds.arraySize = independentSlotIndices.Count;
            serializedObject.ApplyModifiedProperties();
        }

        for (int si = 0; si < independentSlotIndices.Count; si++)
        {
            int slotIdx = independentSlotIndices[si];
            var slot = slots[slotIdx];

            // Sprite 选择
            if (slot.availableSpriteIds != null && slot.availableSpriteIds.Length > 0)
            {
                string[] spriteOptions = new string[slot.availableSpriteIds.Length + 1];
                spriteOptions[0] = "None (隐藏)";
                for (int sp = 0; sp < slot.availableSpriteIds.Length; sp++)
                {
                    int sid = slot.availableSpriteIds[sp];
                    var uv = _agent.exportData.spriteUVs.Find(u => u.spriteId == sid);
                    spriteOptions[sp + 1] = uv != null ? uv.spriteName : $"Sprite_{sid}";
                }

                // 获取当前值
                SerializedProperty elementProp = _initialIndependentSlotSpriteIds.GetArrayElementAtIndex(si);
                int currentSid = elementProp.intValue;

                int currentIdx = 0;
                for (int sp = 0; sp < slot.availableSpriteIds.Length; sp++)
                {
                    if (slot.availableSpriteIds[sp] == currentSid)
                    {
                        currentIdx = sp + 1;
                        break;
                    }
                }

                int newIdx = EditorGUILayout.Popup(slot.slotName, currentIdx, spriteOptions);
                if (newIdx != currentIdx)
                {
                    if (newIdx == 0)
                        elementProp.intValue = -1;
                    else
                        elementProp.intValue = slot.availableSpriteIds[newIdx - 1];
                    serializedObject.ApplyModifiedProperties();
                    // 立即刷新预览：销毁重建
                    serializedObject.Update();
                    BuildPreviewSlotState();
                    if (_preview != null)
                    {
                        _preview.Cleanup();
                        _preview = null;
                    }
                    _preview = new GpuRoleAgentPreview();
                    _preview.Build(_agent.exportData, _previewSlotSpriteIds, _previewSlotVisible, _agent.color, _agent.scale);
                    Repaint();
                }
            }
        }
    }
}
