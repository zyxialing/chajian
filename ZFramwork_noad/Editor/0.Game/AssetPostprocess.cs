using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class AssetImport : AssetPostprocessor
{
    const string dynamicPath = "Assets/Game/AssetDynamic/";
    const string dynamicSpritePath = "Assets/Game/AssetDynamic/Sprite/";
    const string staticSpritePath = "Assets/Game/AssetStatic/Sprite/";
    private void OnPreprocessAsset()
    {
        if (File.GetAttributes(assetPath) == FileAttributes.Directory) return;
        if (assetPath.Contains(dynamicPath))
        {
            string path = assetPath;
            var group = AddressableAssetSettingsDefaultObject.Settings.DefaultGroup;
            string guid = AssetDatabase.AssetPathToGUID(path);//要打包的资产条目   将路径转成guid
            AddressableAssetEntry entry = AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(guid, group);//要打包的资产条目   会将要打包的路径移动到group节点下
            string endStr = path.Substring(path.LastIndexOf("."));
            path = path.Replace(endStr, "");
            entry.SetAddress(path);
            return;
        }
        if (assetPath.Contains(staticSpritePath))
        {

            return;
        }
    }

    private void OnPreprocessTexture()
    {
        if (assetPath.Contains(dynamicSpritePath)||assetPath.Contains(staticSpritePath))
        {
            var importer = assetImporter as TextureImporter;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            //importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.textureType = TextureImporterType.Sprite;
        }
    }

    private void OnPostprocessTexture(Texture2D texture)
    {
        //这个函数会处理OnPreprocessTexture()
        //中设置好的那个图片资源
        //使用texture.Apply();就会在图片下生成Sprite子物体啦~
        texture.Apply();
    }
}