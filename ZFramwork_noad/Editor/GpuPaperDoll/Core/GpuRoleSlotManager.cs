using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GpuRoleSlotManager
{
    private List<GpuRoleSlot> _slotDefinitions = new List<GpuRoleSlot>();
    private List<GpuRoleStyleSlot> _styleSlots = new List<GpuRoleStyleSlot>();

    public List<GpuRoleSlot> SlotDefinitions => _slotDefinitions;
    public List<GpuRoleStyleSlot> StyleSlots => _styleSlots;
    public int Count => _styleSlots.Count;

    public void SetSlotDefinitionsDirect(List<GpuRoleSlot> defs)
    {
        _slotDefinitions = new List<GpuRoleSlot>(defs);
    }

    public void SetStyleSlotsDirect(List<GpuRoleStyleSlot> slots)
    {
        _styleSlots = new List<GpuRoleStyleSlot>(slots);
    }

    public void LoadFromPrefab(GameObject prefab)
    {
        if (prefab == null) return;

        _slotDefinitions.Clear();
        _styleSlots.Clear();

        _slotDefinitions = ScanPrefabSlots(prefab);

        GameObject tempInstance = UnityEngine.Object.Instantiate(prefab);
        SpriteRenderer[] tempRenderers = tempInstance.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < _slotDefinitions.Count; i++)
        {
            Sprite defaultSprite = null;
            Color defaultColor = Color.white;
            if (i < tempRenderers.Length)
            {
                defaultSprite = tempRenderers[i].sprite;
                defaultColor = tempRenderers[i].color;
            }

            _styleSlots.Add(new GpuRoleStyleSlot
            {
                slotKey = _slotDefinitions[i].slotKey,
                slotName = _slotDefinitions[i].slotName,
                spriteFolder = "",
                sprite = defaultSprite,
                color = defaultColor,
                linkedGroupId = -1,
                linkedSubSpriteName = _slotDefinitions[i].slotName,
                exclusiveGroupId = -1,
            });
        }

        UnityEngine.Object.DestroyImmediate(tempInstance);
    }

    public GpuRoleStyleSlot GetSlot(int index)
    {
        return index >= 0 && index < _styleSlots.Count ? _styleSlots[index] : null;
    }

    public List<int> GetSlotIndicesInGroup(int groupId)
    {
        List<int> indices = new List<int>();
        for (int i = 0; i < _styleSlots.Count; i++)
        {
            if (_styleSlots[i].linkedGroupId == groupId)
                indices.Add(i);
        }
        return indices;
    }

    public List<string> GetSlotNamesInGroup(int groupId)
    {
        return GetSlotIndicesInGroup(groupId).Select(i => _styleSlots[i].slotName).ToList();
    }

    public void ClearAllSprites()
    {
        foreach (var slot in _styleSlots)
        {
            slot.sprite = null;
            slot.color = Color.white;
        }
    }

    public void ClearGroupSprites(int groupId)
    {
        for (int i = 0; i < _styleSlots.Count; i++)
        {
            if (_styleSlots[i].linkedGroupId == groupId)
            {
                _styleSlots[i].sprite = null;
                _styleSlots[i].color = Color.white;
            }
        }
    }

    public void RemoveSlotFromGroup(int index)
    {
        if (index >= 0 && index < _styleSlots.Count)
        {
            _styleSlots[index].linkedGroupId = -1;
            _styleSlots[index].linkedSubSpriteName = _styleSlots[index].slotName;
        }
    }

    public void AddSlotToGroup(int index, int groupId, string subSpriteName)
    {
        if (index >= 0 && index < _styleSlots.Count)
        {
            _styleSlots[index].linkedGroupId = groupId;
            _styleSlots[index].linkedSubSpriteName = subSpriteName;
        }
    }

    public void ClearAllGroupAssignments()
    {
        foreach (var slot in _styleSlots)
        {
            slot.linkedGroupId = -1;
            slot.linkedSubSpriteName = slot.slotName;
        }
    }

    public Sprite PickRandomSpriteFromFolder(string folderPath)
    {
        return GpuRoleUtility.PickRandomSpriteFromFolder(folderPath);
    }

    private List<GpuRoleSlot> ScanPrefabSlots(GameObject prefab)
    {
        List<GpuRoleSlot> slots = new List<GpuRoleSlot>();
        Transform root = prefab.transform;
        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            Transform transform = renderer.transform;
            Sprite sprite = renderer.sprite;
            string spritePath = sprite != null ? AssetDatabase.GetAssetPath(sprite) : string.Empty;

            slots.Add(new GpuRoleSlot
            {
                slotId = i,
                slotKey = GpuRoleUtility.BuildSlotKey(root, transform),
                slotName = GpuRoleUtility.BuildSlotName(transform, renderer),
                objectName = transform.name,
                path = GpuRoleUtility.GetPath(root, transform),
                parentPath = transform.parent != null ? GpuRoleUtility.GetPath(root, transform.parent) : string.Empty,
                depth = GpuRoleUtility.GetDepth(root, transform),
                activeSelf = transform.gameObject.activeSelf,
                activeInHierarchy = GpuRoleUtility.IsActiveInHierarchy(transform),
                rendererEnabled = renderer.enabled,
                defaultVisible = transform.gameObject.activeSelf && renderer.enabled && sprite != null,
                sortingLayerId = renderer.sortingLayerID,
                sortingLayerName = renderer.sortingLayerName,
                sortingOrder = renderer.sortingOrder,
                color = renderer.color,
                spriteName = sprite != null ? sprite.name : string.Empty,
                spriteAssetPath = spritePath,
                spriteGuid = !string.IsNullOrEmpty(spritePath) ? AssetDatabase.AssetPathToGUID(spritePath) : string.Empty,
                spriteRectSize = sprite != null ? sprite.rect.size : Vector2.zero,
                spritePivotPixels = sprite != null ? sprite.pivot : Vector2.zero,
                spritePivotNormalized = sprite != null ? new Vector2(sprite.pivot.x / Mathf.Max(1f, sprite.rect.width), sprite.pivot.y / Mathf.Max(1f, sprite.rect.height)) : Vector2.zero,
                spriteBoundsSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.zero,
                pixelsPerUnit = sprite != null ? sprite.pixelsPerUnit : 0f,
                localPosition = transform.localPosition,
                localEulerAngles = transform.localEulerAngles,
                localScale = transform.localScale,
                bindPoseToRoot = root.worldToLocalMatrix * transform.localToWorldMatrix,
                maskInteraction = renderer.maskInteraction,
                drawOrder = i,
                internalOrder = renderer.sortingOrder * GpuRoleUtility.InternalOrderStep + i,
            });
        }
        return slots;
    }
}
