using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GPU PaperDoll 角色代理组件
/// 用户通过此组件控制角色的动画、换装、显隐
/// </summary>
public class GpuRoleAgent : MonoBehaviour
{
    public GpuRoleExportData exportData;

    [Header("动画")]
    public int animIndex = 0;
    public float playbackSpeed = 1f;
    public bool playOnEnable = true;

    [Header("初始 Group Variants")]
    public string[] initialGroupVariants = new string[0]; // 按 group 顺序存 variantName

    [Header("初始独立 Slot SpriteId")]
    public int[] initialIndependentSlotSpriteIds = new int[0]; // 按独立 slot 顺序存 spriteId

    [Header("颜色")]
    public Color color = Color.white;

    [Header("缩放")]
    public float scale = 1f;

    [Header("调试")]
    public bool showDebugLog = false;

    // 内部状态
    [System.NonSerialized] public GpuRoleManager manager;
    [System.NonSerialized] public bool allDirty = true;
    [System.NonSerialized] public bool slotDirty = true;
    [System.NonSerialized] public bool frameDirty = true;

    // 当前动画状态
    private AnimExportData _anim;
    private int _currentFrame;
    private float _timer;
    private int _animIndex = -1;

    // Slot 状态
    private int[] _slotSpriteIds;          // 当前每个 slot 的 spriteId（按 exportData.slots 索引）
    private bool[] _slotVisible;           // 每个 slot 是否可见
    private Dictionary<int, int> _groupVariantIndices = new Dictionary<int, int>(); // groupId → variantIndex

    // 映射
    private Dictionary<string, int> _slotIndexByKey = new Dictionary<string, int>();
    private Dictionary<int, int> _slotToGroupMap = new Dictionary<int, int>(); // slotIndex → groupId

    // 预计算的每角色每 Slot 数据
    private struct PerSlotData
    {
        public int spriteId;
        public int internalOrder;
        public bool visible;
    }
    private PerSlotData[] _perSlotCache;

    private void Awake()
    {
        if (exportData == null)
        {
            Debug.LogError("[GpuRoleAgent] 缺少 ExportData");
            enabled = false;
            return;
        }

        // 建立 SlotKey 映射
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            _slotIndexByKey[exportData.slots[i].slotKey] = i;
        }

        // 建立 Slot → Group 映射
        for (int g = 0; g < exportData.groups.Count; g++)
        {
            var group = exportData.groups[g];
            for (int si = 0; si < group.slotIndices.Length; si++)
            {
                _slotToGroupMap[group.slotIndices[si]] = g;
            }
        }

