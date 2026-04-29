using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class SpriteExtractorByName : EditorWindow
{
    private Texture2D selectedTexture;
    private string spriteNameToExport = "";

    [MenuItem("ZFramework/Window/Texture操作/导出图集中某张 Sprite")]
    static void ShowWindow()
    {
        GetWindow<SpriteExtractorByName>("导出指定 Sprite");
    }

    void OnGUI()
    {
        GUILayout.Label("选择图集(Texture2D)，并输入 Sprite 名称", EditorStyles.boldLabel);

        selectedTexture = (Texture2D)EditorGUILayout.ObjectField("图集 Texture", selectedTexture, typeof(Texture2D), false);
        spriteNameToExport = EditorGUILayout.TextField("Sprite 名称", spriteNameToExport);

        if (GUILayout.Button("导出 Sprite"))
        {
            if (selectedTexture == null)
            {
                Debug.LogError("请先选择图集 Texture2D！");
                return;
            }

            ExportSpriteByName(selectedTexture, spriteNameToExport);
        }
    }

    void ExportSpriteByName(Texture2D texture, string targetSpriteName)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

        Sprite targetSprite = null;
        List<string> spriteNames = new List<string>();

        foreach (var asset in assets)
        {
            if (asset is Sprite sprite)
            {
                spriteNames.Add(sprite.name);
                if (sprite.name == targetSpriteName)
                {
                    targetSprite = sprite;
                    break;
                }
            }
        }

        if (targetSprite == null)
        {
            Debug.LogError("找不到名称为 " + targetSpriteName + " 的 Sprite！");
            Debug.Log("图集中包含的 Sprite 名称如下：");
            foreach (var name in spriteNames)
                Debug.Log("- " + name);
            return;
        }

        // 开始导出
        Rect rect = targetSprite.rect;
        Texture2D newTex = new Texture2D((int)rect.width, (int)rect.height);
        Color[] pixels = texture.GetPixels(
            (int)rect.x,
            (int)rect.y,
            (int)rect.width,
            (int)rect.height
        );
        newTex.SetPixels(pixels);
        newTex.Apply();

        // 保存为 PNG
        string outPath = Path.GetDirectoryName(path) + "/" + targetSprite.name + "_extracted.png";
        File.WriteAllBytes(outPath, newTex.EncodeToPNG());
        AssetDatabase.Refresh();

        Debug.Log("成功导出 Sprite：" + targetSprite.name + " 到 " + outPath);
    }
}
