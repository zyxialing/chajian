using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GpuRoleGpuAgent))]
public class GpuRoleGpuAgentEditor : Editor
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

    private GpuRoleGpuAgent _agent;
    private GpuRoleExportData _lastExportData;
    private GpuRoleAgentPreview _preview;
    private Vector2 _previewDrag;
    private float _previewZoom = 1f;
    private int[] _previewSlotSpriteIds;
    private bool[] _previewSlotVisible;

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

        _agent = (GpuRoleGpuAgent)target;
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

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_exportData);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            _lastExportData = _agent.exportData;
            RebuildPreview();
        }

        EditorGUILayout.Space();

        if (_agent.exportData == null)
        {
            EditorGUILayout.HelpBox("请指定 ExportData", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (_lastExportData != _agent.exportData)
        {
            _lastExportData = _agent.exportData;
            RebuildPreview();
        }

        NormalizeInitialArraysIfNeeded();

        DrawPreviewArea();
        DrawAnimationSection();
        EditorGUILayout.Space();
        DrawGroupVariantsSection();
        EditorGUILayout.Space();
        DrawIndependentSlotsSection();
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_color);
        EditorGUILayout.PropertyField(_scale);
        EditorGUILayout.PropertyField(_showDebugLog);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            RebuildPreview();
            NotifyRuntimeVisualDirty();
            return;
        }

        serializedObject.ApplyModifiedProperties();

        if (_preview != null && !_preview.IsValid)
            RebuildPreview();
    }

    private void DrawPreviewArea()
    {
        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("这是 Inspector 测试预览，正式运行以 GpuRoleGpuManager 渲染为准。", MessageType.None);

        Rect rect = GUILayoutUtility.GetRect(300, 300);
        if (rect.width < 10f || rect.height < 10f)
            return;

        Event e = Event.current;
        if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) && e.button == 0)
        {
            _previewDrag += e.delta * 0.01f;
            e.Use();
            Repaint();
        }

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
            EditorGUI.LabelField(rect, "请先生成预览", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            });
        }
    }

    private void DrawAnimationSection()
    {
        EditorGUILayout.LabelField("动画", EditorStyles.boldLabel);

        List<AnimExportData> anims = _agent.exportData.animations;
        if (anims == null || anims.Count == 0)
        {
            EditorGUILayout.HelpBox("没有动画数据", MessageType.Info);
            return;
        }

        string[] names = new string[anims.Count];
        for (int i = 0; i < anims.Count; i++)
            names[i] = anims[i].animName;

        int current = Mathf.Clamp(_animIndex.intValue, 0, anims.Count - 1);
        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.Popup("初始动画", current, names);
        EditorGUILayout.PropertyField(_playbackSpeed);
        EditorGUILayout.PropertyField(_playOnEnable);
        if (EditorGUI.EndChangeCheck())
        {
            _animIndex.intValue = next;
            serializedObject.ApplyModifiedProperties();
            NotifyRuntimeAnimationDirty();
        }
    }

    private void DrawGroupVariantsSection()
    {
        List<GroupExportData> groups = _agent.exportData.groups;
        if (groups == null || groups.Count == 0)
            return;

        EditorGUILayout.LabelField("初始 Group Variants", EditorStyles.boldLabel);

        if (_initialGroupVariants.arraySize != groups.Count)
        {
            int oldSize = _initialGroupVariants.arraySize;
            _initialGroupVariants.arraySize = groups.Count;
            for (int i = oldSize; i < groups.Count; i++)
            {
                GroupExportData group = groups[i];
                SerializedProperty element = _initialGroupVariants.GetArrayElementAtIndex(i);
                element.stringValue = group.variants != null && group.variants.Count > 0
                    ? group.variants[0].variantName
                    : string.Empty;
            }
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        for (int g = 0; g < groups.Count; g++)
        {
            GroupExportData group = groups[g];
            if (group.variants == null || group.variants.Count == 0)
                continue;

            string[] options = new string[group.variants.Count + 1];
            options[0] = "None (隐藏)";
            for (int v = 0; v < group.variants.Count; v++)
                options[v + 1] = group.variants[v].variantName;

            SerializedProperty element = _initialGroupVariants.GetArrayElementAtIndex(g);
            int current = 0;
            for (int v = 0; v < group.variants.Count; v++)
            {
                if (group.variants[v].variantName == element.stringValue)
                {
                    current = v + 1;
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(group.groupName, current, options);
            if (EditorGUI.EndChangeCheck())
            {
                element.stringValue = next == 0 ? string.Empty : options[next];
                serializedObject.ApplyModifiedProperties();
                RebuildPreview();
                NotifyRuntimeStyleDirty();
            }
        }
    }

    private void DrawIndependentSlotsSection()
    {
        List<int> independent = GetIndependentSlotIndices(_agent.exportData);
        if (independent.Count == 0)
            return;

        EditorGUILayout.LabelField("初始独立 Slot", EditorStyles.boldLabel);

        if (_initialIndependentSlotSpriteIds.arraySize != independent.Count)
        {
            int oldSize = _initialIndependentSlotSpriteIds.arraySize;
            _initialIndependentSlotSpriteIds.arraySize = independent.Count;
            for (int i = oldSize; i < independent.Count; i++)
            {
                int slotIndex = independent[i];
                SerializedProperty element = _initialIndependentSlotSpriteIds.GetArrayElementAtIndex(i);
                element.intValue = _agent.exportData.slots[slotIndex].defaultSpriteId;
            }
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        for (int i = 0; i < independent.Count; i++)
        {
            int slotIndex = independent[i];
            SlotExportData slot = _agent.exportData.slots[slotIndex];
            int[] available = slot.availableSpriteIds;
            if (available == null || available.Length == 0)
                continue;

            string[] options = new string[available.Length + 1];
            options[0] = "None (隐藏)";
            for (int s = 0; s < available.Length; s++)
                options[s + 1] = GetSpriteName(available[s]);

            SerializedProperty element = _initialIndependentSlotSpriteIds.GetArrayElementAtIndex(i);
            int current = 0;
            for (int s = 0; s < available.Length; s++)
            {
                if (available[s] == element.intValue)
                {
                    current = s + 1;
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(slot.slotName, current, options);
            if (EditorGUI.EndChangeCheck())
            {
                element.intValue = next == 0 ? -1 : available[next - 1];
                serializedObject.ApplyModifiedProperties();
                RebuildPreview();
                NotifyRuntimeStyleDirty();
            }
        }
    }

    private void RebuildPreview()
    {
        if (_agent.exportData == null)
            return;

        serializedObject.Update();
        BuildPreviewSlotState();

        if (_preview != null)
            _preview.Cleanup();
        _preview = new GpuRoleAgentPreview();
        _preview.Build(_agent.exportData, _previewSlotSpriteIds, _previewSlotVisible, _agent.color, _agent.scale);
        Repaint();
    }

    private void NormalizeInitialArraysIfNeeded()
    {
        GpuRoleExportData data = _agent.exportData;
        if (data == null)
            return;

        bool changed = false;

        if (data.groups != null)
        {
            if (_initialGroupVariants.arraySize != data.groups.Count)
            {
                int oldSize = _initialGroupVariants.arraySize;
                _initialGroupVariants.arraySize = data.groups.Count;
                for (int i = oldSize; i < data.groups.Count; i++)
                {
                    GroupExportData group = data.groups[i];
                    _initialGroupVariants.GetArrayElementAtIndex(i).stringValue =
                        group.variants != null && group.variants.Count > 0 ? group.variants[0].variantName : string.Empty;
                }
                changed = true;
            }

            bool allGroupsEmpty = data.groups.Count > 0;
            for (int i = 0; i < data.groups.Count && i < _initialGroupVariants.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(_initialGroupVariants.GetArrayElementAtIndex(i).stringValue))
                {
                    allGroupsEmpty = false;
                    break;
                }
            }

            if (allGroupsEmpty)
            {
                for (int i = 0; i < data.groups.Count; i++)
                {
                    GroupExportData group = data.groups[i];
                    if (group.variants != null && group.variants.Count > 0)
                    {
                        _initialGroupVariants.GetArrayElementAtIndex(i).stringValue = group.variants[0].variantName;
                        changed = true;
                    }
                }
            }
        }

        List<int> independent = GetIndependentSlotIndices(data);
        if (_initialIndependentSlotSpriteIds.arraySize != independent.Count)
        {
            int oldSize = _initialIndependentSlotSpriteIds.arraySize;
            _initialIndependentSlotSpriteIds.arraySize = independent.Count;
            for (int i = oldSize; i < independent.Count; i++)
            {
                int slotIndex = independent[i];
                _initialIndependentSlotSpriteIds.GetArrayElementAtIndex(i).intValue = data.slots[slotIndex].defaultSpriteId;
            }
            changed = true;
        }

        for (int i = 0; i < independent.Count && i < _initialIndependentSlotSpriteIds.arraySize; i++)
        {
            int slotIndex = independent[i];
            SlotExportData slot = data.slots[slotIndex];
            SerializedProperty element = _initialIndependentSlotSpriteIds.GetArrayElementAtIndex(i);
            int spriteId = element.intValue;
            if (spriteId >= 0 && !IsSpriteAllowed(slot, spriteId))
            {
                element.intValue = slot.defaultSpriteId;
                changed = true;
            }
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            RebuildPreview();
        }
    }

    private void BuildPreviewSlotState()
    {
        GpuRoleExportData data = _agent.exportData;
        _previewSlotSpriteIds = new int[data.slots.Count];
        _previewSlotVisible = new bool[data.slots.Count];

        for (int i = 0; i < data.slots.Count; i++)
        {
            _previewSlotSpriteIds[i] = data.slots[i].defaultSpriteId;
            _previewSlotVisible[i] = true;
        }

        if (data.groups != null)
        {
            for (int g = 0; g < data.groups.Count; g++)
            {
                GroupExportData group = data.groups[g];
                string variantName = g < _initialGroupVariants.arraySize
                    ? _initialGroupVariants.GetArrayElementAtIndex(g).stringValue
                    : string.Empty;

                if (string.IsNullOrEmpty(variantName))
                {
                    if (group.slotIndices != null)
                    {
                        for (int i = 0; i < group.slotIndices.Length; i++)
                        {
                            int slotIndex = group.slotIndices[i];
                            if (slotIndex >= 0 && slotIndex < _previewSlotVisible.Length)
                                _previewSlotVisible[slotIndex] = false;
                        }
                    }
                    continue;
                }

                GroupVariant variant = group.variants != null
                    ? group.variants.Find(v => v.variantName == variantName)
                    : null;
                if (variant == null || group.slotIndices == null || variant.spriteIds == null)
                    continue;

                int count = Mathf.Min(group.slotIndices.Length, variant.spriteIds.Length);
                for (int i = 0; i < count; i++)
                {
                    int slotIndex = group.slotIndices[i];
                    if (slotIndex < 0 || slotIndex >= _previewSlotSpriteIds.Length)
                        continue;

                    int spriteId = variant.spriteIds[i];
                    _previewSlotSpriteIds[slotIndex] = spriteId;
                    _previewSlotVisible[slotIndex] = spriteId >= 0;
                }
            }
        }

        List<int> independent = GetIndependentSlotIndices(data);
        for (int i = 0; i < independent.Count && i < _initialIndependentSlotSpriteIds.arraySize; i++)
        {
            int slotIndex = independent[i];
            int spriteId = _initialIndependentSlotSpriteIds.GetArrayElementAtIndex(i).intValue;
            if (spriteId < 0)
            {
                _previewSlotVisible[slotIndex] = false;
                continue;
            }

            if (IsSpriteAllowed(data.slots[slotIndex], spriteId))
            {
                _previewSlotSpriteIds[slotIndex] = spriteId;
                _previewSlotVisible[slotIndex] = true;
            }
        }
    }

    private static bool IsSpriteAllowed(SlotExportData slot, int spriteId)
    {
        if (slot == null)
            return false;

        if (slot.defaultSpriteId == spriteId)
            return true;

        if (slot.availableSpriteIds == null)
            return false;

        for (int i = 0; i < slot.availableSpriteIds.Length; i++)
        {
            if (slot.availableSpriteIds[i] == spriteId)
                return true;
        }

        return false;
    }

    private string GetSpriteName(int spriteId)
    {
        if (_agent.exportData == null || _agent.exportData.spriteUVs == null)
            return $"Sprite_{spriteId}";

        SpriteUVData uv = _agent.exportData.spriteUVs.Find(u => u.spriteId == spriteId);
        return uv != null ? uv.spriteName : $"Sprite_{spriteId}";
    }

    private void NotifyRuntimeStyleDirty()
    {
        if (!Application.isPlaying)
            return;
        _agent.RebuildInitialState();
        _agent.manager?.MarkAgentStyleDirty(_agent);
    }

    private void NotifyRuntimeAnimationDirty()
    {
        if (!Application.isPlaying)
            return;
        _agent.Play(_animIndex.intValue);
    }

    private void NotifyRuntimeVisualDirty()
    {
        if (!Application.isPlaying)
            return;
        _agent.manager?.MarkAgentVisualDirty(_agent);
    }

    private static List<int> GetIndependentSlotIndices(GpuRoleExportData data)
    {
        List<int> result = new List<int>();
        if (data == null || data.slots == null)
            return result;

        HashSet<int> grouped = new HashSet<int>();
        if (data.groups != null)
        {
            for (int g = 0; g < data.groups.Count; g++)
            {
                int[] slotIndices = data.groups[g].slotIndices;
                if (slotIndices == null) continue;
                for (int i = 0; i < slotIndices.Length; i++)
                    grouped.Add(slotIndices[i]);
            }
        }

        for (int i = 0; i < data.slots.Count; i++)
        {
            if (!grouped.Contains(i))
                result.Add(i);
        }

        return result;
    }
}
