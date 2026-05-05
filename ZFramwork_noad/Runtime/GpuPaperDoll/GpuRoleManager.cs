using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU PaperDoll 底层渲染管理器
/// 负责 batch 管理、ComputeBuffer、统一绘制
/// </summary>
public class GpuRoleManager : MonoBehaviour
{
    public Shader shader;
    public Camera targetCamera;

    [Header("性能")]
    public int maxCharacterCount = 2000;

    private const int MaxInstanceCount = 1023;

    private Material _material;

    // 缓存
    private Dictionary<int, Mesh> _meshCache = new Dictionary<int, Mesh>();
    private Dictionary<int, SpriteUVData> _uvCache = new Dictionary<int, SpriteUVData>();
    private Dictionary<int, Matrix4x4> _spriteMatrices = new Dictionary<int, Matrix4x4>();
    private Dictionary<int, InstanceBatch> _batchMap = new Dictionary<int, InstanceBatch>();
    private List<InstanceBatch> _batchList = new List<InstanceBatch>();

    // 注册的 Agent
    private List<GpuRoleAgent> _agents = new List<GpuRoleAgent>();

    // 预计算
    private Matrix4x4[][] _frameSlotMatrices;
    private int _slotCount;
    private int _totalFrames;
    private AnimExportData _anim;

    private class InstanceBatch
    {
        public int spriteId;
        public Mesh mesh;
        public MaterialPropertyBlock mpb;
        public Matrix4x4[][] matrixChunks;
        public Vector4[][] colorChunks;
        public int[] chunkCounts;
        public int totalCount;
        public ComputeBuffer instanceDataBuffer;
        public Vector4[] instanceDataArray;
        public int capacity;
    }

    private void Awake()
    {
        if (shader == null)
        {
            Debug.LogError("[GpuRoleManager] 缺少 Shader");
            enabled = false;
            return;
        }

        _material = new Material(shader);
        _material.enableInstancing = true;
    }

    public void Register(GpuRoleAgent agent)
    {
        if (_agents.Contains(agent)) return;
        _agents.Add(agent);
        agent.manager = this;
        agent.allDirty = true;
    }

    public void Unregister(GpuRoleAgent agent)
    {
        _agents.Remove(agent);
        if (agent.manager == this)
            agent.manager = null;
    }

    /// <summary>
    /// 初始化缓存（从第一个注册的 Agent 的 ExportData 建立）
    /// </summary>
    public void InitializeCache(GpuRoleExportData exportData, AnimExportData anim)
    {
        if (exportData == null) return;

        // 建立 UV 和 Mesh 缓存
        for (int i = 0; i < exportData.spriteUVs.Count; i++)
        {
            var uv = exportData.spriteUVs[i];
            if (_uvCache.ContainsKey(uv.spriteId)) continue;
            _uvCache[uv.spriteId] = uv;
            _meshCache[uv.spriteId] = CreateSpriteMesh(uv);
        }

        // 建立 spriteMatrix 缓存
        foreach (var kv in _uvCache)
        {
            var uv = kv.Value;
            float worldW = uv.cropW / 32f;
            float worldH = uv.cropH / 32f;
            Vector3 pivotOffset = new Vector3(-worldW * uv.pivotX, -worldH * uv.pivotY, 0f);
            _spriteMatrices[uv.spriteId] = Matrix4x4.TRS(pivotOffset, Quaternion.identity, new Vector3(worldW, worldH, 1f));
        }

        // 预计算 slotMatrix
        if (anim != null && anim.frames != null && anim.frames.Count > 0)
        {
            _anim = anim;
            _slotCount = anim.slotKeys.Count;
            _totalFrames = anim.frames.Count;
            _frameSlotMatrices = new Matrix4x4[_totalFrames][];
            for (int f = 0; f < _totalFrames; f++)
            {
                _frameSlotMatrices[f] = new Matrix4x4[_slotCount];
                var frame = anim.frames[f];
                for (int s = 0; s < _slotCount; s++)
                {
                    _frameSlotMatrices[f][s] = Matrix4x4.TRS(
                        s < frame.positions.Count ? frame.positions[s] : Vector3.zero,
                        s < frame.rotations.Count ? frame.rotations[s] : Quaternion.identity,
                        s < frame.scales.Count ? frame.scales[s] : Vector3.one
                    );
                }
            }
        }
    }

