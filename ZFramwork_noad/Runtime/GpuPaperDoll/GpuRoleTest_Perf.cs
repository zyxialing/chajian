using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 性能测试：使用 Graphics.DrawMeshInstanced 批量渲染大量角色
/// </summary>
public class GpuRoleTest_Perf : MonoBehaviour
{
    public GpuRoleExportData exportData;
    public Shader shader;
    public Camera targetCamera;

    [Header("角色数量")]
    public int characterCount = 1000;

    [Header("排列方式")]
    public float gridSpacing = 2f;

    [Header("Sprite PPU")]
    public float ppu = 32f;

    [Header("动画")]
    public int animIndex = 0;
    public float playbackSpeed = 1f;

    private const int MaxInstanceCount = 1023;

    private Material _material;

    // 缓存
    private Dictionary<int, Mesh> _meshCache = new Dictionary<int, Mesh>();
    private Dictionary<int, SpriteUVData> _uvCache = new Dictionary<int, SpriteUVData>();
    private Dictionary<int, InstanceBatch> _batchMap = new Dictionary<int, InstanceBatch>();
    private List<InstanceBatch> _batchList = new List<InstanceBatch>();

    // 临时数组（复用避免 GC）- 不再需要，直接使用 batch 内部数组

    private AnimExportData _anim;
    private int _currentFrame;
    private float _timer;

    private Dictionary<string, int> _slotIndexByKey = new Dictionary<string, int>();

    // 预计算的矩阵缓存
    private Matrix4x4[] _rootMatrices;           // 每个角色的根矩阵（不变）
    private Dictionary<int, Matrix4x4> _spriteMatrices = new Dictionary<int, Matrix4x4>(); // spriteId → spriteMatrix（不变）
    private Matrix4x4[][] _frameSlotMatrices;    // [frame][slot] 预计算的 slotMatrix（所有角色共享）

    // GPU 动画相关
    private int _currentAnimFrame;

    // 预计算的每角色每 Slot 数据（避免每帧字典查找）
    private struct PerSlotData
    {
        public Matrix4x4 spriteMatrix;  // 预乘的 spriteMatrix
        public InstanceBatch batch;     // 对应的 batch
        public int internalOrder;       // 排序用
        public int spriteId;            // 当前使用的 spriteId
    }
    private PerSlotData[][] _perSlotCache; // [character][slotIndexInAnim]

    private struct CharacterData
    {
        public Vector3 gridPos;
        public int[] spriteIds;
    }

    private CharacterData[] _characters;

    private class InstanceBatch
    {
        public int spriteId;
        public Mesh mesh;
        public Texture2D texture;
        public SpriteUVData uv;
        public MaterialPropertyBlock mpb;
        public Matrix4x4[][] matrixChunks;  // 按 MaxInstanceCount 分块，每块可直接传给 DrawMeshInstanced
        public Vector4[][] colorChunks;     // 颜色也分块，直接存 Vector4 避免每帧转换
        public int[] chunkCounts;           // 每块当前帧的实际数量
        public int totalCount;              // 当前帧总数量
        public ComputeBuffer instanceDataBuffer; // per-instance 数据：float4(slotIndex, 0, 0, 0)
        public Vector4[] instanceDataArray;      // CPU 端填充用
    }

    private void Start()
    {
        if (exportData == null || shader == null || targetCamera == null)
        {
            Debug.LogError("[PerfTest] 缺少引用");
            return;
        }

        if (exportData.animations == null || exportData.animations.Count == 0)
        {
            Debug.LogError("[PerfTest] 没有动画数据");
            return;
        }

        if (animIndex < 0 || animIndex >= exportData.animations.Count)
        {
            Debug.LogError($"[PerfTest] 动画索引 {animIndex} 超出范围");
            return;
        }

        _anim = exportData.animations[animIndex];
        if (_anim.frames == null || _anim.frames.Count == 0)
        {
            Debug.LogError("[PerfTest] 动画没有帧数据");
            return;
        }

        _material = new Material(shader);
        _material.enableInstancing = true;

        // 建立缓存
        for (int i = 0; i < exportData.spriteUVs.Count; i++)
        {
            var uv = exportData.spriteUVs[i];
            _uvCache[uv.spriteId] = uv;
            _meshCache[uv.spriteId] = CreateSpriteMesh(uv);
        }

        // 建立 SlotKey 映射
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            _slotIndexByKey[exportData.slots[i].slotKey] = i;
        }

        // 生成角色数据
        int cols = Mathf.CeilToInt(Mathf.Sqrt(characterCount));
        _characters = new CharacterData[characterCount];

