using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Extreme GPU path test.
/// This component does not use GpuRoleAgent/GpuRoleManager. It measures a target architecture where
/// CPU uploads one root matrix per character and the GPU expands character slots in the vertex shader.
/// Transparency sorting and runtime clothing changes are intentionally ignored for this test.
/// </summary>
public class GpuRoleTest_ExtremeGpu : MonoBehaviour
{
    public GpuRoleExportData exportData;
    public Shader shader;
    public Camera targetCamera;

    [Header("Test")]
    public int animationIndex = 0;
    public int roleCount = 1000;
    public int columns = 50;
    public float spacing = 2.2f;
    public float scale = 1f;
    public bool moveAgents = true;
    public float moveAmplitude = 0.25f;
    public float moveSpeed = 2f;
    public Color color = Color.white;
    public Vector3 drawBoundsCenter = Vector3.zero;
    public Vector3 drawBoundsSize = new Vector3(10000f, 10000f, 10000f);
    public float pixelsPerUnit = 32f;

    private readonly List<AtlasBatch> _batches = new List<AtlasBatch>();
    private readonly Dictionary<int, SpriteUVData> _uvBySpriteId = new Dictionary<int, SpriteUVData>();
    private Material _material;
    private Mesh _quadMesh;
    private Matrix4x4[] _agentMatrices;
    private ComputeBuffer _agentMatrixBuffer;
    private float _startTime;

    private class AtlasBatch
    {
        public int atlasIndex;
        public int slotCount;
        public MaterialPropertyBlock mpb;
        public ComputeBuffer uvBuffer;
        public ComputeBuffer spriteMatrixBuffer;
        public ComputeBuffer animSlotIndexBuffer;
        public ComputeBuffer argsBuffer;
    }

