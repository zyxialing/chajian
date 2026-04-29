using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class GrassIndirectRenderer : MonoBehaviour
{
    private Mesh mesh;   // ⭐ private
    public Material material;
    public GrassAtlasData atlasData;

    public float mapSize = 200;

    [Header("Init Only")]
    public DistributionType distribution = DistributionType.Cluster;
    public int seed = 12345;
    public int targetCount = 20000;

    [Range(0.5f, 3f)]
    public float density = 1.0f;

    // ⭐ 运行时数据
    private List<Vector2> currentPoints = new List<Vector2>();
    private List<int> currentIndices = new List<int>();

    private ComputeBuffer posBuffer;
    private ComputeBuffer uvBuffer;
    private ComputeBuffer argsBuffer;

    private int currentCount = 0;

    struct GrassData
    {
        public Vector3 pos;
        public float pad;
        public Vector2 scale;
    }

#if UNITY_EDITOR
    private bool isDirty = false;
#endif

    void EnsureInit()
    {
        if (mesh == null)
            mesh = CreateQuad();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            isDirty = true;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isDirty) return;

                isDirty = false;

                EnsureInit();
                Regenerate();
                UnityEditor.SceneView.RepaintAll();
            };
        }
    }
#endif

    public void Init(DistributionType type, int seed, Material material, GrassAtlasData atlasData,int targetCount,float density)
    {
        this.distribution = type;
        this.seed = seed;
        this.material = material;
        this.atlasData = atlasData;
        this.targetCount = targetCount;
        this.density = density;
        EnsureInit();
        Regenerate();
    }

    // =========================
    // ⭐ 初始化生成（唯一生成入口）
    // =========================
    public void Regenerate()
    {
        if (material == null || atlasData == null) return;

        Random.InitState(seed);

        currentPoints = GeneratePoints();
        currentIndices.Clear();

        BuildGPU();
    }

    void BuildGPU()
    {
        int count = currentPoints.Count;
        if (count == 0)
        {
            Release(); // ⭐ 防止旧buffer残留
            return;
        }
        EnsureBuffer(count);

        GrassData[] data = new GrassData[count];
        Vector4[] uv = new Vector4[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 p = currentPoints[i];

            int index = Random.Range(0, atlasData.uvs.Length);
            currentIndices.Add(index);

            Vector2 size = atlasData.sizes[index] * 0.04f;

            data[i] = new GrassData
            {
                pos = new Vector3(p.x, p.y, -0.1f),
                scale = size
            };

            uv[i] = atlasData.uvs[index];
        }

        ApplyBuffer(data, uv);
    }

    void Apply()
    {
        int count = currentPoints.Count;
        if (count == 0)
        {
            Release(); // ⭐ 很关键
            return;
        }

        EnsureBuffer(count);

        GrassData[] data = new GrassData[count];
        Vector4[] uv = new Vector4[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 p = currentPoints[i];
            int index = currentIndices[i];

            Vector2 size = atlasData.sizes[index] * 0.04f;

            data[i] = new GrassData
            {
                pos = new Vector3(p.x, p.y, -0.1f),
                scale = size
            };

            uv[i] = atlasData.uvs[index];
        }

        ApplyBuffer(data, uv);
    }

    void ApplyBuffer(GrassData[] data, Vector4[] uv)
    {
        posBuffer.SetData(data);
        uvBuffer.SetData(uv);

        material.SetBuffer("_GrassData", posBuffer);
        material.SetBuffer("_UVDataBuffer", uvBuffer);

        uint[] args = new uint[5];
        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)data.Length;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);

        argsBuffer.SetData(args);
    }

    // =========================
    // ⭐ 分布（只初始化用）
    // =========================
    List<Vector2> GeneratePoints()
    {
        switch (distribution)
        {
            case DistributionType.Scatter:
                return GenerateScatter();

            case DistributionType.PoissonFast:
                return GeneratePoisson();

            case DistributionType.Cluster:
                return GenerateCluster();
        }

        return GenerateScatter();
    }

    List<Vector2> GenerateScatter()
    {
        List<Vector2> list = new List<Vector2>();

        for (int i = 0; i < targetCount; i++)
        {
            list.Add(RandomPos());
        }

        return list;
    }

    List<Vector2> GeneratePoisson()
    {
        float cellSize = Mathf.Lerp(2.5f, 0.8f, density - 0.5f);

        int gridSize = Mathf.CeilToInt(mapSize / cellSize);
        bool[,] grid = new bool[gridSize, gridSize];

        List<Vector2> points = new List<Vector2>();

        int tries = targetCount * 3;

        while (points.Count < targetCount && tries-- > 0)
        {
            Vector2 p = RandomPos();

            int gx = Mathf.FloorToInt(p.x / cellSize);
            int gy = Mathf.FloorToInt(p.y / cellSize);

            if (gx < 0 || gy < 0 || gx >= gridSize || gy >= gridSize)
                continue;

            if (!grid[gx, gy])
            {
                grid[gx, gy] = true;
                points.Add(p);
            }
        }

        return points;
    }

    List<Vector2> GenerateCluster()
    {
        List<Vector2> result = new List<Vector2>();

        int clusterSize = 30;
        int centerCount = Mathf.Max(1, targetCount / clusterSize);

        List<Vector2> centers = new List<Vector2>();

        float radius = Mathf.Lerp(8f, 4f, density - 0.5f);

        int safety = centerCount * 10;

        while (centers.Count < centerCount && safety-- > 0)
        {
            Vector2 p = RandomPos();

            bool valid = true;

            for (int i = 0; i < centers.Count; i++)
            {
                if ((centers[i] - p).sqrMagnitude < radius * radius)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                centers.Add(p);
        }

        foreach (var c in centers)
        {
            int count = Random.Range(20, 50);

            for (int i = 0; i < count; i++)
            {
                Vector2 p = c + Random.insideUnitCircle * 2.5f;

                if (p.x < 0 || p.y < 0 || p.x > mapSize || p.y > mapSize)
                    continue;

                result.Add(p);

                if (result.Count >= targetCount)
                    return result;
            }
        }

        return result;
    }

    Vector2 RandomPos()
    {
        return new Vector2(
            Random.Range(0, mapSize),
            Random.Range(0, mapSize)
        );
    }

    // =========================
    // ⭐ 建筑压草（核心）
    // =========================
    public void AddBlock(Vector2 center, Vector2 size)
    {
        for (int i = currentPoints.Count - 1; i >= 0; i--)
        {
            Vector2 p = currentPoints[i];

            if (Mathf.Abs(p.x - center.x) < size.x * 0.5f &&
                Mathf.Abs(p.y - center.y) < size.y * 0.5f)
            {
                currentPoints.RemoveAt(i);
                currentIndices.RemoveAt(i);
            }
        }

        Apply();
    }

    // =========================
    void EnsureBuffer(int count)
    {
        if (posBuffer != null && currentCount == count)
            return;

        Release();

        posBuffer = new ComputeBuffer(count, sizeof(float) * 6);
        uvBuffer = new ComputeBuffer(count, sizeof(float) * 4);
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        currentCount = count;
    }

    void Update()
    {
        if (mesh == null || argsBuffer == null || material == null)
            return;

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            material,
            new Bounds(new Vector3(mapSize * 0.5f, mapSize * 0.5f, 0), Vector3.one * mapSize),
            argsBuffer
        );
    }

    void OnDisable() => Release();
    void OnDestroy() => Release();

    void Release()
    {
        if (posBuffer != null) posBuffer.Release();
        if (uvBuffer != null) uvBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();

        posBuffer = null;
        uvBuffer = null;
        argsBuffer = null;
        currentCount = 0;
    }

    Mesh CreateQuad()
    {
        Mesh m = new Mesh();

        m.vertices = new Vector3[]
        {
            new Vector3(-0.5f,-0.5f,0),
            new Vector3(0.5f,-0.5f,0),
            new Vector3(0.5f,0.5f,0),
            new Vector3(-0.5f,0.5f,0)
        };

        m.uv = new Vector2[]
        {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1)
        };

        m.triangles = new int[]
        {
            0,1,2,
            0,2,3
        };

        return m;
    }
}