        for (int i = 0; i < characterCount; i++)
        {
            int col = i % cols;
            int row = i / cols;

            int[] spriteIds = new int[exportData.slots.Count];
            for (int s = 0; s < exportData.slots.Count; s++)
            {
                var slot = exportData.slots[s];
                int id = slot.defaultSpriteId;
                if (id < 0 && slot.availableSpriteIds != null && slot.availableSpriteIds.Length > 0)
                    id = slot.availableSpriteIds[Random.Range(0, slot.availableSpriteIds.Length)];
                spriteIds[s] = id;
            }

            _characters[i] = new CharacterData
            {
                gridPos = new Vector3(col * gridSpacing, -row * gridSpacing, 0),
                spriteIds = spriteIds
            };
        }

        // 预计算每个角色的根矩阵（不变）
        _rootMatrices = new Matrix4x4[characterCount];
        for (int i = 0; i < characterCount; i++)
        {
            _rootMatrices[i] = Matrix4x4.TRS(_characters[i].gridPos, Quaternion.identity, Vector3.one);
        }

        // 预计算每个 spriteId 的 spriteMatrix（不变）
        foreach (var kv in _uvCache)
        {
            var uv = kv.Value;
            float worldW = uv.cropW / ppu;
            float worldH = uv.cropH / ppu;
            Vector3 pivotOffset = new Vector3(-worldW * uv.pivotX, -worldH * uv.pivotY, 0f);
            _spriteMatrices[uv.spriteId] = Matrix4x4.TRS(pivotOffset, Quaternion.identity, new Vector3(worldW, worldH, 1f));
        }

        // 预计算所有帧的 slotMatrix（所有角色共享，避免每帧重复 Matrix4x4.TRS）
        int totalFrames = _anim.frames.Count;
        int slotCount = _anim.slotKeys.Count;
        _frameSlotMatrices = new Matrix4x4[totalFrames][];
        for (int f = 0; f < totalFrames; f++)
        {
            _frameSlotMatrices[f] = new Matrix4x4[slotCount];
            var frame = _anim.frames[f];
            for (int s = 0; s < slotCount; s++)
            {
                _frameSlotMatrices[f][s] = Matrix4x4.TRS(
                    s < frame.positions.Count ? frame.positions[s] : Vector3.zero,
                    s < frame.rotations.Count ? frame.rotations[s] : Quaternion.identity,
                    s < frame.scales.Count ? frame.scales[s] : Vector3.one
                );
            }
        }

        // 预创建 Batch，并设置不变的 MaterialPropertyBlock 属性
        // 先统计每个 spriteId 会被多少个角色 × Slot 使用，预分配数组
        Dictionary<int, int> spriteUsageCount = new Dictionary<int, int>();
        for (int c = 0; c < characterCount; c++)
        {
            for (int s = 0; s < exportData.slots.Count; s++)
            {
                int sid = _characters[c].spriteIds[s];
                if (sid < 0) continue;
                if (!spriteUsageCount.ContainsKey(sid))
                    spriteUsageCount[sid] = 0;
                spriteUsageCount[sid]++;
            }
        }

