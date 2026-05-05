using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class GpuRoleStyleViewer
{
    private Dictionary<int, Vector2> _groupPreviewDrags = new Dictionary<int, Vector2>();

    private Vector2 GetGroupDrag(int groupId)
    {
        if (!_groupPreviewDrags.ContainsKey(groupId))
            _groupPreviewDrags[groupId] = Vector2.zero;
        return _groupPreviewDrags[groupId];
    }

    private void SetGroupDrag(int groupId, Vector2 drag)
    {
        _groupPreviewDrags[groupId] = drag;
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

        // 大图 - 支持拖拽 Texture2D 自动取第一个子 Sprite
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
                    _core.ApplyGroupExclusive(groupId);
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
        // 额外处理 Texture2D 拖拽
        else
        {
            var dropRect = GUILayoutUtility.GetLastRect();
            var evt = Event.current;
            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropRect.Contains(evt.mousePosition))
            {
                bool hasTexture = false;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is Texture2D) { hasTexture = true; break; }
                }
                if (hasTexture)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is Texture2D tex2)
                            {
                                string path = AssetDatabase.GetAssetPath(tex2);
                                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                                if (sprites.Length > 0)
                                {
                                    if (_core.TryApplyGroupSpriteToSlots(groupId, sprites[0], out var missingSubSprites))
                                    {
                                        _core.SetGroupSpritePath(groupId, path);
                                        _core.ApplyGroupExclusive(groupId);
                                        _renderer?.MarkGroupPreviewDirty(groupId);
                                        _delayedPreviewRefresh = true;
                                        _messages.Add($"Group {gName}: loaded sprite from texture.");
                                    }
                                    else
                                    {
                                        _messages.Add($"Group {gName}: texture has no valid sub sprites: {string.Join(", ", missingSubSprites)}");
                                    }
                                }
                                break;
                            }
                        }
                        changed = true;
                    }
                    evt.Use();
                }
            }
        }

        // 组目录 - 支持拖拽文件夹
        string gFolderPath = _core.GetGroupSpriteFolder(groupId);
        DrawFolderField("Group Sprite Folder", gFolderPath, (path) =>
        {
            _core.SetGroupSpriteFolder(groupId, path);
            changed = true;
        });

        if (GUILayout.Button("Random Group Sprite from Folder", GUILayout.Width(240)))
        {
            bool result = _core.RandomizeLinkedGroup(groupId);
            if (result)
            {
                changed = true;
                _core.SetGroupSpritePath(groupId,
                    _core.GroupSprites.ContainsKey(groupId) && _core.GroupSprites[groupId] != null
                    ? AssetDatabase.GetAssetPath(_core.GroupSprites[groupId]) : "");
                _core.ApplyGroupExclusive(groupId);
                _renderer?.MarkGroupPreviewDirty(groupId);
                _delayedPreviewRefresh = true;
                _messages.Add($"Group {gName}: randomized successfully.");
            }
            else
            {
                _messages.Add($"Group {gName}: random failed - no sprite folder set and no group sprite path to fallback to.");
            }
        }

        // 联动组的互斥组归属
        var exclGroups = _core.ExclusiveGroups;
        int currentGroupExclId = -1;
        foreach (var eg in exclGroups)
        {
            if (eg.memberGroupIds.Contains(groupId))
            {
                currentGroupExclId = eg.exclusiveGroupId;
                break;
            }
        }

        if (currentGroupExclId >= 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"In Exclusive Group: {_core.GetExclusiveGroupName(currentGroupExclId)} (ID {currentGroupExclId})", EditorStyles.miniLabel);
            if (GUILayout.Button("Remove from Exclusive", GUILayout.Width(160)))
            {
                _core.RemoveGroupFromExclusive(currentGroupExclId, groupId);
                changed = true;
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            if (exclGroups.Count > 0)
            {
                var exclNames = exclGroups.Select(eg => $"{eg.groupName} (ID {eg.exclusiveGroupId})").Prepend("-").ToArray();
                int currentExclIdx = 0;
                EditorGUI.BeginChangeCheck();
                int newExclIdx = EditorGUILayout.Popup("Add to Exclusive Group", currentExclIdx, exclNames, GUILayout.Width(300));
                if (EditorGUI.EndChangeCheck() && newExclIdx > 0)
                {
                    _core.AddGroupToExclusive(exclGroups[newExclIdx - 1].exclusiveGroupId, groupId);
                    changed = true;
                }
            }
            else
            {
                if (GUILayout.Button("Add Group to New Exclusive Group", GUILayout.Width(260)))
                {
                    int egId = _core.CreateExclusiveGroup();
                    _core.AddGroupToExclusive(egId, groupId);
                    _messages.Add($"Created exclusive group (ID {egId}) and added group {gName}.");
                    changed = true;
                }
            }
        }

        // 子 sprite 分配
        GUILayout.Space(4);
        EditorGUILayout.LabelField("Slot -> Sub Sprite Name:", EditorStyles.boldLabel);
        string groupSpritePath = _core.GetGroupSpritePath(groupId);
        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            if (_core.StyleSlots[i].linkedGroupId != groupId) continue;

            EditorGUILayout.BeginHorizontal();
            var slotLabel = new GUIContent($"  {_core.StyleSlots[i].slotName}", _core.StyleSlots[i].slotKey);
            EditorGUILayout.LabelField(slotLabel, GUILayout.Width(120));

            // 颜色选择器
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(_core.StyleSlots[i].color, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck())
            {
                _core.StyleSlots[i].color = newColor;
                _renderer?.MarkGroupPreviewDirty(groupId);
                _delayedPreviewRefresh = true;
                changed = true;
            }

            // 互斥组选择（slot 级别）
            var slotExclGroups = _core.ExclusiveGroups;
            int currentSlotExclId = -1;
            foreach (var eg in slotExclGroups)
            {
                if (eg.memberSlotIndices.Contains(i))
                {
                    currentSlotExclId = eg.exclusiveGroupId;
                    break;
                }
            }

            if (currentSlotExclId >= 0)
            {
                EditorGUILayout.LabelField($"In {_core.GetExclusiveGroupName(currentSlotExclId)}", EditorStyles.miniLabel, GUILayout.Width(100));
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    _core.RemoveSlotFromExclusive(currentSlotExclId, i);
                    changed = true;
                }
            }
            else if (slotExclGroups.Count > 0)
            {
                var slotExclNames = slotExclGroups.Select(eg => $"{eg.groupName}").Prepend("-").ToArray();
                int currentExclIdx = 0;
                EditorGUI.BeginChangeCheck();
                int newExclIdx = EditorGUILayout.Popup(currentExclIdx, slotExclNames, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck() && newExclIdx > 0)
                {
                    _core.AddSlotToExclusive(slotExclGroups[newExclIdx - 1].exclusiveGroupId, i);
                    changed = true;
                }
            }

            // 从组大图路径加载所有子 Sprite 名称，始终显示下拉列表
            string[] subSpriteNames = new string[0];
            if (!string.IsNullOrEmpty(groupSpritePath))
            {
                subSpriteNames = AssetDatabase.LoadAllAssetsAtPath(groupSpritePath)
                    .OfType<Sprite>()
                    .Select(s => s.name)
                    .Distinct()
                    .ToArray();
            }

            if (subSpriteNames.Length > 0)
            {
                int currentIdx = System.Array.IndexOf(subSpriteNames, _core.StyleSlots[i].linkedSubSpriteName);
                if (currentIdx < 0) currentIdx = 0;

                EditorGUI.BeginChangeCheck();
                int newIdx = EditorGUILayout.Popup(currentIdx, subSpriteNames, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    _core.StyleSlots[i].linkedSubSpriteName = subSpriteNames[newIdx];
                    if (currentGroupSprite != null)
                    {
                        _core.TryApplyGroupSpriteToSlots(groupId, currentGroupSprite, out _);
                    }
                    _core.ApplySlotExclusive(i);
                    _renderer?.MarkGroupPreviewDirty(groupId);
                    _delayedPreviewRefresh = true;
                    changed = true;
                }
            }
            else
            {
                // 没有子 Sprite 列表时，显示可编辑的文本框让用户手动输入
                EditorGUI.BeginChangeCheck();
                string newSubName = EditorGUILayout.TextField(_core.StyleSlots[i].linkedSubSpriteName, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    _core.StyleSlots[i].linkedSubSpriteName = newSubName;
                    if (currentGroupSprite != null)
                    {
                        _core.TryApplyGroupSpriteToSlots(groupId, currentGroupSprite, out _);
                    }
                    _core.ApplySlotExclusive(i);
                    _renderer?.MarkGroupPreviewDirty(groupId);
                    _delayedPreviewRefresh = true;
                    changed = true;
                }
            }

            if (_core.StyleSlots[i].sprite != null)
                EditorGUILayout.LabelField($"-> {_core.StyleSlots[i].sprite.name}", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField("-> (none)", EditorStyles.miniLabel);

            // 从组中移除按钮
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                _core.StyleSlots[i].linkedGroupId = -1;
                _core.StyleSlots[i].linkedSubSpriteName = _core.StyleSlots[i].slotName;
                _renderer?.MarkGroupPreviewDirty(groupId);
                _delayedPreviewRefresh = true;
                changed = true;
                _messages.Add($"Removed {_core.StyleSlots[i].slotName} from group {gName}.");
            }

            EditorGUILayout.EndHorizontal();
        }

        // 保存当前图集结构按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Current Atlas Structure", GUILayout.Width(200)))
        {
            AutoSave();
            _messages.Add($"Group {gName}: saved current sub sprite name mappings.");
        }
        EditorGUILayout.EndHorizontal();

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

        // 拖拽（每个组独立）
        Event e = Event.current;
        if (e.type == EventType.MouseDrag && pRect.Contains(e.mousePosition) && e.button == 0)
        {
            Vector2 drag = GetGroupDrag(groupId);
            drag += e.delta * 0.01f;
            SetGroupDrag(groupId, drag);
            e.Use();
            Repaint();
        }

        Vector2 groupDrag = GetGroupDrag(groupId);
        Texture tex = _renderer?.RenderGroupPreview(pRect, groupId,
            _core.SlotDefinitions, _core.StyleSlots, ref groupDrag,
            _core.RootPosition, _core.RootRotation, _core.RootScale);
        SetGroupDrag(groupId, groupDrag);
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

                // 别名（只有独立 Slot 才需要）
        if (slot.linkedGroupId >= 0)
        {
            EditorGUILayout.LabelField("Alias", "--- (in group)");
        }
        else
        {
            if (string.IsNullOrEmpty(slot.aliasName))
                slot.aliasName = slot.slotKey;
            EditorGUI.BeginChangeCheck();
            string newAlias = EditorGUILayout.TextField("Alias", slot.aliasName);
            if (EditorGUI.EndChangeCheck())
            {
                slot.aliasName = newAlias;
                changed = true;
            }
        }

        // 目录 - 支持拖拽文件夹
        DrawFolderField("Sprite Folder", slot.spriteFolder, (path) =>
        {
            slot.spriteFolder = path;
        });

        EditorGUI.BeginChangeCheck();
        var newSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", slot.sprite, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck()) { slot.sprite = newSprite; _core.ApplySlotExclusive(index); changed = true; _delayedPreviewRefresh = true; } // 修复 Bug 8

        EditorGUI.BeginChangeCheck();
        var newColor = EditorGUILayout.ColorField("Color", slot.color);
        if (EditorGUI.EndChangeCheck()) { slot.color = newColor; changed = true; _delayedPreviewRefresh = true; } // 修复 Bug 8

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Random from Folder", GUILayout.Width(160)))
        {
            slot.sprite = _core.PickRandomSpriteFromFolder(slot.spriteFolder);
            _core.ApplySlotExclusive(index);
            changed = true;
            _delayedPreviewRefresh = true; // 修复 Bug 8
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

        // 互斥组
        var exclGroups = _core.ExclusiveGroups;
        int currentSlotExclId = -1;
        foreach (var eg in exclGroups)
        {
            if (eg.memberSlotIndices.Contains(index))
            {
                currentSlotExclId = eg.exclusiveGroupId;
                break;
            }
        }

        if (currentSlotExclId >= 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"In Exclusive Group: {_core.GetExclusiveGroupName(currentSlotExclId)}", EditorStyles.miniLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                _core.RemoveSlotFromExclusive(currentSlotExclId, index);
                changed = true;
            }
            EditorGUILayout.EndHorizontal();
        }
        else if (exclGroups.Count > 0)
        {
            var exclNames = exclGroups.Select(eg => $"{eg.groupName} (ID {eg.exclusiveGroupId})").Prepend("None").ToArray();
            int currentExclIdx = 0;
            EditorGUI.BeginChangeCheck();
            int newExclIdx = EditorGUILayout.Popup("Exclusive Group", currentExclIdx, exclNames, GUILayout.Width(300));
            if (EditorGUI.EndChangeCheck() && newExclIdx > 0)
            {
                _core.AddSlotToExclusive(exclGroups[newExclIdx - 1].exclusiveGroupId, index);
                changed = true;
            }
        }
        else
        {
            if (GUILayout.Button("Add to New Exclusive Group", GUILayout.Width(200)))
            {
                int egId = _core.CreateExclusiveGroup();
                _core.AddSlotToExclusive(egId, index);
                _messages.Add($"Created exclusive group (ID {egId}) and added {slot.slotName}.");
                changed = true;
            }
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
                var indices = _core.GetSlotIndicesInGroup(targetGroupId);
                string groupSpritePath = _core.GetGroupSpritePath(targetGroupId);
                if (!string.IsNullOrEmpty(groupSpritePath))
                {
                    var names = AssetDatabase.LoadAllAssetsAtPath(groupSpritePath)
                        .OfType<Sprite>()
                        .Select(s => s.name)
                        .Distinct()
                        .ToArray();
                    int defaultIdx = indices.Count - 1;
                    _core.StyleSlots[idx].linkedSubSpriteName = defaultIdx < names.Length ? names[defaultIdx] : _core.StyleSlots[idx].slotName;
                }
                else
                {
                    _core.StyleSlots[idx].linkedSubSpriteName = _core.StyleSlots[idx].slotName;
                }
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
                var indices = _core.GetSlotIndicesInGroup(groupId);
                string groupSpritePath = _core.GetGroupSpritePath(groupId);
                if (!string.IsNullOrEmpty(groupSpritePath))
                {
                    var names = AssetDatabase.LoadAllAssetsAtPath(groupSpritePath)
                        .OfType<Sprite>()
                        .Select(s => s.name)
                        .Distinct()
                        .ToArray();
                    int defaultIdx = indices.Count - 1;
                    _core.StyleSlots[slotIndex].linkedSubSpriteName = defaultIdx < names.Length ? names[defaultIdx] : _core.StyleSlots[slotIndex].slotName;
                }
                else
                {
                    _core.StyleSlots[slotIndex].linkedSubSpriteName = _core.StyleSlots[slotIndex].slotName;
                }
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
}
