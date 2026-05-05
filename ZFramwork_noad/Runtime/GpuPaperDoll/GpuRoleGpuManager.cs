using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GpuRoleGpuManager : MonoBehaviour
{
    public Shader shader;
    public Camera targetCamera;

    [Header("Performance")]
    public int maxCharacterCount = 2000;
    public float pixelsPerUnit = 32f;
    public bool useInternalOrderSorting = true;
    public bool compressInternalOrder = true;
    [Range(0f, 1f)] public float alphaClipThreshold = 0.01f;
    public Vector3 drawBoundsCenter = Vector3.zero;
    public Vector3 drawBoundsSize = new Vector3(10000f, 10000f, 10000f);

    [Header("Depth Sorting")]
    public bool useYDepthSorting;
    public bool lowerYIsCloser = true;
    public bool preserveAgentZ = true;
    public float yToZScale = 0.01f;
    public float depthBaseZ = 0f;
    public bool writeDepth;

    [Header("Debug")]
    public bool showRuntimeStats;
    [SerializeField] private int agentCount;
    [SerializeField] private int batchCount;
    [SerializeField] private int instanceCount;
    [SerializeField] private int rebuildCount;

    private readonly List<GpuRoleGpuAgent> _agents = new List<GpuRoleGpuAgent>();
    private readonly Dictionary<int, SpriteUVData> _uvBySpriteId = new Dictionary<int, SpriteUVData>();
    private readonly Dictionary<GpuRoleExportData, Dictionary<string, int>> _slotIndexCache = new Dictionary<GpuRoleExportData, Dictionary<string, int>>();
    private readonly Dictionary<GpuRoleExportData, int[]> _compressedOrderCache = new Dictionary<GpuRoleExportData, int[]>();
    private readonly Dictionary<AnimExportData, int[]> _animSlotToExportSlotCache = new Dictionary<AnimExportData, int[]>();
    private readonly Dictionary<BatchKey, AtlasBatch> _batchMap = new Dictionary<BatchKey, AtlasBatch>();
    private readonly List<AtlasBatch> _batches = new List<AtlasBatch>();

    private Material _material;
    private Mesh _quadMesh;
    private Matrix4x4[] _agentMatrices;
    private Vector4[] _agentAnimData;
    private Vector4[] _agentAnimExtraData;
    private Vector4[] _agentColors;
    private ComputeBuffer _agentMatrixBuffer;
    private ComputeBuffer _agentAnimBuffer;
    private ComputeBuffer _agentAnimExtraBuffer;
    private ComputeBuffer _agentColorBuffer;
    private bool _topologyDirty = true;
    private bool _agentAnimDataDirty = true;
    private bool _agentColorDataDirty = true;
    private float _lastAppliedAlphaClipThreshold = -1f;
    private int _lastAppliedWriteDepth = -1;

    private struct BatchKey : System.IEquatable<BatchKey>
    {
        public int atlasIndex;
        public int animIndex;
        public int internalOrder;

        public BatchKey(int atlasIndex, int animIndex, int internalOrder)
        {
            this.atlasIndex = atlasIndex;
            this.animIndex = animIndex;
            this.internalOrder = internalOrder;
        }

        public bool Equals(BatchKey other)
        {
            return atlasIndex == other.atlasIndex &&
                   animIndex == other.animIndex &&
                   internalOrder == other.internalOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is BatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = atlasIndex;
                hash = (hash * 397) ^ animIndex;
                hash = (hash * 397) ^ internalOrder;
                return hash;
            }
        }
    }

    private class AtlasBatch
    {
        public BatchKey key;
        public MaterialPropertyBlock mpb;
        public int count;
        public ComputeBuffer instanceDataBuffer;
        public ComputeBuffer uvBuffer;
        public ComputeBuffer spriteMatrixBuffer;
        public ComputeBuffer argsBuffer;
    }

    private struct InstanceBuildData
    {
        public int agentIndex;
        public int animSlotIndex;
        public int internalOrder;
        public SpriteUVData uv;
    }

    private void Awake()
    {
        if (shader == null)
            shader = Shader.Find("GpuPaperDoll/GpuRuntime");

        if (shader == null)
        {
            Debug.LogError("[GpuRoleGpuManager] Missing shader: GpuPaperDoll/GpuRuntime");
            enabled = false;
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        _material = new Material(shader);
        _quadMesh = CreateQuadMesh();
        EnsureAgentBuffers();
    }

    public void Register(GpuRoleGpuAgent agent)
    {
        if (agent == null || _agents.Contains(agent)) return;
        if (_agents.Count >= maxCharacterCount)
        {
            Debug.LogError($"[GpuRoleGpuManager] Agent count exceeds maxCharacterCount: {maxCharacterCount}");
            return;
        }

        agent.EnsureInitialized();
        agent.runtimeIndex = _agents.Count;
        _agents.Add(agent);
        agent.manager = this;
        CacheExportData(agent.exportData);
        EnsureAgentBuffers();
        _agentAnimDataDirty = true;
        _agentColorDataDirty = true;
        _topologyDirty = true;
    }

    public void Unregister(GpuRoleGpuAgent agent)
    {
        if (agent == null) return;
        int index = _agents.IndexOf(agent);
        if (index < 0) return;

        _agents.RemoveAt(index);
        if (agent.manager == this)
            agent.manager = null;
        agent.runtimeIndex = -1;
        ReassignRuntimeIndices();
        _agentAnimDataDirty = true;
        _agentColorDataDirty = true;
        _topologyDirty = true;
    }

    public void MarkAgentStyleDirty(GpuRoleGpuAgent agent)
    {
        _topologyDirty = true;
    }

    public void MarkAgentAnimationDirty(GpuRoleGpuAgent agent)
    {
        _agentAnimDataDirty = true;
    }

    public void MarkAgentAnimationTopologyDirty(GpuRoleGpuAgent agent)
    {
        _agentAnimDataDirty = true;
        _topologyDirty = true;
    }

    public void MarkAgentVisualDirty(GpuRoleGpuAgent agent)
    {
        _agentColorDataDirty = true;
    }

    public int AgentCount => _agents.Count;
    public int BatchCount => _batches.Count;
    public int InstanceCount => instanceCount;
    public int RebuildCount => rebuildCount;

    private void LateUpdate()
    {
        if (_material == null || _quadMesh == null || targetCamera == null)
            return;

        EnsureAgentBuffers();

        if (_topologyDirty)
            RebuildBatches();

        UpdateBatchMaterialProperties();

        UploadAgentBuffers();

        agentCount = _agents.Count;
        batchCount = _batches.Count;

        Bounds bounds = new Bounds(drawBoundsCenter, drawBoundsSize);
        for (int i = 0; i < _batches.Count; i++)
        {
            AtlasBatch batch = _batches[i];
            if (batch.count == 0) continue;

            Graphics.DrawMeshInstancedIndirect(
                _quadMesh,
                0,
                _material,
                bounds,
                batch.argsBuffer,
                0,
                batch.mpb,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                targetCamera
            );
        }
    }

    private void RebuildBatches()
    {
        ReleaseBatches();

        Dictionary<BatchKey, List<InstanceBuildData>> grouped = new Dictionary<BatchKey, List<InstanceBuildData>>();
        for (int a = 0; a < _agents.Count; a++)
        {
            GpuRoleGpuAgent agent = _agents[a];
            if (agent == null || !agent.isActiveAndEnabled || agent.exportData == null)
                continue;

            CacheExportData(agent.exportData);
            agent.EnsureInitialized();

            AnimExportData anim = agent.GetCurrentAnim();
            if (anim == null || anim.slotKeys == null)
                continue;

            int[] spriteIds = agent.GetCurrentSlotSpriteIds();
            bool[] visible = agent.GetCurrentSlotVisible();
            if (spriteIds == null || visible == null)
                continue;

            int[] animSlotToExportSlot = GetAnimSlotToExportSlotMap(agent.exportData, anim);
            if (animSlotToExportSlot == null)
                continue;

            for (int animSlotIndex = 0; animSlotIndex < anim.slotKeys.Count; animSlotIndex++)
            {
                int exportSlotIndex = animSlotIndex < animSlotToExportSlot.Length ? animSlotToExportSlot[animSlotIndex] : -1;
                if (exportSlotIndex < 0)
                    continue;

                if (exportSlotIndex < 0 || exportSlotIndex >= spriteIds.Length || exportSlotIndex >= visible.Length)
                    continue;

                int spriteId = spriteIds[exportSlotIndex];
                if (spriteId < 0 || !visible[exportSlotIndex])
                    continue;

                if (!_uvBySpriteId.TryGetValue(spriteId, out SpriteUVData uv))
                    continue;

                int internalOrder = useInternalOrderSorting ? GetBatchOrder(agent.exportData, exportSlotIndex) : 0;
                int animBatchIndex = UsesCombinedAnimTexture(agent.exportData) ? 0 : agent.CurrentAnimIndex;
                BatchKey key = new BatchKey(uv.atlasIndex, animBatchIndex, internalOrder);
                if (!grouped.TryGetValue(key, out List<InstanceBuildData> list))
                {
                    list = new List<InstanceBuildData>();
                    grouped.Add(key, list);
                }

                list.Add(new InstanceBuildData
                {
                    agentIndex = agent.runtimeIndex,
                    animSlotIndex = animSlotIndex,
                    internalOrder = internalOrder,
                    uv = uv
                });
            }
        }

        foreach (var kv in grouped)
            CreateBatch(kv.Key, kv.Value);

        if (useInternalOrderSorting)
        {
            _batches.Sort((a, b) =>
            {
                int order = a.key.internalOrder.CompareTo(b.key.internalOrder);
                if (order != 0) return order;

                int anim = a.key.animIndex.CompareTo(b.key.animIndex);
                if (anim != 0) return anim;

                return a.key.atlasIndex.CompareTo(b.key.atlasIndex);
            });
        }

        instanceCount = 0;
        for (int i = 0; i < _batches.Count; i++)
            instanceCount += _batches[i].count;
        rebuildCount++;
        _topologyDirty = false;
    }

    private void CreateBatch(BatchKey key, List<InstanceBuildData> instances)
    {
        if (instances == null || instances.Count == 0)
            return;

        GpuRoleExportData exportData = FindExportDataForBatch(key.animIndex);
        if (exportData == null || exportData.atlases == null || key.atlasIndex < 0 || key.atlasIndex >= exportData.atlases.Count)
            return;

        AtlasData atlas = exportData.atlases[key.atlasIndex];
        if (atlas == null || atlas.texture == null)
            return;

        AnimExportData anim = exportData.animations[key.animIndex];
        Texture2D animTex = GetRuntimeAnimTexture(exportData, anim);
        if (anim == null || animTex == null)
            return;

        int count = instances.Count;
        Vector4[] instanceData = new Vector4[count];
        Vector4[] uvRects = new Vector4[count];
        Matrix4x4[] spriteMatrices = new Matrix4x4[count];

        for (int i = 0; i < count; i++)
        {
            InstanceBuildData instance = instances[i];
            instanceData[i] = new Vector4(instance.agentIndex, instance.animSlotIndex, 0f, 0f);
            uvRects[i] = new Vector4(instance.uv.uMin, instance.uv.vMin, instance.uv.uMax, instance.uv.vMax);
            spriteMatrices[i] = CreateSpriteMatrix(instance.uv);
        }

        AtlasBatch batch = new AtlasBatch
        {
            key = key,
            count = count,
            mpb = new MaterialPropertyBlock(),
            instanceDataBuffer = new ComputeBuffer(count, sizeof(float) * 4),
            uvBuffer = new ComputeBuffer(count, sizeof(float) * 4),
            spriteMatrixBuffer = new ComputeBuffer(count, sizeof(float) * 16),
            argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments)
        };

        batch.instanceDataBuffer.SetData(instanceData);
        batch.uvBuffer.SetData(uvRects);
        batch.spriteMatrixBuffer.SetData(spriteMatrices);

        uint[] args = new uint[5];
        args[0] = _quadMesh.GetIndexCount(0);
        args[1] = (uint)count;
        args[2] = _quadMesh.GetIndexStart(0);
        args[3] = _quadMesh.GetBaseVertex(0);
        args[4] = 0;
        batch.argsBuffer.SetData(args);

        batch.mpb.SetTexture("_MainTex", atlas.texture);
        batch.mpb.SetTexture("_AnimTex", animTex);
        batch.mpb.SetFloat("_AlphaClipThreshold", alphaClipThreshold);
        batch.mpb.SetFloat("_AnimSlotCount", GetRuntimeAnimTexWidth(exportData, anim) / 3f);
        batch.mpb.SetFloat("_AnimTexHeight", GetRuntimeAnimTexHeight(exportData, anim));
        batch.mpb.SetBuffer("_AgentMatrices", _agentMatrixBuffer);
        batch.mpb.SetBuffer("_AgentAnimData", _agentAnimBuffer);
        batch.mpb.SetBuffer("_AgentAnimExtraData", _agentAnimExtraBuffer);
        batch.mpb.SetBuffer("_AgentColors", _agentColorBuffer);
        batch.mpb.SetBuffer("_InstanceData", batch.instanceDataBuffer);
        batch.mpb.SetBuffer("_InstanceUVRects", batch.uvBuffer);
        batch.mpb.SetBuffer("_InstanceSpriteMatrices", batch.spriteMatrixBuffer);

        _batchMap[key] = batch;
        _batches.Add(batch);
    }

    private void UploadAgentBuffers()
    {
        for (int i = 0; i < _agents.Count; i++)
        {
            GpuRoleGpuAgent agent = _agents[i];
            if (agent == null)
                continue;

            Vector3 finalScale = agent.transform.lossyScale * agent.scale;
            Vector3 position = agent.transform.position;
            if (useYDepthSorting)
                position.z = CalculateDepthZ(position);

            _agentMatrices[i] = Matrix4x4.TRS(position, agent.transform.rotation, finalScale);

            if (_agentAnimDataDirty)
            {
                _agentAnimData[i] = agent.GetGpuAnimState();
                _agentAnimExtraData[i] = agent.GetGpuAnimExtraState();
            }

            if (_agentColorDataDirty)
            {
                Color c = agent.color;
                _agentColors[i] = new Vector4(c.r, c.g, c.b, c.a);
            }
        }

        int count = Mathf.Min(_agents.Count, _agentMatrices.Length);
        if (count <= 0)
            return;

        _agentMatrixBuffer.SetData(_agentMatrices, 0, 0, count);

        if (_agentAnimDataDirty)
        {
            _agentAnimBuffer.SetData(_agentAnimData, 0, 0, count);
            _agentAnimExtraBuffer.SetData(_agentAnimExtraData, 0, 0, count);
            _agentAnimDataDirty = false;
        }

        if (_agentColorDataDirty)
        {
            _agentColorBuffer.SetData(_agentColors, 0, 0, count);
            _agentColorDataDirty = false;
        }
    }

    private void EnsureAgentBuffers()
    {
        int capacity = Mathf.Max(1, maxCharacterCount);
        if (_agentMatrixBuffer != null && _agentMatrices != null && _agentMatrices.Length == capacity)
            return;

        ReleaseAgentBuffers();

        _agentMatrices = new Matrix4x4[capacity];
        _agentAnimData = new Vector4[capacity];
        _agentAnimExtraData = new Vector4[capacity];
        _agentColors = new Vector4[capacity];
        _agentMatrixBuffer = new ComputeBuffer(capacity, sizeof(float) * 16);
        _agentAnimBuffer = new ComputeBuffer(capacity, sizeof(float) * 4);
        _agentAnimExtraBuffer = new ComputeBuffer(capacity, sizeof(float) * 4);
        _agentColorBuffer = new ComputeBuffer(capacity, sizeof(float) * 4);
        _agentAnimDataDirty = true;
        _agentColorDataDirty = true;

        for (int i = 0; i < _agentColors.Length; i++)
            _agentColors[i] = Vector4.one;
    }

    private void CacheExportData(GpuRoleExportData exportData)
    {
        if (exportData == null || exportData.spriteUVs == null)
            return;

        for (int i = 0; i < exportData.spriteUVs.Count; i++)
        {
            SpriteUVData uv = exportData.spriteUVs[i];
            if (!_uvBySpriteId.ContainsKey(uv.spriteId))
                _uvBySpriteId.Add(uv.spriteId, uv);
        }
    }

    private GpuRoleExportData FindExportDataForBatch(int animIndex)
    {
        for (int i = 0; i < _agents.Count; i++)
        {
            GpuRoleGpuAgent agent = _agents[i];
            if (agent == null || agent.exportData == null || agent.exportData.animations == null)
                continue;

            if (animIndex >= 0 && animIndex < agent.exportData.animations.Count)
                return agent.exportData;
        }

        return null;
    }

    private static bool UsesCombinedAnimTexture(GpuRoleExportData exportData)
    {
        return exportData != null && exportData.combinedAnimDataTex != null;
    }

    private static Texture2D GetRuntimeAnimTexture(GpuRoleExportData exportData, AnimExportData anim)
    {
        if (UsesCombinedAnimTexture(exportData))
            return exportData.combinedAnimDataTex;

        return anim != null ? anim.animDataTex : null;
    }

    private static int GetRuntimeAnimTexWidth(GpuRoleExportData exportData, AnimExportData anim)
    {
        if (UsesCombinedAnimTexture(exportData))
            return exportData.combinedAnimDataTexWidth;

        return anim != null ? anim.animDataTexWidth : 0;
    }

    private static int GetRuntimeAnimTexHeight(GpuRoleExportData exportData, AnimExportData anim)
    {
        if (UsesCombinedAnimTexture(exportData))
            return exportData.combinedAnimDataTexHeight;

        return anim != null ? anim.animDataTexHeight : 0;
    }

    private void ReassignRuntimeIndices()
    {
        for (int i = 0; i < _agents.Count; i++)
            _agents[i].runtimeIndex = i;
    }

    private static int GetInternalOrder(GpuRoleExportData exportData, int slotIndex)
    {
        if (exportData == null || exportData.slots == null)
            return 0;

        if (slotIndex < 0 || slotIndex >= exportData.slots.Count)
            return 0;

        return exportData.slots[slotIndex].internalOrder;
    }

    private int GetBatchOrder(GpuRoleExportData exportData, int slotIndex)
    {
        if (!compressInternalOrder)
            return GetInternalOrder(exportData, slotIndex);

        int[] orders = GetCompressedInternalOrderMap(exportData);
        if (orders == null || slotIndex < 0 || slotIndex >= orders.Length)
            return GetInternalOrder(exportData, slotIndex);

        return orders[slotIndex];
    }

    private int[] GetCompressedInternalOrderMap(GpuRoleExportData exportData)
    {
        if (exportData == null || exportData.slots == null)
            return null;

        if (_compressedOrderCache.TryGetValue(exportData, out int[] cached) &&
            cached != null &&
            cached.Length == exportData.slots.Count)
            return cached;

        List<int> indices = new List<int>(exportData.slots.Count);
        for (int i = 0; i < exportData.slots.Count; i++)
            indices.Add(i);

        indices.Sort((a, b) =>
        {
            int order = GetInternalOrder(exportData, a).CompareTo(GetInternalOrder(exportData, b));
            if (order != 0) return order;
            return a.CompareTo(b);
        });

        int[] compressed = new int[exportData.slots.Count];
        for (int sortedIndex = 0; sortedIndex < indices.Count; sortedIndex++)
            compressed[indices[sortedIndex]] = sortedIndex;

        _compressedOrderCache[exportData] = compressed;
        return compressed;
    }

    private int[] GetAnimSlotToExportSlotMap(GpuRoleExportData exportData, AnimExportData anim)
    {
        if (exportData == null || anim == null || anim.slotKeys == null)
            return null;

        if (_animSlotToExportSlotCache.TryGetValue(anim, out int[] cached) &&
            cached != null &&
            cached.Length == anim.slotKeys.Count)
            return cached;

        Dictionary<string, int> slotIndexByKey = GetSlotIndexCache(exportData);
        int[] map = new int[anim.slotKeys.Count];
        for (int i = 0; i < map.Length; i++)
        {
            BakedSlotData slot = anim.slotKeys[i];
            if (slot != null && !string.IsNullOrEmpty(slot.slotKey) && slotIndexByKey.TryGetValue(slot.slotKey, out int slotIndex))
                map[i] = slotIndex;
            else
                map[i] = -1;
        }

        _animSlotToExportSlotCache[anim] = map;
        return map;
    }

    private Dictionary<string, int> GetSlotIndexCache(GpuRoleExportData exportData)
    {
        if (_slotIndexCache.TryGetValue(exportData, out Dictionary<string, int> cached) && cached != null)
            return cached;

        Dictionary<string, int> map = new Dictionary<string, int>();
        if (exportData != null && exportData.slots != null)
        {
            for (int i = 0; i < exportData.slots.Count; i++)
            {
                SlotExportData slot = exportData.slots[i];
                if (slot != null && !string.IsNullOrEmpty(slot.slotKey))
                    map[slot.slotKey] = i;
            }
        }

        _slotIndexCache[exportData] = map;
        return map;
    }

    private void UpdateBatchMaterialProperties()
    {
        int writeDepthValue = writeDepth ? 1 : 0;
        if (_lastAppliedWriteDepth != writeDepthValue)
        {
            _lastAppliedWriteDepth = writeDepthValue;
            _material.SetInt("_ZWrite", writeDepthValue);
        }

        if (Mathf.Approximately(_lastAppliedAlphaClipThreshold, alphaClipThreshold))
            return;

        _lastAppliedAlphaClipThreshold = alphaClipThreshold;
        for (int i = 0; i < _batches.Count; i++)
            _batches[i].mpb.SetFloat("_AlphaClipThreshold", alphaClipThreshold);
    }

    private float CalculateDepthZ(Vector3 position)
    {
        float baseZ = preserveAgentZ ? position.z : depthBaseZ;
        float sign = lowerYIsCloser ? 1f : -1f;
        return baseZ + position.y * yToZScale * sign;
    }

    private Matrix4x4 CreateSpriteMatrix(SpriteUVData uv)
    {
        float worldW = uv.cropW / pixelsPerUnit;
        float worldH = uv.cropH / pixelsPerUnit;
        Vector3 pivotOffset = new Vector3(-worldW * uv.pivotX, -worldH * uv.pivotY, 0f);
        return Matrix4x4.TRS(pivotOffset, Quaternion.identity, new Vector3(worldW, worldH, 1f));
    }

    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "GpuRoleGpuRuntimeQuad";
        mesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ReleaseBatches()
    {
        for (int i = 0; i < _batches.Count; i++)
        {
            AtlasBatch batch = _batches[i];
            if (batch.instanceDataBuffer != null) batch.instanceDataBuffer.Release();
            if (batch.uvBuffer != null) batch.uvBuffer.Release();
            if (batch.spriteMatrixBuffer != null) batch.spriteMatrixBuffer.Release();
            if (batch.argsBuffer != null) batch.argsBuffer.Release();
        }

        _batchMap.Clear();
        _batches.Clear();
    }

    private void ReleaseAgentBuffers()
    {
        if (_agentMatrixBuffer != null) _agentMatrixBuffer.Release();
        if (_agentAnimBuffer != null) _agentAnimBuffer.Release();
        if (_agentAnimExtraBuffer != null) _agentAnimExtraBuffer.Release();
        if (_agentColorBuffer != null) _agentColorBuffer.Release();
        _agentMatrixBuffer = null;
        _agentAnimBuffer = null;
        _agentAnimExtraBuffer = null;
        _agentColorBuffer = null;
    }

    private void OnDestroy()
    {
        ReleaseBatches();
        ReleaseAgentBuffers();

        if (_quadMesh != null)
            DestroyImmediate(_quadMesh);
        if (_material != null)
            DestroyImmediate(_material);
    }

    private void OnGUI()
    {
        if (!showRuntimeStats)
            return;

        Rect rect = new Rect(8f, 8f, 300f, 96f);
        GUI.Box(rect, string.Empty);
        GUILayout.BeginArea(rect);
        GUILayout.Label($"GpuRoleGpuManager");
        GUILayout.Label($"Agents: {agentCount}  Batches: {batchCount}  Instances: {instanceCount}");
        GUILayout.Label($"Rebuilds: {rebuildCount}");
        GUILayout.EndArea();
    }
}