    private void OnEnable()
    {
        Build();
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnValidate()
    {
        roleCount = Mathf.Max(1, roleCount);
        columns = Mathf.Max(1, columns);
        spacing = Mathf.Max(0.01f, spacing);
        pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
    }

    [ContextMenu("Rebuild Extreme GPU Test")]
    public void Build()
    {
        Release();

        if (exportData == null)
        {
            Debug.LogError("[GpuRoleTest_ExtremeGpu] Missing ExportData");
            enabled = false;
            return;
        }

        if (shader == null)
            shader = Shader.Find("GpuPaperDoll/ExtremeTest");

        if (shader == null)
        {
            Debug.LogError("[GpuRoleTest_ExtremeGpu] Missing shader: GpuPaperDoll/ExtremeTest");
            enabled = false;
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            Debug.LogError("[GpuRoleTest_ExtremeGpu] Missing target camera");
            enabled = false;
            return;
        }

        if (exportData.animations == null || animationIndex < 0 || animationIndex >= exportData.animations.Count)
        {
            Debug.LogError("[GpuRoleTest_ExtremeGpu] Invalid animation index");
            enabled = false;
            return;
        }

        AnimExportData anim = exportData.animations[animationIndex];
        if (anim == null || anim.animDataTex == null || anim.slotKeys == null || anim.slotKeys.Count == 0)
        {
            Debug.LogError("[GpuRoleTest_ExtremeGpu] Animation has no GPU anim texture or slots");
            enabled = false;
            return;
        }

        _material = new Material(shader);
        _quadMesh = CreateQuadMesh();
        _startTime = Time.time;

        _uvBySpriteId.Clear();
        for (int i = 0; i < exportData.spriteUVs.Count; i++)
        {
            SpriteUVData uv = exportData.spriteUVs[i];
            if (!_uvBySpriteId.ContainsKey(uv.spriteId))
                _uvBySpriteId.Add(uv.spriteId, uv);
        }

        BuildAgentBuffer();
        BuildAtlasBatches(anim);
    }

    private void LateUpdate()
    {
        if (_material == null || _quadMesh == null || _agentMatrixBuffer == null || targetCamera == null)
            return;

        UpdateAgentMatrices();

        AnimExportData anim = exportData.animations[animationIndex];
        int frameCount = Mathf.Max(1, anim.frames != null ? anim.frames.Count : anim.totalFrames);
        float elapsed = Mathf.Max(0f, Time.time - _startTime);
        int frame = Mathf.FloorToInt(elapsed * anim.frameRate) % frameCount;
        Bounds bounds = new Bounds(drawBoundsCenter, drawBoundsSize);

        for (int i = 0; i < _batches.Count; i++)
        {
            AtlasBatch batch = _batches[i];
            if (batch == null || batch.slotCount == 0) continue;

            batch.mpb.SetFloat("_AnimFrame", frame);
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

    private void BuildAgentBuffer()
    {
        _agentMatrices = new Matrix4x4[roleCount];
        _agentMatrixBuffer = new ComputeBuffer(roleCount, sizeof(float) * 16);
        UpdateAgentMatrices();
    }

    private void UpdateAgentMatrices()
    {
        float t = Time.time * moveSpeed;
        for (int i = 0; i < roleCount; i++)
        {
            int x = i % columns;
            int y = i / columns;
            Vector3 pos = transform.position + new Vector3(x * spacing, -y * spacing, 0f);
            if (moveAgents)
            {
                pos.x += Mathf.Sin(t + i * 0.173f) * moveAmplitude;
                pos.y += Mathf.Cos(t * 0.73f + i * 0.119f) * moveAmplitude;
            }

            _agentMatrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * scale);
        }

        _agentMatrixBuffer.SetData(_agentMatrices);
    }

    private void BuildAtlasBatches(AnimExportData anim)
    {
        Dictionary<int, List<SlotBuildData>> slotsByAtlas = new Dictionary<int, List<SlotBuildData>>();

        for (int animSlotIndex = 0; animSlotIndex < anim.slotKeys.Count; animSlotIndex++)
        {
            string slotKey = anim.slotKeys[animSlotIndex].slotKey;
            int exportSlotIndex = FindExportSlotIndex(slotKey);
            if (exportSlotIndex < 0 || exportSlotIndex >= exportData.slots.Count)
                continue;

            int spriteId = exportData.slots[exportSlotIndex].defaultSpriteId;
            if (spriteId < 0 || !_uvBySpriteId.TryGetValue(spriteId, out SpriteUVData uv))
                continue;

            if (uv.atlasIndex < 0 || uv.atlasIndex >= exportData.atlases.Count)
                continue;

            if (!slotsByAtlas.TryGetValue(uv.atlasIndex, out List<SlotBuildData> list))
            {
                list = new List<SlotBuildData>();
                slotsByAtlas.Add(uv.atlasIndex, list);
            }

            list.Add(new SlotBuildData
            {
                animSlotIndex = animSlotIndex,
                uv = uv
            });
        }

        foreach (var kv in slotsByAtlas)
        {
            AtlasData atlas = exportData.atlases[kv.Key];
            if (atlas == null || atlas.texture == null)
                continue;

            List<SlotBuildData> slots = kv.Value;
            int slotCount = slots.Count;
            if (slotCount == 0)
                continue;

            Vector4[] uvRects = new Vector4[slotCount];
            Matrix4x4[] spriteMatrices = new Matrix4x4[slotCount];
            int[] animSlotIndices = new int[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                SpriteUVData uv = slots[i].uv;
                uvRects[i] = new Vector4(uv.uMin, uv.vMin, uv.uMax, uv.vMax);
                spriteMatrices[i] = CreateSpriteMatrix(uv);
                animSlotIndices[i] = slots[i].animSlotIndex;
            }

            AtlasBatch batch = new AtlasBatch
            {
                atlasIndex = kv.Key,
                slotCount = slotCount,
                mpb = new MaterialPropertyBlock(),
                uvBuffer = new ComputeBuffer(slotCount, sizeof(float) * 4),
                spriteMatrixBuffer = new ComputeBuffer(slotCount, sizeof(float) * 16),
                animSlotIndexBuffer = new ComputeBuffer(slotCount, sizeof(int)),
                argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments)
            };

            batch.uvBuffer.SetData(uvRects);
            batch.spriteMatrixBuffer.SetData(spriteMatrices);
            batch.animSlotIndexBuffer.SetData(animSlotIndices);

            uint[] args = new uint[5];
            args[0] = _quadMesh.GetIndexCount(0);
            args[1] = (uint)(roleCount * slotCount);
            args[2] = _quadMesh.GetIndexStart(0);
            args[3] = _quadMesh.GetBaseVertex(0);
            args[4] = 0;
            batch.argsBuffer.SetData(args);

            batch.mpb.SetTexture("_MainTex", atlas.texture);
            batch.mpb.SetTexture("_AnimTex", anim.animDataTex);
            batch.mpb.SetColor("_Color", color);
            batch.mpb.SetFloat("_AnimSlotCount", anim.animDataTexWidth / 3f);
            batch.mpb.SetFloat("_AnimTexHeight", anim.animDataTexHeight);
            batch.mpb.SetInt("_BatchSlotCount", slotCount);
            batch.mpb.SetBuffer("_AgentMatrices", _agentMatrixBuffer);
            batch.mpb.SetBuffer("_SlotUVRects", batch.uvBuffer);
            batch.mpb.SetBuffer("_SlotSpriteMatrices", batch.spriteMatrixBuffer);
            batch.mpb.SetBuffer("_AnimSlotIndices", batch.animSlotIndexBuffer);

            _batches.Add(batch);
        }

        Debug.Log($"[GpuRoleTest_ExtremeGpu] Built: roles={roleCount}, atlasBatches={_batches.Count}, slots={anim.slotKeys.Count}");
    }

    private int FindExportSlotIndex(string slotKey)
    {
        if (string.IsNullOrEmpty(slotKey) || exportData == null || exportData.slots == null)
            return -1;

        for (int i = 0; i < exportData.slots.Count; i++)
        {
            if (exportData.slots[i].slotKey == slotKey)
                return i;
        }

        return -1;
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
        mesh.name = "GpuRoleExtremeQuad";
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

    private void Release()
    {
        if (_material != null)
        {
            DestroyImmediate(_material);
            _material = null;
        }

        if (_quadMesh != null)
        {
            DestroyImmediate(_quadMesh);
            _quadMesh = null;
        }

        if (_agentMatrixBuffer != null)
        {
            _agentMatrixBuffer.Release();
            _agentMatrixBuffer = null;
        }

        for (int i = 0; i < _batches.Count; i++)
        {
            AtlasBatch batch = _batches[i];
            if (batch == null) continue;
            if (batch.uvBuffer != null) batch.uvBuffer.Release();
            if (batch.spriteMatrixBuffer != null) batch.spriteMatrixBuffer.Release();
            if (batch.animSlotIndexBuffer != null) batch.animSlotIndexBuffer.Release();
            if (batch.argsBuffer != null) batch.argsBuffer.Release();
        }

        _batches.Clear();
        _uvBySpriteId.Clear();
        _agentMatrices = null;
    }

    private struct SlotBuildData
    {
        public int animSlotIndex;
        public SpriteUVData uv;
    }
}
