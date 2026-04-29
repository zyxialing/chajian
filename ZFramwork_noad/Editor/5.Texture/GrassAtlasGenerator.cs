//   [MenuItem("ZFramework/Window/数据转图片合集")]
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class GrassAtlasGenerator : EditorWindow
{
    public TextAsset dataFile;

    [MenuItem("ZFramework/Window/地面图片处理/2.数据转图片合集",false,2)]
    static void Open()
    {
        GetWindow<GrassAtlasGenerator>();
    }

    void OnGUI()
    {
        dataFile = (TextAsset)EditorGUILayout.ObjectField("Grass Bytes", dataFile, typeof(TextAsset), false);

        if (GUILayout.Button("生成Atlas"))
        {
            GenerateAtlas();
        }
    }

    void GenerateAtlas()
    {
        if (dataFile == null)
        {
            Debug.LogError("没有数据文件");
            return;
        }

        var templates = LoadTemplates(dataFile.bytes, out List<Color> palette);

        int padding = 2;
        int maxRowWidth = 128; // ⭐ 可调（控制横向排布）

        // =========================
        // ⭐ 第1步：计算最终尺寸
        // =========================
        int cursorX = 0;
        int cursorY = 0;
        int rowHeight = 0;

        int maxWidth = 0;

        List<RectInt> layout = new List<RectInt>();

        foreach (var temp in templates)
        {
            GetBounds(temp, out int width, out int height);

            if (cursorX + width >= maxRowWidth)
            {
                cursorX = 0;
                cursorY += rowHeight + padding;
                rowHeight = 0;
            }

            layout.Add(new RectInt(cursorX, cursorY, width, height));

            cursorX += width + padding;
            rowHeight = Mathf.Max(rowHeight, height);

            maxWidth = Mathf.Max(maxWidth, cursorX);
        }

        int totalHeight = cursorY + rowHeight;

        int finalWidth = NextPowerOfTwo(maxWidth);
        int finalHeight = NextPowerOfTwo(totalHeight);

        Debug.Log($"Atlas尺寸：{finalWidth} x {finalHeight}");

        // =========================
        // ⭐ 第2步：创建贴图
        // =========================
        Texture2D tex = new Texture2D(finalWidth, finalHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0, 0, 0, 0);

        for (int x = 0; x < finalWidth; x++)
            for (int y = 0; y < finalHeight; y++)
                tex.SetPixel(x, y, clear);

        // =========================
        // ⭐ 第3步：绘制草
        // =========================
        List<Vector4> uvData = new List<Vector4>();
        List<Vector2> sizeData = new List<Vector2>();

        for (int i = 0; i < templates.Count; i++)
        {
            var temp = templates[i];
            var rect = layout[i];

            GetMin(temp, out int minX, out int minY);

            foreach (var p in temp.pixels)
            {
                int px = rect.x + (p.x - minX);
                int py = rect.y + (p.y - minY);

                tex.SetPixel(px, py, palette[p.colorIndex]);
            }

            // ⭐ UV
            Vector4 uv = new Vector4(
                (float)rect.x / finalWidth,
                (float)rect.y / finalHeight,
                (float)rect.width / finalWidth,
                (float)rect.height / finalHeight
            );

            uvData.Add(uv);
            sizeData.Add(new Vector2(rect.width, rect.height));
        }

        tex.Apply();

        // =========================
        // ⭐ 第4步：保存图片
        // =========================
        string texPath = "Assets/GrassAtlas.png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        AssetDatabase.Refresh();

        // =========================
        // ⭐ 第5步：保存UV数据
        // =========================
        string assetPath = "Assets/GrassAtlasData.asset";

        var data = ScriptableObject.CreateInstance<GrassAtlasData>();

        data.uvs = uvData.ToArray();
        data.sizes = sizeData.ToArray();

        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log("Atlas数据保存完成");

        AssetDatabase.Refresh();

        Debug.Log("Atlas生成完成 + UV数据保存");
    }

    // =========================
    // 工具函数
    // =========================

    void GetBounds(Template t, out int width, out int height)
    {
        int minX = 999, minY = 999, maxX = -999, maxY = -999;

        foreach (var p in t.pixels)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }

        width = maxX - minX + 1;
        height = maxY - minY + 1;
    }

    void GetMin(Template t, out int minX, out int minY)
    {
        minX = 999;
        minY = 999;

        foreach (var p in t.pixels)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
        }
    }

    int NextPowerOfTwo(int v)
    {
        int p = 1;
        while (p < v) p <<= 1;
        return p;
    }

    List<Template> LoadTemplates(byte[] bytes, out List<Color> palette)
    {
        palette = new List<Color>();
        List<Template> templates = new List<Template>();

        using (BinaryReader br = new BinaryReader(new MemoryStream(bytes)))
        {
            int paletteCount = br.ReadByte();

            for (int i = 0; i < paletteCount; i++)
            {
                float r = br.ReadByte() / 255f;
                float g = br.ReadByte() / 255f;
                float b = br.ReadByte() / 255f;

                palette.Add(new Color(r, g, b));
            }

            int templateCount = br.ReadUInt16();

            for (int i = 0; i < templateCount; i++)
            {
                int pixelCount = br.ReadUInt16();

                Template t = new Template();
                t.pixels = new PixelData[pixelCount];

                for (int j = 0; j < pixelCount; j++)
                {
                    t.pixels[j] = new PixelData
                    {
                        x = br.ReadSByte(),
                        y = br.ReadSByte(),
                        colorIndex = br.ReadByte()
                    };
                }

                templates.Add(t);
            }
        }

        return templates;
    }

    struct PixelData
    {
        public sbyte x;
        public sbyte y;
        public byte colorIndex;
    }

    class Template
    {
        public PixelData[] pixels;
    }
}