        // 初始化 slot 状态
        _slotSpriteIds = new int[exportData.slots.Count];
        _slotVisible = new bool[exportData.slots.Count];
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            var slot = exportData.slots[i];
            _slotSpriteIds[i] = slot.defaultSpriteId;
            _slotVisible[i] = true;
        }

        // 初始化 group variant
        _groupVariantIndices.Clear();
        for (int g = 0; g < exportData.groups.Count; g++)
        {
            _groupVariantIndices[exportData.groups[g].groupId] = -1;
        }

        // 设置初始动画
        SetAnimation(animIndex);

        // 应用初始 Group Variants
        ApplyInitialGroupVariants();

        // 应用初始独立 Slot
        ApplyInitialIndependentSlots();
    }

    private void OnEnable()
    {
        if (manager == null)
        {
            manager = Object.FindObjectOfType<GpuRoleManager>();
            if (manager == null)
            {
                Debug.LogError("[GpuRoleAgent] 场景中没有 GpuRoleManager");
                return;
            }
        }

        // 初始化缓存
        if (manager.currentAnim == null && _anim != null)
        {
            manager.InitializeCache(exportData, _anim);
        }

        manager.Register(this);
        allDirty = true;
        slotDirty = true;
        frameDirty = true;

        if (playOnEnable)
            _timer = 0;
    }

    private void OnDisable()
    {
        if (manager != null)
            manager.Unregister(this);
    }

    private void Update()
    {
        if (_anim == null || _anim.frames == null || _anim.frames.Count == 0) return;

        _timer += Time.deltaTime * playbackSpeed;
        float frameDuration = 1f / _anim.frameRate;

        if (_timer >= frameDuration)
        {
            _timer -= frameDuration;
            _currentFrame++;
            if (_currentFrame >= _anim.frames.Count)
                _currentFrame = 0;

            frameDirty = true;
        }
    }

    #region 公开 API

    /// <summary>
    /// 播放指定动画
    /// </summary>
    public void Play(int index)
    {
        SetAnimation(index);
        allDirty = true;
    }

    /// <summary>
    /// 按名称播放动画
    /// </summary>
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
        Debug.LogWarning($"[GpuRoleAgent] 未找到动画: {animName}");
    }

    /// <summary>
    /// 设置 Group 联动换装
    /// </summary>
    public void SetGroupVariant(int groupId, int variantIndex)
    {
        if (exportData == null) return;
        var group = FindGroupById(groupId);
        if (group == null)
        {
            Debug.LogWarning($"[GpuRoleAgent] 未找到 Group: {groupId}");
            return;
        }
        SetGroupVariantInternal(group, variantIndex);
    }

    /// <summary>
    /// 按名称设置 Group 联动换装。variantName 为空字符串表示隐藏整组。
    /// </summary>
    public void SetGroupVariant(string groupName, string variantName)
    {
        if (exportData == null) return;

        for (int g = 0; g < exportData.groups.Count; g++)
        {
            if (exportData.groups[g].groupName == groupName)
            {
                var group = exportData.groups[g];

                // 空字符串表示隐藏整组
                if (string.IsNullOrEmpty(variantName))
                {
                    for (int i = 0; i < group.slotIndices.Length; i++)
                    {
                        int slotIdx = group.slotIndices[i];
                        _slotVisible[slotIdx] = false;
                    }
                    if (_groupVariantIndices.ContainsKey(group.groupId))
                        _groupVariantIndices[group.groupId] = -1;
                    slotDirty = true;
                    if (showDebugLog)
                        Debug.Log($"[GpuRoleAgent] Group {group.groupName} -> None (隐藏)");
                    return;
                }

                for (int v = 0; v < group.variants.Count; v++)
                {
                    if (group.variants[v].variantName == variantName)
                    {
                        SetGroupVariantInternal(group, v);
                        return;
                    }
                }
                Debug.LogWarning($"[GpuRoleAgent] Group {groupName} 未找到 Variant: {variantName}");
                return;
            }
        }
        Debug.LogWarning($"[GpuRoleAgent] 未找到 Group: {groupName}");
    }

    /// <summary>
    /// 设置单个 Slot 的 Sprite（如果 slot 属于 group，需要 force=true）
    /// </summary>
    public void SetSlotSprite(string slotKey, int spriteId, bool force = false)
    {
        if (!_slotIndexByKey.TryGetValue(slotKey, out int slotIdx))
        {
            Debug.LogWarning($"[GpuRoleAgent] 未找到 Slot: {slotKey}");
            return;
        }

        if (!force && _slotToGroupMap.ContainsKey(slotIdx))
        {
            Debug.LogWarning($"[GpuRoleAgent] Slot {slotKey} 属于 Group，请使用 SetGroupVariant 换装。如需强制单独修改，请设置 force=true");
            return;
        }

        _slotSpriteIds[slotIdx] = spriteId;
        _slotVisible[slotIdx] = spriteId >= 0;
        slotDirty = true;
    }

    /// <summary>
    /// 设置 Slot 显隐
    /// </summary>
    public void SetSlotVisible(string slotKey, bool visible, bool force = false)
    {
        if (!_slotIndexByKey.TryGetValue(slotKey, out int slotIdx))
        {
            Debug.LogWarning($"[GpuRoleAgent] 未找到 Slot: {slotKey}");
            return;
        }

        if (!force && _slotToGroupMap.ContainsKey(slotIdx))
        {
            Debug.LogWarning($"[GpuRoleAgent] Slot {slotKey} 属于 Group，请使用 SetGroupVariant 控制显隐。如需强制，请设置 force=true");
            return;
        }

        _slotVisible[slotIdx] = visible;
        slotDirty = true;
    }

    /// <summary>
    /// 设置角色颜色
    /// </summary>
    public void SetColor(Color c)
    {
        color = c;
        frameDirty = true;
    }

    /// <summary>
    /// 设置角色缩放
    /// </summary>
    public void SetScale(float s)
    {
        scale = s;
        frameDirty = true;
    }

    /// <summary>
    /// 获取当前 slot 的 spriteId 数组（按 exportData.slots 索引）
    /// </summary>
    public int[] GetCurrentSlotSpriteIds()
    {
        return _slotSpriteIds;
    }

    /// <summary>
    /// 获取当前 slot 的可见性数组
    /// </summary>
    public bool[] GetCurrentSlotVisible()
    {
        return _slotVisible;
    }

    /// <summary>
    /// 填充 instance 数据到 Manager
    /// </summary>
    public void FillInstanceData(int frameIndex, GpuRoleManager mgr)
    {
        if (_anim == null || exportData == null) return;

        int slotCount = _anim.slotKeys.Count;
        Vector3 finalScale = transform.lossyScale * scale;
        Matrix4x4 rootMat = Matrix4x4.TRS(transform.position, transform.rotation, finalScale);

        for (int i = 0; i < slotCount; i++)
        {
            var slotKey = _anim.slotKeys[i];
            if (!_slotIndexByKey.TryGetValue(slotKey.slotKey, out int exportSlotIdx))
                continue;

            int spriteId = _slotSpriteIds[exportSlotIdx];
            if (spriteId < 0 || !_slotVisible[exportSlotIdx]) continue;

            // 只传 rootMatrix
            Matrix4x4 finalMatrix = rootMat;
            finalMatrix.m23 = -exportData.slots[exportSlotIdx].internalOrder * 0.001f;

            Vector4 colorVec = new Vector4(color.r, color.g, color.b, color.a);
            Vector4 instanceData = new Vector4(i, colorVec.x, colorVec.y, colorVec.z);

            mgr.FillInstanceToBatch(spriteId, finalMatrix, colorVec, instanceData, i);
        }
    }

    public int currentFrame => _currentFrame;

    #endregion

    #region 内部方法

    private void SetAnimation(int index)
    {
        if (exportData == null || exportData.animations == null) return;
        if (index < 0 || index >= exportData.animations.Count) return;

        _animIndex = index;
        _anim = exportData.animations[index];
        _currentFrame = 0;
        _timer = 0;
    }

    private void SetGroupVariantInternal(GroupExportData group, int variantIndex)
    {
        if (variantIndex < 0 || variantIndex >= group.variants.Count)
        {
            Debug.LogWarning($"[GpuRoleAgent] Group {group.groupName} Variant 索引 {variantIndex} 超出范围");
            return;
        }

        var variant = group.variants[variantIndex];
        for (int i = 0; i < group.slotIndices.Length && i < variant.spriteIds.Length; i++)
        {
            int slotIdx = group.slotIndices[i];
            if (slotIdx < 0 || slotIdx >= _slotSpriteIds.Length) continue;
            _slotSpriteIds[slotIdx] = variant.spriteIds[i];
            _slotVisible[slotIdx] = variant.spriteIds[i] >= 0;
        }

        if (_groupVariantIndices.ContainsKey(group.groupId))
            _groupVariantIndices[group.groupId] = variantIndex;
        slotDirty = true;

        if (showDebugLog)
            Debug.Log($"[GpuRoleAgent] Group {group.groupName} -> Variant {variant.variantName}");
    }

    private GroupExportData FindGroupById(int groupId)
    {
        for (int g = 0; g < exportData.groups.Count; g++)
        {
            if (exportData.groups[g].groupId == groupId)
                return exportData.groups[g];
        }
        return null;
    }

    private void ApplyInitialGroupVariants()
    {
        if (exportData == null || exportData.groups == null) return;
        for (int g = 0; g < exportData.groups.Count; g++)
        {
            if (g < initialGroupVariants.Length)
            {
                SetGroupVariant(exportData.groups[g].groupName, initialGroupVariants[g]);
            }
        }
    }

    private void ApplyInitialIndependentSlots()
    {
        if (exportData == null || exportData.slots == null) return;

        // 找出独立 slot 索引
        var independentIndices = GetIndependentSlotIndices();
        for (int i = 0; i < independentIndices.Count && i < initialIndependentSlotSpriteIds.Length; i++)
        {
            int slotIdx = independentIndices[i];
            int spriteId = initialIndependentSlotSpriteIds[i];
            if (spriteId >= 0)
            {
                _slotSpriteIds[slotIdx] = spriteId;
                _slotVisible[slotIdx] = true;
            }
            else
            {
                _slotVisible[slotIdx] = false;
            }
        }
        slotDirty = true;
    }

    private List<int> GetIndependentSlotIndices()
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

    #endregion
}
