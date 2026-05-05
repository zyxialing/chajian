using System.Collections.Generic;
using UnityEngine;

public class GpuRoleGpuAgent : MonoBehaviour
{
    public GpuRoleExportData exportData;

    [Header("动画")]
    public int animIndex = 0;
    public float playbackSpeed = 1f;
    public bool playOnEnable = true;

    [Header("初始 Group Variants")]
    public string[] initialGroupVariants = new string[0];

    [Header("初始独立 Slot SpriteId")]
    public int[] initialIndependentSlotSpriteIds = new int[0];

    [Header("颜色")]
    public Color color = Color.white;

    [Header("缩放")]
    public float scale = 1f;

    [Header("调试")]
    public bool showDebugLog = false;

    [System.NonSerialized] public GpuRoleGpuManager manager;
    [System.NonSerialized] public int runtimeIndex = -1;

    private int[] _slotSpriteIds;
    private bool[] _slotVisible;
    private Dictionary<string, int> _slotIndexByKey = new Dictionary<string, int>();
    private Dictionary<int, int> _slotToGroupMap = new Dictionary<int, int>();
    private float _animStartTime;
    private bool _initialized;

    public bool IsInitialized => _initialized;
    public float AnimationStartTime => _animStartTime;
    public int SlotCount => _slotSpriteIds != null ? _slotSpriteIds.Length : 0;
    public int CurrentAnimIndex
    {
        get
        {
            if (exportData == null || exportData.animations == null || exportData.animations.Count == 0)
                return 0;
            return Mathf.Clamp(animIndex, 0, exportData.animations.Count - 1);
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        if (exportData == null) return;

        if (manager == null)
            manager = Object.FindObjectOfType<GpuRoleGpuManager>();

        if (manager == null)
        {
            Debug.LogError("[GpuRoleGpuAgent] 场景中没有 GpuRoleGpuManager");
            return;
        }

        if (playOnEnable)
            _animStartTime = Time.time;

        manager.Register(this);
    }

    private void OnDisable()
    {
        if (manager != null)
            manager.Unregister(this);
    }

    public void EnsureInitialized()
    {
        if (_initialized && _slotSpriteIds != null && exportData != null && _slotSpriteIds.Length == exportData.slots.Count)
            return;

        _initialized = false;
        _slotIndexByKey.Clear();
        _slotToGroupMap.Clear();

        if (exportData == null || exportData.slots == null)
            return;

        for (int i = 0; i < exportData.slots.Count; i++)
            _slotIndexByKey[exportData.slots[i].slotKey] = i;

        if (exportData.groups != null)
        {
            for (int g = 0; g < exportData.groups.Count; g++)
            {
                GroupExportData group = exportData.groups[g];
                if (group.slotIndices == null) continue;
                for (int i = 0; i < group.slotIndices.Length; i++)
                    _slotToGroupMap[group.slotIndices[i]] = g;
            }
        }

        _slotSpriteIds = new int[exportData.slots.Count];
        _slotVisible = new bool[exportData.slots.Count];
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            _slotSpriteIds[i] = exportData.slots[i].defaultSpriteId;
            _slotVisible[i] = true;
        }

        _initialized = true;
        ApplyInitialGroupVariants();
        ApplyInitialIndependentSlots();

        animIndex = CurrentAnimIndex;
        _animStartTime = Time.time;
    }

    public void RebuildInitialState()
    {
        _initialized = false;
        EnsureInitialized();
        manager?.MarkAgentStyleDirty(this);
    }

    public void Play(int index)
    {
        if (exportData == null || exportData.animations == null || exportData.animations.Count == 0) return;
        animIndex = Mathf.Clamp(index, 0, exportData.animations.Count - 1);
        _animStartTime = Time.time;
        manager?.MarkAgentAnimationTopologyDirty(this);
    }

