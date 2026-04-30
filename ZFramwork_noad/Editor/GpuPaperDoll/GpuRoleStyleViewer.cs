using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GPU 角色换装编辑窗口
/// 数据由 GpuRoleViewerCore（ScriptableObject）持久化，不因重编译丢失
/// </summary>
public class GpuRoleStyleViewer : EditorWindow
{
    [SerializeField] private GpuRoleViewerCore _core;
    private GpuRolePreviewRenderer _renderer;
    private Vector2 _scrollPos, _previewDrag;
    private Vector2 _groupPreviewDrag;
    private readonly List<string> _messages = new List<string>();

    private bool _coreExists;
    private bool _delayedPreviewRefresh; // 延迟刷新标记

    [MenuItem("ZFramework/Window/GPU Role/Style Viewer")]
    public static void Open()
    {
        GetWindow<GpuRoleStyleViewer>("GPU Role Style Viewer");
    }

    private void OnEnable()
    {
        _core = ScriptableObject.CreateInstance<GpuRoleViewerCore>();
        _core.hideFlags = HideFlags.HideAndDontSave;

        // 从 EditorPrefs 恢复数据
        bool loaded = _core.LoadFromEditorPrefs();
        if (!loaded)
        {
            // 尝试兼容旧的 GpuRoleViewerData（磁盘保存的）
            string[] guids = AssetDatabase.FindAssets("t:GpuRoleViewerData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var oldData = AssetDatabase.LoadAssetAtPath<GpuRoleViewerData>(path);
                if (oldData != null && oldData.sourcePrefab != null)
                {
                    _core.LoadFromPrefab(oldData.sourcePrefab);
                    if (oldData.styleSlots.Count == _core.StyleSlots.Count)
                    {
                        for (int i = 0; i < _core.StyleSlots.Count; i++)
                        {
                            _core.StyleSlots[i].spriteFolder = oldData.styleSlots[i].spriteFolder;
                            _core.StyleSlots[i].sprite = oldData.styleSlots[i].sprite;
                            _core.StyleSlots[i].color = oldData.styleSlots[i].color;
                            _core.StyleSlots[i].linkedGroupId = oldData.styleSlots[i].linkedGroupId;
                            _core.StyleSlots[i].linkedSubSpriteName = oldData.styleSlots[i].linkedSubSpriteName;
                        }
                    }
                    _core.SetGroups(oldData.groups.Select(g => new GroupDataEntry
                    {
                        groupId = g.groupId,
                        groupName = g.groupName,
                        groupSpritePath = g.groupSpritePath
                    }).ToList());
                    _core.NextGroupId = oldData.nextGroupId;
                }
            }
        }

        _renderer = new GpuRolePreviewRenderer();
        _coreExists = _core.HasData;

        if (_core.HasData)
            _renderer.BuildMainPreview(_core.SlotDefinitions, _core.StyleSlots,
                _core.RootPosition, _core.RootRotation, _core.RootScale);
    }

    private void OnDisable()
    {
        AutoSave();
        if (_renderer != null)
        {
            _renderer.CleanupAll();
            _renderer = null;
        }
    }

    private void OnDestroy()
    {
        AutoSave();
    }

    /// <summary>
    /// 将当前数据持久化到 EditorPrefs（每次修改后调用）
    /// </summary>
    private void AutoSave()
    {
        if (_core != null)
            _core.SaveToEditorPrefs();
    }

