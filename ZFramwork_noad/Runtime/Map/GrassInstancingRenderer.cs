using UnityEngine;
using System.Collections.Generic;

public class GrassInstancingRenderer : MonoBehaviour
{
    public Mesh quadMesh;
    public Material material;
    public GrassAtlasData atlasData;

    public int grassCount = 20000;
    public float mapSize = 200;

    // =========================
    // ⭐ 缓存数据（核心优化）
    // =========================
    List<Matrix4x4> matrices = new List<Matrix4x4>();
    List<Vector4> uvList = new List<Vector4>();

    Matrix4x4[] matrixArray = new Matrix4x4[1023];
    Vector4[] uvArray = new Vector4[1023];

    MaterialPropertyBlock mpb;

    // =========================
    // ⭐ 初始化
    // =========================
    void Start()
    {
        // 自动生成Quad
        if (quadMesh == null)
        {
            quadMesh = CreateQuadMesh();
            Debug.Log("自动生成Quad Mesh");
        }

        if (material == null)
        {
            Debug.LogError("Material 没设置！");
            return;
        }

        if (atlasData == null)
        {
            Debug.LogError("AtlasData 没设置！");
            return;
        }

        mpb = new MaterialPropertyBlock();

        Generate(); // ⭐ 只生成一次
    }

    // =========================
    // ⭐ 生成草（只执行一次）
    // =========================
    void Generate()
    {
        matrices.Clear();
        uvList.Clear();

        for (int i = 0; i < grassCount; i++)
        {
            float x = Random.Range(0, mapSize);
            float y = Random.Range(0, mapSize);

            // ⭐ 分布控制（自然一点）
            float n1 = Mathf.PerlinNoise(x * 0.03f, y * 0.03f);
            float n2 = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);

            float density = n1 * 0.7f + n2 * 0.3f;

            if (density < 0.45f) continue;

            int index = Random.Range(0, atlasData.uvs.Length);

            Vector4 uv = atlasData.uvs[index];
            Vector2 size = atlasData.sizes[index];

            float scale = 0.02f;

            Vector3 pos = new Vector3(x, y, 0);

            Matrix4x4 m = Matrix4x4.TRS(
                pos,
                Quaternion.Euler(0, 0, 0),
                new Vector3(size.x * scale, size.y * scale, 1)
            );

            matrices.Add(m);
            uvList.Add(uv);
        }

        Debug.Log("生成草数量: " + matrices.Count);
    }

    // =========================
    // ⭐ 每帧绘制（轻量）
    // =========================
    void Update()
    {
        Draw();
    }

    void Draw()
    {
        int batchSize = 1023;

        for (int i = 0; i < matrices.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, matrices.Count - i);

            // ⭐ 填充缓存数组（无GC）
            for (int j = 0; j < count; j++)
            {
                matrixArray[j] = matrices[i + j];
                uvArray[j] = uvList[i + j];
            }

            mpb.Clear();
            mpb.SetVectorArray("_UVData", uvArray);

            Graphics.DrawMeshInstanced(
                quadMesh,
                0,
                material,
                matrixArray,
                count,
                mpb
            );
        }
    }

    // =========================
    // ⭐ 自动创建Quad
    // =========================
    Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();

        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0)
        };

        mesh.uv = new Vector2[]
        {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1)
        };

        mesh.triangles = new int[]
        {
            0,1,2,
            0,2,3
        };

        mesh.RecalculateBounds();

        return mesh;
    }
}