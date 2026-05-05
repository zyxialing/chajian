using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class GpuRoleStyleViewer
{
    private void SaveStyleAsset()
    {
        if (!_core.HasData) { _messages.Add("Nothing to save."); return; }

        // 选择目标文件（可覆盖已有文件）
        string defaultName = _core.SourcePrefab != null ? _core.SourcePrefab.name : "Style";
        var path = EditorUtility.SaveFilePanelInProject("Save Style Asset", defaultName + "_Style.asset", "asset", "Select save location");
        if (string.IsNullOrEmpty(path)) return;

        var data = ScriptableObject.CreateInstance<GpuRoleStyleData>();
        data.generatedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 保存槽位定义（骨骼数据）
        data.slotDefs = new List<GpuRoleSlotDefData>();
        foreach (var s in _core.SlotDefinitions)
        {
            data.slotDefs.Add(new GpuRoleSlotDefData
            {
                slotId = s.slotId,
                slotKey = s.slotKey,
                slotName = s.slotName,
                objectName = s.objectName,
                path = s.path,
                parentPath = s.parentPath,
                depth = s.depth,
                activeSelf = s.activeSelf,
                activeInHierarchy = s.activeInHierarchy,
                rendererEnabled = s.rendererEnabled,
                defaultVisible = s.defaultVisible,
                sortingLayerId = s.sortingLayerId,
                sortingLayerName = s.sortingLayerName,
                sortingOrder = s.sortingOrder,
                color = s.color,
                spriteName = s.spriteName,
                spriteAssetPath = s.spriteAssetPath,
                spriteGuid = s.spriteGuid,
                spriteRectSize = s.spriteRectSize,
                spritePivotPixels = s.spritePivotPixels,
                spritePivotNormalized = s.spritePivotNormalized,
                spriteBoundsSize = s.spriteBoundsSize,
                pixelsPerUnit = s.pixelsPerUnit,
                localPosition = s.localPosition,
                localEulerAngles = s.localEulerAngles,
                localScale = s.localScale,
                bindPose00 = s.bindPoseToRoot.m00,
                bindPose01 = s.bindPoseToRoot.m10,
                bindPose02 = s.bindPoseToRoot.m20,
                bindPose03 = s.bindPoseToRoot.m30,
                bindPose10 = s.bindPoseToRoot.m01,
                bindPose11 = s.bindPoseToRoot.m11,
                bindPose12 = s.bindPoseToRoot.m21,
                bindPose13 = s.bindPoseToRoot.m31,
                bindPose20 = s.bindPoseToRoot.m02,
                bindPose21 = s.bindPoseToRoot.m12,
                bindPose22 = s.bindPoseToRoot.m22,
                bindPose23 = s.bindPoseToRoot.m32,
                bindPose30 = s.bindPoseToRoot.m03,
                bindPose31 = s.bindPoseToRoot.m13,
                bindPose32 = s.bindPoseToRoot.m23,
                bindPose33 = s.bindPoseToRoot.m33,
                                maskInteraction = s.maskInteraction,
                drawOrder = s.drawOrder,
                internalOrder = s.internalOrder
            });
        }

                // 保存样式槽位
        data.slots = new List<GpuRoleStyleSlot>();
        foreach (var s in _core.StyleSlots)
            data.slots.Add(new GpuRoleStyleSlot
            {
                slotKey = s.slotKey,
                slotName = s.slotName,
                aliasName = s.aliasName,
                spriteFolder = s.spriteFolder,
                sprite = s.sprite,
                color = s.color,
                linkedGroupId = s.linkedGroupId,
                linkedSubSpriteName = s.linkedSubSpriteName,
                exclusiveGroupId = s.exclusiveGroupId
            });

        // 保存组数据
        data.groups = new List<GpuRoleLinkedGroup>();
        foreach (var g in _core.Groups)
        {
            Sprite groupSprite = _core.GroupSprites.ContainsKey(g.groupId) ? _core.GroupSprites[g.groupId] : null;
            data.groups.Add(new GpuRoleLinkedGroup
            {
                groupId = g.groupId,
                groupName = g.groupName,
                groupSprite = groupSprite,
                groupSpritePath = g.groupSpritePath,
                groupSpriteFolder = g.groupSpriteFolder
            });
        }

        // 保存互斥组数据
        data.exclusiveGroups = new List<GpuRoleExclusiveGroupData>();
        foreach (var eg in _core.ExclusiveGroups)
        {
            data.exclusiveGroups.Add(new GpuRoleExclusiveGroupData
            {
                exclusiveGroupId = eg.exclusiveGroupId,
                groupName = eg.groupName,
                memberGroupIds = new List<int>(eg.memberGroupIds),
                memberSlotIndices = new List<int>(eg.memberSlotIndices)
            });
        }

        // 如果文件已存在，先删除再创建（实现覆盖）
        var existing = AssetDatabase.LoadAssetAtPath<GpuRoleStyleData>(path);
        if (existing != null)
        {
            EditorUtility.SetDirty(data);
            // 复制数据到已有 asset
            EditorUtility.CopySerialized(data, existing);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = existing;
            _messages.Add($"Overwritten: {path}");
        }
        else
        {
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = data;
            _messages.Add($"Saved: {path}");
        }
    }

    private void LoadStyleAsset()
    {
        var path = EditorUtility.OpenFilePanel("Select Style Asset", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;

        var rel = GetRelativePath(path);
        var data = AssetDatabase.LoadAssetAtPath<GpuRoleStyleData>(rel);
        if (data == null) { _messages.Add("Failed to load."); return; }

        _sourceStyleAsset = data;
        LoadFromStyleAsset(data);
    }

    private void LoadFromStyleAsset(GpuRoleStyleData data)
    {
        if (data == null) { _messages.Add("Style asset is null."); return; }

        // 从 asset 中恢复槽位定义（骨骼数据），完全自包含，不依赖 Prefab
        if (data.slotDefs == null || data.slotDefs.Count == 0)
        {
            _messages.Add("Style asset has no slot definitions.");
            return;
        }

        // 从 asset 的 slotDefs 重建 GpuRoleSlot 列表
        var slotDefs = new List<GpuRoleSlot>();
        foreach (var sd in data.slotDefs)
        {
            slotDefs.Add(new GpuRoleSlot
            {
                slotId = sd.slotId,
                slotKey = sd.slotKey,
                slotName = sd.slotName,
                objectName = sd.objectName,
                path = sd.path,
                parentPath = sd.parentPath,
                depth = sd.depth,
                activeSelf = sd.activeSelf,
                activeInHierarchy = sd.activeInHierarchy,
                rendererEnabled = sd.rendererEnabled,
                defaultVisible = sd.defaultVisible,
                sortingLayerId = sd.sortingLayerId,
                sortingLayerName = sd.sortingLayerName,
                sortingOrder = sd.sortingOrder,
                color = sd.color,
                spriteName = sd.spriteName,
                spriteAssetPath = sd.spriteAssetPath,
                spriteGuid = sd.spriteGuid,
                spriteRectSize = sd.spriteRectSize,
                spritePivotPixels = sd.spritePivotPixels,
                spritePivotNormalized = sd.spritePivotNormalized,
                spriteBoundsSize = sd.spriteBoundsSize,
                pixelsPerUnit = sd.pixelsPerUnit,
                localPosition = sd.localPosition,
                localEulerAngles = sd.localEulerAngles,
                localScale = sd.localScale,
                bindPoseToRoot = new Matrix4x4(
                    new Vector4(sd.bindPose00, sd.bindPose01, sd.bindPose02, sd.bindPose03),
                    new Vector4(sd.bindPose10, sd.bindPose11, sd.bindPose12, sd.bindPose13),
                    new Vector4(sd.bindPose20, sd.bindPose21, sd.bindPose22, sd.bindPose23),
                    new Vector4(sd.bindPose30, sd.bindPose31, sd.bindPose32, sd.bindPose33)
                ),
                                maskInteraction = sd.maskInteraction,
                drawOrder = sd.drawOrder,
                internalOrder = sd.internalOrder
            });
        }

        // 设置到 core（不设置 SourcePrefab，数据完全来自 asset）
        _core.SlotManager.SetSlotDefinitionsDirect(slotDefs);

        // 从 data.slots 重建 styleSlots（按 slotKey 匹配）
        var styleSlots = new List<GpuRoleStyleSlot>();
        foreach (var sd in slotDefs)
        {
            var saved = data.slots?.FirstOrDefault(s => s.slotKey == sd.slotKey);
                        styleSlots.Add(new GpuRoleStyleSlot
            {
                slotKey = sd.slotKey,
                slotName = sd.slotName,
                aliasName = saved?.aliasName ?? sd.slotKey,
                spriteFolder = saved?.spriteFolder ?? "",
                sprite = saved?.sprite,
                color = saved?.color ?? Color.white,
                linkedGroupId = saved?.linkedGroupId ?? -1,
                linkedSubSpriteName = saved?.linkedSubSpriteName ?? sd.slotName,
                exclusiveGroupId = saved?.exclusiveGroupId ?? -1
            });
        }
        _core.SlotManager.SetStyleSlotsDirect(styleSlots);

        // 重置组和互斥组
        _core.GroupManager.Reset();
        _core.ExclusiveManager.Reset();

        // 恢复组数据
        Dictionary<int, int> groupIdMap = new Dictionary<int, int>(); // 旧ID -> 新ID
        if (data.groups != null && data.groups.Count > 0)
        {
            foreach (var g in data.groups)
            {
                int newId = _core.CreateGroup(g.groupName);
                groupIdMap[g.groupId] = newId;
                if (g.groupSprite != null)
                {
                    _core.SetGroupSpritePath(newId, g.groupSpritePath);
                    _core.TryApplyGroupSpriteToSlots(newId, g.groupSprite, out _);
                }
                if (!string.IsNullOrEmpty(g.groupSpriteFolder))
                    _core.SetGroupSpriteFolder(newId, g.groupSpriteFolder);
            }

            // 重新映射 styleSlots 中的 linkedGroupId
            for (int i = 0; i < _core.StyleSlots.Count; i++)
            {
                int oldId = _core.StyleSlots[i].linkedGroupId;
                if (oldId >= 0 && groupIdMap.ContainsKey(oldId))
                    _core.StyleSlots[i].linkedGroupId = groupIdMap[oldId];
            }
        }

        // 恢复互斥组数据
        _core.ExclusiveManager.Reset();
        if (data.exclusiveGroups != null && data.exclusiveGroups.Count > 0)
        {
            foreach (var eg in data.exclusiveGroups)
            {
                int newId = _core.ExclusiveManager.CreateExclusiveGroup();
                var entry = _core.ExclusiveManager.ExclusiveGroups.FirstOrDefault(e => e.exclusiveGroupId == newId);
                if (entry != null)
                {
                    entry.exclusiveGroupId = eg.exclusiveGroupId;
                    entry.groupName = eg.groupName;

                    // 重新映射 memberGroupIds（使用 groupIdMap）
                    entry.memberGroupIds.Clear();
                    foreach (var oldGroupId in eg.memberGroupIds)
                    {
                        if (groupIdMap.ContainsKey(oldGroupId))
                            entry.memberGroupIds.Add(groupIdMap[oldGroupId]);
                    }

                    entry.memberSlotIndices = new List<int>(eg.memberSlotIndices);
                }
                if (eg.exclusiveGroupId >= _core.ExclusiveManager.NextExclusiveGroupId)
                    _core.ExclusiveManager.NextExclusiveGroupId = eg.exclusiveGroupId + 1;
            }
        }
        else
        {
            // 兼容旧数据：从槽位数据重建互斥组
            _core.ExclusiveManager.RebuildFromSlotData(_core.StyleSlots);
        }

        // 应用互斥逻辑：遍历所有互斥组，对每个有 sprite 的成员应用互斥
        foreach (var eg in _core.ExclusiveGroups)
        {
            foreach (var gId in eg.memberGroupIds)
            {
                _core.ApplyGroupExclusive(gId);
            }
            foreach (var idx in eg.memberSlotIndices)
            {
                _core.ApplySlotExclusive(idx);
            }
        }

        RebuildPreview();
        AutoSave();
        _messages.Add($"Loaded: {data.name}");
        Repaint();
    }

        private string GetRelativePath(string fullPath)
    {
        return GpuRoleUtility.GetRelativePath(fullPath);
    }
}