    private void OnGUI()
    {
        if (_core == null)
        {
            EditorGUILayout.LabelField("Core data lost. Reopen the window.");
            return;
        }

        if (_renderer == null)
        {
            _renderer = new GpuRolePreviewRenderer();
            if (_core.HasData)
                _renderer.BuildMainPreview(_core.SlotDefinitions, _core.StyleSlots,
                    _core.RootPosition, _core.RootRotation, _core.RootScale);
        }

        // 延迟刷新：避免在 Picker 关闭等事件流中直接操作 PreviewRenderUtility
        if (_delayedPreviewRefresh)
        {
            _delayedPreviewRefresh = false;
            _renderer?.UpdateMainPreview(_core.SlotDefinitions, _core.StyleSlots);
            Repaint();
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawToolbar();
        DrawGroupManagement();
        DrawSlotList();
        DrawPreviewArea();
        DrawMessages();

        EditorGUILayout.EndScrollView();

        _coreExists = _core.HasData;
    }

    private void DrawToolbar()
    {
        GUILayout.Label("GPU Role Style Viewer", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newPrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab", _core.SourcePrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            _core.LoadFromPrefab(newPrefab);
            AutoSave();
            RebuildPreview();
            Repaint();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Load From Prefab"))
        {
            _core.LoadFromPrefab(_core.SourcePrefab);
            AutoSave();
            RebuildPreview();
            Repaint();
        }

        if (GUILayout.Button("Random All Groups"))
        {
            RandomizeAllGroups();
            AutoSave();
            Repaint();
        }

        if (GUILayout.Button("Clear All Slots"))
        {
            _core.ClearAllSprites();
            AutoSave();
            _delayedPreviewRefresh = true;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Save Style Asset"))
        {
            SaveStyleAsset();
        }

        if (GUILayout.Button("Load Style Asset"))
        {
            LoadStyleAsset();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void RandomizeAllGroups()
    {
        if (!_core.HasData)
        {
            _messages.Add("No slots loaded.");
            return;
        }

        HashSet<int> done = new HashSet<int>();
        int count = 0;

        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            var slot = _core.StyleSlots[i];
            if (slot.linkedGroupId >= 0)
            {
                if (done.Add(slot.linkedGroupId))
                {
                    if (_core.RandomizeLinkedGroup(slot.linkedGroupId))
                        count++;
                }
            }
            else
            {
                var s = _core.PickRandomSpriteFromFolder(slot.spriteFolder);
                if (s != null) { slot.sprite = s; slot.color = Color.white; count++; }
            }
        }

        _messages.Add($"Randomized {count} groups/slots.");
        _delayedPreviewRefresh = true;
    }

    private void DrawGroupManagement()
    {
        if (_core.StyleSlots.Count == 0) return;

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Linked Group Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create New Group", GUILayout.Width(160)))
        {
            int id = _core.CreateGroup();
            AutoSave();
            _messages.Add($"Created group (ID: {id}).");
        }

        if (GUILayout.Button("Clear All Groups", GUILayout.Width(160)))
        {
            foreach (var slot in _core.StyleSlots)
            {
                slot.linkedGroupId = -1;
                slot.linkedSubSpriteName = slot.slotName;
            }
            var groups = _core.Groups.ToList();
            foreach (var g in groups) _core.RemoveGroup(g.groupId);
            AutoSave();
            _messages.Add("Cleared all groups.");
        }

        EditorGUILayout.EndHorizontal();

        // 组列表
        EditorGUI.indentLevel++;
        foreach (var g in _core.Groups)
        {
            var names = _core.GetSlotNamesInGroup(g.groupId);
            string list = names.Count > 0 ? string.Join(", ", names) : "(empty)";
            EditorGUILayout.LabelField($"{g.groupName} (ID {g.groupId}): {list}", EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;

        GUILayout.Space(4);
    }

    private void DrawSlotList()
    {
        if (_core.StyleSlots.Count == 0) return;

        GUILayout.Space(4);
        GUILayout.Label($"Slots ({_core.StyleSlots.Count})", EditorStyles.boldLabel);

        bool needsRebuild = false;
        HashSet<int> drawnGroups = new HashSet<int>();

        // 组区域
        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            int gId = _core.StyleSlots[i].linkedGroupId;
            if (gId < 0 || drawnGroups.Contains(gId)) continue;
            drawnGroups.Add(gId);
            if (DrawLinkedGroupArea(gId))
                needsRebuild = true;
        }

        // 独立槽位
        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            if (_core.StyleSlots[i].linkedGroupId >= 0) continue;
            if (DrawSingleSlot(i))
                needsRebuild = true;
        }

        if (needsRebuild)
        {
            AutoSave();
            _delayedPreviewRefresh = true;
        }

        if (GUILayout.Button("Apply Changes to Preview"))
        {
            AutoSave();
            _delayedPreviewRefresh = true;
        }
    }

    private bool DrawLinkedGroupArea(int groupId)
    {
        bool changed = false;
        string gName = _core.GetGroupName(groupId);
        Sprite currentGroupSprite = _core.GroupSprites.ContainsKey(groupId) ? _core.GroupSprites[groupId] : null;

        Color bg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.7f, 0.85f, 1f, 0.3f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = bg;

        // 标题
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Group: {gName} (ID {groupId})", EditorStyles.boldLabel);

        string newName = EditorGUILayout.TextField(gName, GUILayout.Width(200));
        if (newName != gName) _core.SetGroupName(groupId, newName);

        if (GUILayout.Button("Dissolve Group", GUILayout.Width(120)))
        {
            for (int i = 0; i < _core.StyleSlots.Count; i++)
            {
                if (_core.StyleSlots[i].linkedGroupId == groupId)
                {
                    _core.StyleSlots[i].linkedGroupId = -1;
                    _core.StyleSlots[i].linkedSubSpriteName = _core.StyleSlots[i].slotName;
                }
            }
            _core.RemoveGroup(groupId);
            _renderer?.MarkGroupPreviewDirty(groupId);
            AutoSave();
            _messages.Add($"Dissolved group ID {groupId}.");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
            return true;
        }
        EditorGUILayout.EndHorizontal();

        // 大图
        EditorGUI.BeginChangeCheck();
        var newGS = (Sprite)EditorGUILayout.ObjectField("Group Sprite", currentGroupSprite, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck())
        {
            bool wasNull = currentGroupSprite == null;
            bool isNull = newGS == null;

            if (!wasNull && isNull)
            {
                _core.ClearGroupSprite(groupId);
                _renderer?.MarkGroupPreviewDirty(groupId);
                _delayedPreviewRefresh = true;
                GUI.FocusControl(null);
            }
            else if (!isNull)
            {
                if (_core.TryApplyGroupSpriteToSlots(groupId, newGS, out var missingSubSprites))
                {
                    _core.SetGroupSpritePath(groupId, AssetDatabase.GetAssetPath(newGS));
                    _renderer?.MarkGroupPreviewDirty(groupId);
                    _delayedPreviewRefresh = true;
                    GUI.FocusControl(null);
                }
                else
                {
                    _messages.Add($"Group {gName}: selected sprite is missing sub sprites: {string.Join(", ", missingSubSprites)}");
                    GUI.FocusControl(null);
                }
            }
            changed = true;
        }

        // 组目录
        var gEntry = _core.Groups.FirstOrDefault(x => x.groupId == groupId);
        DefaultAsset gFolderAsset = null;
        if (gEntry != null && !string.IsNullOrEmpty(gEntry.groupSpriteFolder))
            gFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(gEntry.groupSpriteFolder);

        EditorGUI.BeginChangeCheck();
        var newGFolder = (DefaultAsset)EditorGUILayout.ObjectField("Group Sprite Folder", gFolderAsset, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (gEntry != null)
                gEntry.groupSpriteFolder = newGFolder != null ? AssetDatabase.GetAssetPath(newGFolder) : "";
            changed = true;
        }

        if (GUILayout.Button("Random Group Sprite from Folder", GUILayout.Width(240)))
        {
            if (_core.RandomizeLinkedGroup(groupId))
            {
                changed = true;
                _core.SetGroupSpritePath(groupId, 
                    _core.GroupSprites.ContainsKey(groupId) && _core.GroupSprites[groupId] != null
                    ? AssetDatabase.GetAssetPath(_core.GroupSprites[groupId]) : "");
                _renderer?.MarkGroupPreviewDirty(groupId);
                _delayedPreviewRefresh = true;
            }
        }

        // 子 sprite 分配
        GUILayout.Space(4);
        EditorGUILayout.LabelField("Slot → Sub Sprite Name:", EditorStyles.boldLabel);
        var indices = _core.GetSlotIndicesInGroup(groupId);
        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            if (_core.StyleSlots[i].linkedGroupId != groupId) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  {_core.StyleSlots[i].slotName}", GUILayout.Width(120));

            EditorGUI.BeginChangeCheck();
            string newSub = EditorGUILayout.TextField(_core.StyleSlots[i].linkedSubSpriteName, GUILayout.Width(150));
            if (EditorGUI.EndChangeCheck())
            {
                _core.StyleSlots[i].linkedSubSpriteName = newSub;
                if (currentGroupSprite != null)
                {
                    if (!_core.TryApplyGroupSpriteToSlots(groupId, currentGroupSprite, out var missingSubSprites))
                    {
                        _messages.Add($"Group {gName}: current sprite is missing sub sprites: {string.Join(", ", missingSubSprites)}");
                    }
                }
                _renderer?.MarkGroupPreviewDirty(groupId);
                _delayedPreviewRefresh = true;
                changed = true;
            }

            if (_core.StyleSlots[i].sprite != null)
                EditorGUILayout.LabelField($"→ {_core.StyleSlots[i].sprite.name}", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField("→ (none)", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        // 按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Slot to This Group"))
            ShowSlotPickerForGroup(groupId);
        if (GUILayout.Button("Open Group Preview", GUILayout.Width(140)))
            GpuRoleGroupPreviewWindow.Open(groupId, _core, _renderer);
        if (GUILayout.Button("Refresh Group Preview", GUILayout.Width(160)))
        {
            _renderer?.MarkGroupPreviewDirty(groupId);
            _delayedPreviewRefresh = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical(); // 左边结束

        // 右边：小组预览
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(220), GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField("Group Preview", EditorStyles.boldLabel);
        Rect pRect = GUILayoutUtility.GetRect(200, 260, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(pRect, new Color(0.12f, 0.12f, 0.12f, 1f));

        // 拖拽
        Event e = Event.current;
        if (e.type == EventType.MouseDrag && pRect.Contains(e.mousePosition) && e.button == 0)
        {
            _groupPreviewDrag += e.delta * 0.01f;
            e.Use();
            Repaint();
        }

        Texture tex = _renderer?.RenderGroupPreview(pRect, groupId, 
            _core.SlotDefinitions, _core.StyleSlots, ref _groupPreviewDrag,
            _core.RootPosition, _core.RootRotation, _core.RootScale);
        if (tex != null)
            GUI.DrawTexture(pRect, tex, ScaleMode.StretchToFill, false);
        else
            EditorGUI.LabelField(pRect, "No preview", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical(); // 右边结束
        EditorGUILayout.EndHorizontal(); // 水平结束
        GUILayout.Space(4);
        return changed;
    }

    private bool DrawSingleSlot(int index)
    {
        bool changed = false;
        var slot = _core.StyleSlots[index];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(slot.slotName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Key: " + slot.slotKey);

        // 目录
        DefaultAsset folderAsset = null;
        if (!string.IsNullOrEmpty(slot.spriteFolder))
            folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(slot.spriteFolder);

        EditorGUI.BeginChangeCheck();
        var newFolder = (DefaultAsset)EditorGUILayout.ObjectField("Sprite Folder", folderAsset, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            slot.spriteFolder = (newFolder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(newFolder)))
                ? AssetDatabase.GetAssetPath(newFolder) : "";
        }

        EditorGUI.BeginChangeCheck();
        var newSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", slot.sprite, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck()) { slot.sprite = newSprite; changed = true; }

        EditorGUI.BeginChangeCheck();
        var newColor = EditorGUILayout.ColorField("Color", slot.color);
        if (EditorGUI.EndChangeCheck()) { slot.color = newColor; changed = true; }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Random from Folder", GUILayout.Width(160)))
        {
            slot.sprite = _core.PickRandomSpriteFromFolder(slot.spriteFolder);
            changed = true;
        }
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            slot.sprite = null;
            changed = true;
            _delayedPreviewRefresh = true;
        }
        EditorGUILayout.EndHorizontal();

        if (_core.Groups.Any())
        {
            if (GUILayout.Button("Add to Group"))
                ShowGroupPickerForSlot(index);
        }

        if (!string.IsNullOrEmpty(slot.spriteFolder) && AssetDatabase.IsValidFolder(slot.spriteFolder))
        {
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { slot.spriteFolder });
            EditorGUILayout.LabelField($"Available sprites: {guids.Length}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
        return changed;
    }

    private void ShowSlotPickerForGroup(int targetGroupId)
    {
        var menu = new GenericMenu();
        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            int idx = i;
            string label;
            if (_core.StyleSlots[i].linkedGroupId >= 0)
            {
                int eg = _core.StyleSlots[i].linkedGroupId;
                string egName = _core.GetGroupName(eg);
                label = $"{_core.StyleSlots[i].slotName} (in {egName})";
            }
            else
            {
                label = _core.StyleSlots[i].slotName;
            }

            menu.AddItem(new GUIContent(label), false, () =>
            {
                _core.StyleSlots[idx].linkedGroupId = targetGroupId;
                _core.StyleSlots[idx].linkedSubSpriteName = _core.StyleSlots[idx].slotName;
                var gs = _core.GroupSprites.ContainsKey(targetGroupId) ? _core.GroupSprites[targetGroupId] : null;
                if (gs != null && !_core.TryApplyGroupSpriteToSlots(targetGroupId, gs, out var missingSubSprites))
                {
                    _messages.Add($"Group {_core.GetGroupName(targetGroupId)}: current sprite is missing sub sprites: {string.Join(", ", missingSubSprites)}");
                }
                _renderer?.MarkGroupPreviewDirty(targetGroupId);
                _delayedPreviewRefresh = true;
                AutoSave();
                _messages.Add($"Added {_core.StyleSlots[idx].slotName} to group {targetGroupId}.");
                Repaint();
            });
        }
        menu.ShowAsContext();
    }

    private void ShowGroupPickerForSlot(int slotIndex)
    {
        var menu = new GenericMenu();
        foreach (var g in _core.Groups)
        {
            int gId = g.groupId;
            menu.AddItem(new GUIContent($"{g.groupName} (ID {gId})"), false, (object id) =>
            {
                int groupId = (int)id;
                _core.StyleSlots[slotIndex].linkedGroupId = groupId;
                _core.StyleSlots[slotIndex].linkedSubSpriteName = _core.StyleSlots[slotIndex].slotName;
                var gs = _core.GroupSprites.ContainsKey(groupId) ? _core.GroupSprites[groupId] : null;
                if (gs != null && !_core.TryApplyGroupSpriteToSlots(groupId, gs, out var missingSubSprites))
                {
                    _messages.Add($"Group {_core.GetGroupName(groupId)}: current sprite is missing sub sprites: {string.Join(", ", missingSubSprites)}");
                }
                _renderer?.MarkGroupPreviewDirty(groupId);
                _delayedPreviewRefresh = true;
                AutoSave();
                _messages.Add($"Added {_core.StyleSlots[slotIndex].slotName} to group {groupId}.");
                Repaint();
            }, gId);
        }
        menu.ShowAsContext();
    }

    private void DrawPreviewArea()
    {
        GUILayout.Space(8);
        GUILayout.Label("Preview", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetRect(360f, 400f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

        if (!_core.HasData)
        {
            GUI.Label(rect, "Assign a prefab and load slots.");
            return;
        }

        if (_renderer == null || !_renderer.HasMainPreview)
        {
            GUI.Label(rect, "Click 'Load From Prefab'.");
            return;
        }

        // 拖拽
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
    }

    private void RebuildPreview()
    {
        _renderer?.CleanupAll();
        if (_core.HasData)
            _renderer?.BuildMainPreview(_core.SlotDefinitions, _core.StyleSlots,
                _core.RootPosition, _core.RootRotation, _core.RootScale);
    }

    private void SaveStyleAsset()
    {
        if (!_core.HasData) { _messages.Add("Nothing to save."); return; }

        var folder = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;

        var rel = GetRelativePath(folder);
        if (rel == null) return;

        var data = ScriptableObject.CreateInstance<GpuRoleStyleData>();
        data.sourcePrefab = _core.SourcePrefab;
        data.sourcePrefabPath = AssetDatabase.GetAssetPath(_core.SourcePrefab);
        data.generatedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.slots = new List<GpuRoleStyleSlot>();
        foreach (var s in _core.StyleSlots)
            data.slots.Add(new GpuRoleStyleSlot { slotKey = s.slotKey, slotName = s.slotName, spriteFolder = s.spriteFolder, sprite = s.sprite, color = s.color, linkedGroupId = s.linkedGroupId, linkedSubSpriteName = s.linkedSubSpriteName });

        string path = AssetDatabase.GenerateUniqueAssetPath($"{rel}/{_core.SourcePrefab.name}_Style.asset");
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = data;
        _messages.Add($"Saved: {path}");
    }

    private void LoadStyleAsset()
    {
        var path = EditorUtility.OpenFilePanel("Select Style Asset", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;

        var rel = GetRelativePath(path);
        var data = AssetDatabase.LoadAssetAtPath<GpuRoleStyleData>(rel);
        if (data == null) { _messages.Add("Failed to load."); return; }

        _core.LoadFromPrefab(data.sourcePrefab);

        for (int i = 0; i < data.slots.Count && i < _core.StyleSlots.Count; i++)
        {
            _core.StyleSlots[i].spriteFolder = data.slots[i].spriteFolder;
            _core.StyleSlots[i].sprite = data.slots[i].sprite;
            _core.StyleSlots[i].color = data.slots[i].color;
            _core.StyleSlots[i].linkedGroupId = data.slots[i].linkedGroupId;
            _core.StyleSlots[i].linkedSubSpriteName = data.slots[i].linkedSubSpriteName;
        }

        RebuildPreview();
        AutoSave();
        _messages.Add($"Loaded: {rel}");
        Repaint();
    }

    private string GetRelativePath(string fullPath)
    {
        string dp = Application.dataPath;
        return fullPath.StartsWith(dp) ? "Assets" + fullPath.Substring(dp.Length) : null;
    }

    private void DrawMessages()
    {
        foreach (var m in _messages)
            EditorGUILayout.HelpBox(m, MessageType.Info);
    }
}
