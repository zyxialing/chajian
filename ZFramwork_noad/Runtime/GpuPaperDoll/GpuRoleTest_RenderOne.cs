using UnityEngine;

/// <summary>
/// 测试脚本：只渲染一个 Slot 的一张图
/// 确认 Graphics.DrawMesh + PropertyBlock 能正确显示
/// </summary>
public class GpuRoleTest_RenderOne : MonoBehaviour
{
    public GpuRoleExportData exportData;
    public Shader shader; // 拖入 GpuPaperDoll/Sprite Shader
    public Camera targetCamera; // 拖入 Main Camera
    public int testSlotIndex = 0;

    private Mesh _quadMesh;
    private Material _material;
    private MaterialPropertyBlock _propertyBlock;

    private void Start()
    {
        if (exportData == null)
        {
            Debug.LogError("[Test] exportData is null!");
            return;
        }

        // 1. 创建 Quad Mesh
        _quadMesh = CreateQuadMesh();

        // 2. 创建材质 - 用 GpuPaperDoll/Sprite
        if (shader == null)
        {
            Debug.LogError("[Test] shader is null! Drag GpuPaperDoll/Sprite shader to the field.");
            return;
        }
        _material = new Material(shader);
        _material.enableInstancing = true;

        // 3. 取第一个有可用 Sprite 的 Slot
        testSlotIndex = -1;
        SlotExportData slotData = null;
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            if (exportData.slots[i].availableSpriteIds.Length > 0)
            {
                testSlotIndex = i;
                slotData = exportData.slots[i];
                break;
            }
        }

        if (slotData == null)
        {
            Debug.LogError("[Test] No slot with available sprites!");
            return;
        }

        // 4. 取第一个可用的 SpriteId
        int spriteId = slotData.availableSpriteIds[0];
        Debug.Log($"[Test] Rendering Slot[{testSlotIndex}] {slotData.slotName} spriteId={spriteId}");

        // 5. 查找 SpriteUV 数据
        SpriteUVData uvData = null;
        foreach (var uv in exportData.spriteUVs)
        {
            if (uv.spriteId == spriteId)
            {
                uvData = uv;
                break;
            }
        }

        if (uvData == null)
        {
            Debug.LogError($"[Test] SpriteUV not found for spriteId={spriteId}");
            return;
        }

        // 6. 获取图集纹理
        var atlasData = exportData.atlases[uvData.atlasIndex];
        if (atlasData == null || atlasData.texture == null)
        {
            Debug.LogError("[Test] Atlas texture is null!");
            return;
        }

        Debug.Log($"[Test] Using atlas={atlasData.name} uv=({uvData.uMin},{uvData.vMin})-({uvData.uMax},{uvData.vMax})");

        // 7. 偏移通过修改 Quad 顶点位置来实现，Shader 里不做偏移
        // 先只测试 UV 裁剪，偏移后面再处理
        _propertyBlock = new MaterialPropertyBlock();
        _propertyBlock.SetTexture("_MainTex", atlasData.texture);
        _propertyBlock.SetVector("_UVRect", new Vector4(uvData.uMin, uvData.vMin, uvData.uMax, uvData.vMax));
        _propertyBlock.SetVector("_CropOffset", Vector4.zero);
        _propertyBlock.SetVector("_Size", new Vector4(uvData.cropW, uvData.cropH, uvData.originalWidth, uvData.originalHeight));
        _propertyBlock.SetFloat("_AtlasWidth", atlasData.width);
        _propertyBlock.SetFloat("_AtlasHeight", atlasData.height);

        Debug.Log($"[Test] Ready! pivot=({uvData.pivotX},{uvData.pivotY}) cropXY=({uvData.cropX},{uvData.cropY}) cropWH=({uvData.cropW},{uvData.cropH}) orig=({uvData.originalWidth},{uvData.originalHeight})");
    }

    private void LateUpdate()
    {
        if (_quadMesh == null || _material == null || _propertyBlock == null) return;

        Matrix4x4 trs = Matrix4x4.TRS(
            transform.position,
            Quaternion.identity,
            new Vector3(2, 2, 1)
        );

        if (targetCamera == null) return;

        Graphics.DrawMesh(
            _quadMesh,
            trs,
            _material,
            gameObject.layer,
            targetCamera,
            0,
            _propertyBlock,
            UnityEngine.Rendering.ShadowCastingMode.Off,
            false
        );
    }

    private Mesh CreateQuadMesh()
    {
        var mesh = new Mesh();
        mesh.name = "TestQuad";

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
        };

        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
        };

        int[] triangles = new int[]
        {
            0, 1, 2,
            2, 1, 3,
        };

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private void OnDestroy()
    {
        if (_quadMesh != null) DestroyImmediate(_quadMesh);
        if (_material != null) DestroyImmediate(_material);
    }
}