    public void RebuildAllBatches()
    {
        // 清空旧 batch
        foreach (var batch in _batchList)
        {
            if (batch.instanceDataBuffer != null)
                batch.instanceDataBuffer.Release();
        }
        _batchMap.Clear();
        _batchList.Clear();

        // 统计每个 spriteId 的使用量
        Dictionary<int, int> spriteUsageCount = new Dictionary<int, int>();
        for (int a = 0; a < _agents.Count; a++)
        {
            var agent = _agents[a];
            if (!agent.isActiveAndEnabled) continue;
            var slots = agent.GetCurrentSlotSpriteIds();
            if (slots == null) continue;
            for (int i = 0; i < slots.Length; i++)
            {
                int sid = slots[i];
                if (sid < 0) continue;
                if (!spriteUsageCount.ContainsKey(sid))
                    spriteUsageCount[sid] = 0;
                spriteUsageCount[sid]++;
            }
        }

        // 创建 Batch
        foreach (var kv in _uvCache)
        {
            var uv = kv.Value;
            var exportData = GetExportData();
            if (exportData == null) continue;
            var atlas = exportData.atlases[uv.atlasIndex];
            if (atlas == null || atlas.texture == null) continue;

            int maxCount = spriteUsageCount.TryGetValue(uv.spriteId, out int count) ? count : 0;
            if (maxCount < 1) maxCount = 1;

            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_MainTex", atlas.texture);
            mpb.SetVector("_UVRect", new Vector4(uv.uMin, uv.vMin, uv.uMax, uv.vMax));
            if (_spriteMatrices.TryGetValue(uv.spriteId, out Matrix4x4 sm))
                mpb.SetMatrix("_SpriteMatrix", sm);
            if (_anim != null && _anim.animDataTex != null)
            {
                mpb.SetTexture("_AnimTex", _anim.animDataTex);
                mpb.SetFloat("_AnimSlotCount", _anim.animDataTexWidth / 3);
                mpb.SetFloat("_AnimTexHeight", _anim.animDataTexHeight);
            }

            int chunkCount = (maxCount + MaxInstanceCount - 1) / MaxInstanceCount;
            if (chunkCount < 1) chunkCount = 1;
            var matrixChunks = new Matrix4x4[chunkCount][];
            var colorChunks = new Vector4[chunkCount][];
            var chunkCounts = new int[chunkCount];
            for (int ci = 0; ci < chunkCount; ci++)
            {
                int chunkSize = Mathf.Min(MaxInstanceCount, maxCount - ci * MaxInstanceCount);
                matrixChunks[ci] = new Matrix4x4[chunkSize];
                colorChunks[ci] = new Vector4[chunkSize];
            }

            var batch = new InstanceBatch
            {
                spriteId = uv.spriteId,
                mesh = _meshCache[uv.spriteId],
                mpb = mpb,
                matrixChunks = matrixChunks,
                colorChunks = colorChunks,
                chunkCounts = chunkCounts,
                totalCount = 0,
                instanceDataBuffer = new ComputeBuffer(maxCount, sizeof(float) * 4),
                instanceDataArray = new Vector4[maxCount],
                capacity = maxCount
            };
            batch.mpb.SetBuffer("_InstanceData", batch.instanceDataBuffer);
            _batchMap[uv.spriteId] = batch;
            _batchList.Add(batch);
        }
    }

    public void FillFrame(int frameIndex)
    {
        // 重置 batch 计数器
        for (int b = 0; b < _batchList.Count; b++)
        {
            var batch = _batchList[b];
            batch.totalCount = 0;
            for (int ci = 0; ci < batch.chunkCounts.Length; ci++)
                batch.chunkCounts[ci] = 0;
        }

        for (int a = 0; a < _agents.Count; a++)
        {
            var agent = _agents[a];
            if (!agent.isActiveAndEnabled) continue;
            agent.FillInstanceData(frameIndex, this);
        }

        // 更新 ComputeBuffer
        for (int b = 0; b < _batchList.Count; b++)
        {
            var batch = _batchList[b];
            if (batch.totalCount > 0)
            {
                batch.instanceDataBuffer.SetData(batch.instanceDataArray, 0, 0, batch.totalCount);
            }
        }
    }

    public void FillInstanceToBatch(int spriteId, Matrix4x4 matrix, Vector4 color, Vector4 instanceData, int slotIndex)
    {
        if (!_batchMap.TryGetValue(spriteId, out InstanceBatch batch)) return;
        if (batch.totalCount >= batch.capacity) return;

        int total = batch.totalCount;
        int chunkIdx = total / MaxInstanceCount;
        int inChunkIdx = total % MaxInstanceCount;
        batch.matrixChunks[chunkIdx][inChunkIdx] = matrix;
        batch.colorChunks[chunkIdx][inChunkIdx] = color;
        batch.instanceDataArray[total] = instanceData;
        batch.chunkCounts[chunkIdx] = inChunkIdx + 1;
        batch.totalCount = total + 1;
    }

