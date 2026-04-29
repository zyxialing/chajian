using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GrassTemplateEditor : EditorWindow
{
    List<Texture2D> textures = new List<Texture2D>();
    DefaultAsset folder;
    [MenuItem("ZFramework/Window/地面图片处理/1.图片转为数据",false,1)]
    static void Open()
    {
        GetWindow<GrassTemplateEditor>("Grass Template");
    }
    Vector2 scrollPos;
    void OnGUI()
    {
        GUILayout.Label("草模板生成工具（多图版）", EditorStyles.boldLabel);
        // ⭐ 开始滚动区域
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        folder = (DefaultAsset)EditorGUILayout.ObjectField("草图片文件夹", folder, typeof(DefaultAsset), false);

        if (GUILayout.Button("从文件夹读取"))
        {
            LoadTexturesFromFolder();
        }
        int newCount = Mathf.Max(0, EditorGUILayout.IntField("图片数量", textures.Count));

        while (newCount > textures.Count)
            textures.Add(null);

        while (newCount < textures.Count)
            textures.RemoveAt(textures.Count - 1);

        for (int i = 0; i < textures.Count; i++)
        {
            textures[i] = (Texture2D)EditorGUILayout.ObjectField($"草图 {i}", textures[i], typeof(Texture2D), false);
        }
        // ⭐ 结束滚动区域
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        if (GUILayout.Button("生成模板数据（多图）"))
        {
            ProcessTextures(textures);
        }
    }
    void LoadTexturesFromFolder()
    {
        textures.Clear();

        string path = AssetDatabase.GetAssetPath(folder);

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });

        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            textures.Add(tex);
        }

        Debug.Log("加载图片数量：" + textures.Count);
    }
    // ⭐ 核心逻辑
    void ProcessTextures(List<Texture2D> texList)
    {
        Dictionary<Color32, int> colorMap = new Dictionary<Color32, int>();
        List<GrassTemplate> templates = new List<GrassTemplate>();

        int colorIndex = 0;

        // ⭐ 第一遍：收集所有颜色（全局）
        foreach (var tex in texList)
        {
            if (tex == null) continue;

            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    Color32 c = tex.GetPixel(x, y);
                    if (c.a < 10) continue;

                    if (!colorMap.ContainsKey(c))
                    {
                        colorMap[c] = colorIndex++;
                    }
                }
            }
        }

        // ⭐ 打印颜色
        Debug.Log("=== 全局颜色数量：" + colorMap.Count + " ===");
        foreach (var kv in colorMap)
        {
            Debug.Log($"Index:{kv.Value}  RGB:{kv.Key.r},{kv.Key.g},{kv.Key.b}");
        }

        // ⭐ 第二遍：生成模板数据
        foreach (var tex in texList)
        {
            if (tex == null) continue;

            List<PixelData> pixels = new List<PixelData>();

            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    Color32 c = tex.GetPixel(x, y);
                    if (c.a < 10) continue;

                    pixels.Add(new PixelData
                    {
                        x = (sbyte)x,
                        y = (sbyte)y,
                        colorIndex = (byte)colorMap[c]
                    });
                }
            }

            templates.Add(new GrassTemplate
            {
                pixels = pixels.ToArray()
            });
        }

        // ⭐ 输出结果
        SaveToBinary(colorMap, templates);
    }

    // ⭐ 打印最终数据
    void SaveToBinary(Dictionary<Color32, int> colorMap, List<GrassTemplate> templates)
    {
        string folderPath = AssetDatabase.GetAssetPath(folder);
        string savePath = folderPath + "/GrassData.bytes";

        using (var bw = new System.IO.BinaryWriter(System.IO.File.Open(savePath, System.IO.FileMode.Create)))
        {
            // =========================
            // ⭐ 1. 写入 palette
            // =========================

            bw.Write((byte)colorMap.Count); // 颜色数量

            // 关键：按 index 排序
            Color32[] palette = new Color32[colorMap.Count];

            foreach (var kv in colorMap)
            {
                palette[kv.Value] = kv.Key;
            }

            // 写入 RGB
            for (int i = 0; i < palette.Length; i++)
            {
                bw.Write(palette[i].r);
                bw.Write(palette[i].g);
                bw.Write(palette[i].b);
            }

            // =========================
            // ⭐ 2. 写入模板数量
            // =========================

            bw.Write((ushort)templates.Count);

            // =========================
            // ⭐ 3. 写入模板数据
            // =========================

            foreach (var temp in templates)
            {
                bw.Write((ushort)temp.pixels.Length);

                foreach (var p in temp.pixels)
                {
                    bw.Write(p.x);           // sbyte
                    bw.Write(p.y);           // sbyte
                    bw.Write(p.colorIndex);  // byte
                }
            }
        }

        AssetDatabase.Refresh();

        Debug.Log("保存完成（含颜色表）：" + savePath);
    }
}

public class GrassTemplate
{
    public PixelData[] pixels;
}