        foreach (var kv in _uvCache)
        {
            var uv = kv.Value;
            var atlas = exportData.atlases[uv.atlasIndex];
            if (atlas == null || atlas.texture == null) continue;

            int maxCount = spriteUsageCount.TryGetValue(uv.spriteId, out int count) ? count : 0;
            if (maxCount < 1) maxCount = 1; // 至少 1，避免 0 长度 buffer

            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_MainTex", atlas.texture);
            mpb.SetVector("_UVRect", new Vector4(uv.uMin, uv.vMin, uv.uMax, uv.vMax));
            // 设置当前 batch 固定的 spriteMatrix
            if (_spriteMatrices.TryGetValue(uv.spriteId, out Matrix4x4 sm))
                mpb.SetMatrix("_SpriteMatrix", sm);
            // 设置动画纹理（所有 Batch 共享同一张）
            if (_anim.animDataTex != null)
            {
                mpb.SetTexture("_AnimTex", _anim.animDataTex);
                mpb.SetFloat("_AnimSlotCount", _anim.animDataTexWidth / 3);
                mpb.SetFloat("_AnimTexHeight", _anim.animDataTexHeight);
            }
            else
            {
                Debug.LogWarning($"[PerfTest] 动画 {_anim.animName} 没有烘焙纹理，请重新导出");
            }

            // 按 MaxInstanceCount 分块预分配
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
                texture = atlas.texture,
                uv = uv,
                mpb = mpb,
                matrixChunks = matrixChunks,
                colorChunks = colorChunks,
                chunkCounts = chunkCounts,
                totalCount = 0,
                instanceDataBuffer = new ComputeBuffer(maxCount, sizeof(float) * 4),
                instanceDataArray = new Vector4[maxCount]
            };
            // 绑定 ComputeBuffer 到 Shader
            batch.mpb.SetBuffer("_InstanceData", batch.instanceDataBuffer);
            _batchMap[uv.spriteId] = batch;
            _batchList.Add(batch);
        }

        // 预计算每角色每 Slot 的查找结果（避免每帧字典查找）
        _perSlotCache = new PerSlotData[characterCount][];
        for (int c = 0; c < characterCount; c++)
        {
            _perSlotCache[c] = new PerSlotData[slotCount];
            var ch = _characters[c];
            for (int i = 0; i < slotCount; i++)
            {
                var slotKey = _anim.slotKeys[i];
                if (!_slotIndexByKey.TryGetValue(slotKey.slotKey, out int slotIdx) ||
                    slotIdx < 0 || slotIdx >= exportData.slots.Count)
                {
                    _perSlotCache[c][i] = new PerSlotData { batch = null };
                    continue;
                }

                int spriteId = ch.spriteIds[slotIdx];
                if (spriteId < 0 || !_spriteMatrices.TryGetValue(spriteId, out Matrix4x4 sm) ||
                    !_batchMap.TryGetValue(spriteId, out InstanceBatch batch))
                {
                    _perSlotCache[c][i] = new PerSlotData { batch = null };
                    continue;
                }

                _perSlotCache[c][i] = new PerSlotData
                {
                    spriteMatrix = sm,
                    batch = batch,
                    internalOrder = exportData.slots[slotIdx].internalOrder,
                    spriteId = spriteId
                };
            }
        }

        // 应用第 0 帧
        ApplyFrame(0);

        Debug.Log($"[PerfTest] 角色: {characterCount}, Slot: {exportData.slots.Count}, Batch: {_batchList.Count}");
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

            ApplyFrame(_currentFrame);
        }
    }

    private void ApplyFrame(int frameIndex)
    {
        _currentAnimFrame = frameIndex;

        // 重置 batch 计数器
        for (int b = 0; b < _batchList.Count; b++)
        {
            var batch = _batchList[b];
            batch.totalCount = 0;
            for (int ci = 0; ci < batch.chunkCounts.Length; ci++)
                batch.chunkCounts[ci] = 0;
        }

        int slotCount = _anim.slotKeys.Count;
        for (int c = 0; c < _characters.Length; c++)
        {
            var perSlot = _perSlotCache[c];
            Matrix4x4 rootMat = _rootMatrices[c];

            for (int i = 0; i < slotCount; i++)
            {
                var slotData = perSlot[i];
                if (slotData.batch == null) continue;

                // 只传 rootMatrix（Shader 中再应用 slotMatrix 和 spriteMatrix）
                Matrix4x4 finalMatrix = rootMat;
                // Z 偏移排序
                finalMatrix.m23 = -slotData.internalOrder * 0.001f - c * 0.000001f;

                InstanceBatch batch = slotData.batch;
                int total = batch.totalCount;
                int chunkIdx = total / MaxInstanceCount;
                int inChunkIdx = total % MaxInstanceCount;
                batch.matrixChunks[chunkIdx][inChunkIdx] = finalMatrix;
                batch.colorChunks[chunkIdx][inChunkIdx] = Vector4.one; // 颜色由 Shader 从动画纹理采样
                // per-instance 数据：slotIndex
                batch.instanceDataArray[total] = new Vector4(i, 0f, 0f, 0f);
                batch.chunkCounts[chunkIdx] = inChunkIdx + 1;
                batch.totalCount = total + 1;
            }
        }

        // 更新每个 Batch 的 ComputeBuffer 和 _AnimFrame
        for (int b = 0; b < _batchList.Count; b++)
        {
            var batch = _batchList[b];
            if (batch.totalCount > 0)
            {
                batch.instanceDataBuffer.SetData(batch.instanceDataArray, 0, 0, batch.totalCount);
                batch.mpb.SetFloat("_AnimFrame", frameIndex);
            }
        }
    }

    private void LateUpdate()
    {
        if (_material == null || targetCamera == null) return;

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
                    uv.meshVertices[i].x / uv.cropW,
                    uv.meshVertices[i].y / uv.cropH,
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
    }
}