    public Matrix4x4 GetSlotMatrix(int frameIndex, int slotIndex)
    {
        if (_frameSlotMatrices == null) return Matrix4x4.identity;
        if (frameIndex < 0 || frameIndex >= _frameSlotMatrices.Length) return Matrix4x4.identity;
        if (slotIndex < 0 || slotIndex >= _frameSlotMatrices[frameIndex].Length) return Matrix4x4.identity;
        return _frameSlotMatrices[frameIndex][slotIndex];
    }

    public int slotCount => _slotCount;
    public int totalFrames => _totalFrames;
    public AnimExportData currentAnim => _anim;

    private GpuRoleExportData GetExportData()
    {
        for (int i = 0; i < _agents.Count; i++)
        {
            if (_agents[i] != null && _agents[i].exportData != null)
                return _agents[i].exportData;
        }
        return null;
    }

    private void LateUpdate()
    {
        if (_material == null || targetCamera == null) return;

        bool needRebuild = false;
        bool needFill = false;

        for (int a = 0; a < _agents.Count; a++)
        {
            var agent = _agents[a];
            if (!agent.isActiveAndEnabled) continue;

            if (agent.allDirty)
            {
                needRebuild = true;
                agent.allDirty = false;
                agent.frameDirty = true;
            }
            if (agent.slotDirty)
            {
                needRebuild = true;
                agent.slotDirty = false;
                agent.frameDirty = true;
            }
            if (agent.frameDirty)
            {
                needFill = true;
            }
        }

        if (needRebuild)
        {
            RebuildAllBatches();
        }

        if (needFill)
        {
            // 更新 _AnimFrame
            int frameIndex = 0;
            for (int a = 0; a < _agents.Count; a++)
            {
                var agent = _agents[a];
                if (!agent.isActiveAndEnabled) continue;
                frameIndex = agent.currentFrame;
                agent.frameDirty = false;
            }

            FillFrame(frameIndex);

            // 设置 _AnimFrame 到所有 batch
            for (int b = 0; b < _batchList.Count; b++)
            {
                _batchList[b].mpb.SetFloat("_AnimFrame", frameIndex);
            }
        }

        // 绘制
        for (int b = 0; b < _batchList.Count; b++)
        {
            DrawBatch(_batchList[b]);
        }
    }

    private void DrawBatch(InstanceBatch batch)
    {
        if (batch.totalCount == 0) return;

        var mpb = batch.mpb;

        for (int ci = 0; ci < batch.matrixChunks.Length; ci++)
        {
            int count = batch.chunkCounts[ci];
            if (count == 0) continue;

            int start = ci * MaxInstanceCount;
            mpb.SetInt("_InstanceOffset", start);
            mpb.SetVectorArray("_InstanceColor", batch.colorChunks[ci]);

            Graphics.DrawMeshInstanced(
                batch.mesh,
                0,
                _material,
                batch.matrixChunks[ci],
                count,
                mpb,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                targetCamera
            );
        }
    }

    private Mesh CreateSpriteMesh(SpriteUVData uv)
    {
        Mesh mesh = new Mesh();
        mesh.name = $"SpriteMesh_{uv.spriteId}";

        if (uv.meshVertices != null && uv.meshVertices.Length > 0 && uv.meshTriangles != null && uv.meshTriangles.Length > 0)
        {
            Vector3[] verts = new Vector3[uv.meshVertices.Length];
            for (int i = 0; i < uv.meshVertices.Length; i++)
            {
                verts[i] = new Vector3(
                    Mathf.Clamp01(uv.meshVertices[i].x / uv.cropW),
                    Mathf.Clamp01(uv.meshVertices[i].y / uv.cropH),
                    0
                );
            }
            mesh.vertices = verts;
            mesh.uv = uv.meshUVs;
            mesh.triangles = System.Array.ConvertAll(uv.meshTriangles, t => (int)t);
        }
        else
        {
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (_material != null) DestroyImmediate(_material);

        foreach (var kv in _meshCache)
        {
            if (kv.Value != null) DestroyImmediate(kv.Value);
        }
        _meshCache.Clear();

        foreach (var batch in _batchList)
        {
            if (batch.instanceDataBuffer != null)
                batch.instanceDataBuffer.Release();
        }
        _batchList.Clear();
        _batchMap.Clear();
    }
}