    public void RestartAnimation()
    {
        _animStartTime = Time.time;
        manager?.MarkAgentAnimationDirty(this);
    }

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = speed;
        manager?.MarkAgentAnimationDirty(this);
    }

    public void Play(string animName)
    {
        if (exportData == null || exportData.animations == null) return;
        for (int i = 0; i < exportData.animations.Count; i++)
        {
            if (exportData.animations[i].animName == animName)
            {
                Play(i);
                return;
            }
        }

        Debug.LogWarning($"[GpuRoleGpuAgent] 未找到动画: {animName}");
    }

    public void SetGroupVariant(int groupId, int variantIndex)
    {
        EnsureInitialized();
        GroupExportData group = FindGroupById(groupId);
        if (group == null) return;
        SetGroupVariantInternal(group, variantIndex);
    }

    public void SetGroupVariant(string groupName, string variantName)
    {
        EnsureInitialized();
        if (exportData == null || exportData.groups == null) return;

        for (int g = 0; g < exportData.groups.Count; g++)
        {
            GroupExportData group = exportData.groups[g];
            if (group.groupName != groupName) continue;

            if (string.IsNullOrEmpty(variantName))
            {
                if (group.slotIndices != null)
                {
                    for (int i = 0; i < group.slotIndices.Length; i++)
                    {
                        int slotIndex = group.slotIndices[i];
                        if (slotIndex >= 0 && slotIndex < _slotVisible.Length)
                            _slotVisible[slotIndex] = false;
                    }
                }
                manager?.MarkAgentStyleDirty(this);
                return;
            }

            if (group.variants != null)
            {
                for (int v = 0; v < group.variants.Count; v++)
                {
                    if (group.variants[v].variantName == variantName)
                    {
                        SetGroupVariantInternal(group, v);
                        return;
                    }
                }
            }

            Debug.LogWarning($"[GpuRoleGpuAgent] Group {groupName} 未找到 Variant: {variantName}");
            return;
        }
    }

    public void SetSlotSprite(string slotKey, int spriteId, bool force = false)
    {
        EnsureInitialized();
        if (!_slotIndexByKey.TryGetValue(slotKey, out int slotIndex))
            return;

        if (!force && _slotToGroupMap.ContainsKey(slotIndex))
        {
            Debug.LogWarning($"[GpuRoleGpuAgent] Slot {slotKey} 属于 Group，请使用 SetGroupVariant，或 force=true");
            return;
        }

        _slotSpriteIds[slotIndex] = spriteId;
        _slotVisible[slotIndex] = spriteId >= 0;
        manager?.MarkAgentStyleDirty(this);
    }

    public void SetSlotVisible(string slotKey, bool visible, bool force = false)
    {
        EnsureInitialized();
        if (!_slotIndexByKey.TryGetValue(slotKey, out int slotIndex))
            return;

        if (!force && _slotToGroupMap.ContainsKey(slotIndex))
        {
            Debug.LogWarning($"[GpuRoleGpuAgent] Slot {slotKey} 属于 Group，请使用 SetGroupVariant，或 force=true");
            return;
        }

        _slotVisible[slotIndex] = visible;
        manager?.MarkAgentStyleDirty(this);
    }

    public void SetColor(Color c)
    {
        color = c;
        manager?.MarkAgentVisualDirty(this);
    }

    public void SetScale(float s)
    {
        scale = s;
        manager?.MarkAgentVisualDirty(this);
    }

    public int[] GetCurrentSlotSpriteIds()
    {
        EnsureInitialized();
        return _slotSpriteIds;
    }

    public bool[] GetCurrentSlotVisible()
    {
        EnsureInitialized();
        return _slotVisible;
    }

    public int GetSlotSpriteId(int slotIndex)
    {
        EnsureInitialized();
        if (_slotSpriteIds == null || slotIndex < 0 || slotIndex >= _slotSpriteIds.Length)
            return -1;
        return _slotSpriteIds[slotIndex];
    }

    public bool IsSlotVisible(int slotIndex)
    {
        EnsureInitialized();
        if (_slotVisible == null || slotIndex < 0 || slotIndex >= _slotVisible.Length)
            return false;
        return _slotVisible[slotIndex];
    }

    public bool TryGetExportSlotIndex(string slotKey, out int slotIndex)
    {
        EnsureInitialized();
        return _slotIndexByKey.TryGetValue(slotKey, out slotIndex);
    }

    public AnimExportData GetCurrentAnim()
    {
        if (exportData == null || exportData.animations == null || exportData.animations.Count == 0)
            return null;

        animIndex = CurrentAnimIndex;
        return exportData.animations[animIndex];
    }

    public Vector4 GetGpuAnimState()
    {
        AnimExportData anim = GetCurrentAnim();
        float frameRate = anim != null ? anim.frameRate : 30f;
        float frameCount = anim != null && anim.frames != null ? Mathf.Max(1, anim.frames.Count) : 1;
        return new Vector4(_animStartTime, playbackSpeed, frameRate, frameCount);
    }

    public Vector4 GetGpuAnimExtraState()
    {
        AnimExportData anim = GetCurrentAnim();
        float animTexY = anim != null ? anim.animDataTexY : 0f;
        return new Vector4(animTexY, 0f, 0f, 0f);
    }

    private void SetGroupVariantInternal(GroupExportData group, int variantIndex)
    {
        if (group == null || group.variants == null || variantIndex < 0 || variantIndex >= group.variants.Count)
            return;

        GroupVariant variant = group.variants[variantIndex];
        if (group.slotIndices == null || variant.spriteIds == null)
            return;

        int count = Mathf.Min(group.slotIndices.Length, variant.spriteIds.Length);
        for (int i = 0; i < count; i++)
        {
            int slotIndex = group.slotIndices[i];
            if (slotIndex < 0 || slotIndex >= _slotSpriteIds.Length) continue;

            int spriteId = variant.spriteIds[i];
            _slotSpriteIds[slotIndex] = spriteId;
            _slotVisible[slotIndex] = spriteId >= 0;
        }

        manager?.MarkAgentStyleDirty(this);
    }

    private GroupExportData FindGroupById(int groupId)
    {
        if (exportData == null || exportData.groups == null) return null;
        for (int i = 0; i < exportData.groups.Count; i++)
        {
            if (exportData.groups[i].groupId == groupId)
                return exportData.groups[i];
        }

        return null;
    }

    private void ApplyInitialGroupVariants()
    {
        if (exportData == null || exportData.groups == null || initialGroupVariants == null) return;
        for (int g = 0; g < exportData.groups.Count && g < initialGroupVariants.Length; g++)
            SetGroupVariant(exportData.groups[g].groupName, initialGroupVariants[g]);
    }

    private void ApplyInitialIndependentSlots()
    {
        if (exportData == null || exportData.slots == null || initialIndependentSlotSpriteIds == null) return;

        List<int> independent = GetIndependentSlotIndices();
        for (int i = 0; i < independent.Count && i < initialIndependentSlotSpriteIds.Length; i++)
        {
            int slotIndex = independent[i];
            int spriteId = initialIndependentSlotSpriteIds[i];
            _slotSpriteIds[slotIndex] = spriteId;
            _slotVisible[slotIndex] = spriteId >= 0;
        }
    }

    private List<int> GetIndependentSlotIndices()
    {
        HashSet<int> grouped = new HashSet<int>();
        if (exportData != null && exportData.groups != null)
        {
            for (int g = 0; g < exportData.groups.Count; g++)
            {
                int[] slotIndices = exportData.groups[g].slotIndices;
                if (slotIndices == null) continue;
                for (int i = 0; i < slotIndices.Length; i++)
                    grouped.Add(slotIndices[i]);
            }
        }

        List<int> result = new List<int>();
        if (exportData != null && exportData.slots != null)
        {
            for (int i = 0; i < exportData.slots.Count; i++)
            {
                if (!grouped.Contains(i))
                    result.Add(i);
            }
        }

        return result;
    }
}
