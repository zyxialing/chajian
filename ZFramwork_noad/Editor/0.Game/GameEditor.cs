using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class GameEditor : Editor
{
    const string assetDynamic = "Assets/Game/AssetDynamic";
    const string assets = "Assets";
    const string meta = ".meta";
    const string gameScriptPath = "Assets/Game/Scripts/";
    const string resourcesConfig = "Assets/Game/Resources/AdressablePath.asset";
    const string commonAudioConfig = "Assets/Game/AssetDynamic/Audio/Common";

    static List<string> AddPath(List<string> paths)
    {
        List<string> loadPath = new List<string>();
        paths.Add("Assets/Game");
        paths.Add("Assets/Third");

        paths.Add(assetDynamic);
        paths.Add("Assets/Game/AssetDynamic/Material");
        paths.Add("Assets/Game/AssetDynamic/Prefab");
        paths.Add("Assets/Game/AssetDynamic/Prefab/Game");
        paths.Add("Assets/Game/AssetDynamic/Prefab/UI");
        paths.Add("Assets/Game/AssetDynamic/Prefab/UI/Panel");
        paths.Add("Assets/Game/AssetDynamic/Prefab/UI/PanelUp");
        paths.Add("Assets/Game/AssetDynamic/Prefab/UI/Pop");
        paths.Add("Assets/Game/AssetDynamic/Prefab/UI/Tips");
        paths.Add("Assets/Game/AssetDynamic/Prefab/UI/Overlay");
        paths.Add("Assets/Game/AssetDynamic/Scene");
        paths.Add("Assets/Game/AssetDynamic/Sprite");
        paths.Add("Assets/Game/AssetDynamic/Sprite/Game");
        paths.Add("Assets/Game/AssetDynamic/Sprite/UI");
        paths.Add("Assets/Game/AssetDynamic/Texture");
        paths.Add("Assets/Game/AssetDynamic/Texture/Game");
        paths.Add("Assets/Game/AssetDynamic/Texture/UI");
        paths.Add("Assets/Game/AssetDynamic/Audio");
        paths.Add("Assets/Game/AssetDynamic/Config");
        paths.Add("Assets/Game/AssetDynamic/Config/AI");
        paths.Add(commonAudioConfig);

        paths.Add("Assets/Game/AssetStatic");
        paths.Add("Assets/Game/AssetStatic/Material");
        paths.Add("Assets/Game/AssetStatic/Prefab");
        paths.Add("Assets/Game/AssetStatic/Prefab/Game");
        paths.Add("Assets/Game/AssetStatic/Prefab/UI");
        paths.Add("Assets/Game/AssetStatic/Prefab/UI/Panel");
        paths.Add("Assets/Game/AssetStatic/Prefab/UI/PanelUp");
        paths.Add("Assets/Game/AssetStatic/Prefab/UI/Pop");
        paths.Add("Assets/Game/AssetStatic/Prefab/UI/Tips");
        paths.Add("Assets/Game/AssetStatic/Prefab/UI/Overlay");
        paths.Add("Assets/Game/AssetStatic/Scene");
        paths.Add("Assets/Game/AssetStatic/Sprite");
        paths.Add("Assets/Game/AssetStatic/Sprite/Game");
        paths.Add("Assets/Game/AssetStatic/Sprite/UI");
        paths.Add("Assets/Game/AssetStatic/Texture");
        paths.Add("Assets/Game/AssetStatic/Texture/Game");
        paths.Add("Assets/Game/AssetStatic/Texture/UI");
        paths.Add("Assets/Game/AssetStatic/Audio");
        paths.Add("Assets/Game/AssetStatic/Shader");

        paths.Add("Assets/Game/Resources");



        paths.Add("Assets/Game/Scripts");

        paths.Add("Assets/Game/Scripts/UI");
        paths.Add("Assets/Game/Scripts/UI/Panel");
        paths.Add("Assets/Game/Scripts/UI/PanelUp");
        paths.Add("Assets/Game/Scripts/UI/Pop");
        paths.Add("Assets/Game/Scripts/UI/Tips");
        paths.Add("Assets/Game/Scripts/UI/Overlay");
        paths.Add("Assets/Game/Scripts/UIVO");
        paths.Add("Assets/Game/Scripts/UIVO/Panel");
        paths.Add("Assets/Game/Scripts/UIVO/Pop");
        paths.Add("Assets/Game/Scripts/UIVO/Tips");
        paths.Add("Assets/Game/Scripts/UIVO/Overlay");

        //////////////动态目录
        loadPath.Add("Assets/Game/AssetDynamic/Material");
        loadPath.Add("Assets/Game/AssetDynamic/Prefab/Game");
        loadPath.Add("Assets/Game/AssetDynamic/Prefab/UI");
        loadPath.Add("Assets/Game/AssetDynamic/Scene");
        loadPath.Add("Assets/Game/AssetDynamic/Sprite/Game");
        loadPath.Add("Assets/Game/AssetDynamic/Sprite/UI");
        loadPath.Add("Assets/Game/AssetDynamic/Texture/Game");
        loadPath.Add("Assets/Game/AssetDynamic/Texture/UI");
        loadPath.Add("Assets/Game/AssetDynamic/Audio");
        return loadPath;
    }

    [MenuItem("GameStart/1.CheckPath")]
    public static void CheckPath()
    {
        List<string> paths = new List<string>();
        AddPath(paths);
        foreach (var item in paths)
        {
            if (Directory.Exists(item))
            {
                Debug.Log(item);
            }
            else
            {
                Debug.LogError(item+"该路径不存在");
            }
        }
        paths = null;
    }
    [MenuItem("GameStart/2.CreatePath")]
    public static void CreatePath()
    {
        List<string> paths = new List<string>();
        List<string> adressPaths = AddPath(paths);
        foreach (var item in paths)
        {
            if (Directory.Exists(item))
            {
                Debug.Log(item);
            }
            else
            {
                Directory.CreateDirectory(item);
                Debug.Log(item + "路径创建成功");
            }
        }
        AssetDatabase.Refresh();
        AdressablePath adressablePath = ScriptableObject.CreateInstance<AdressablePath>();
        AssetDatabase.CreateAsset(adressablePath, resourcesConfig);
        string str = "/";
        adressablePath.material_path = adressPaths[0] + str;
        adressablePath.prefab_game_path = adressPaths[1] + str;
        adressablePath.prefab_ui_path = adressPaths[2] + str;
        adressablePath.scene_path = adressPaths[3] + str;
        adressablePath.sprite_game_path = adressPaths[4] + str;
        adressablePath.sprite_ui_path = adressPaths[5] + str;
        adressablePath.texture_game_path = adressPaths[6] + str;
        adressablePath.texture_ui_path = adressPaths[7] + str;
        adressablePath.audio_path = adressPaths[8] + str;
        List<string> configPath = new List<string>();
        configPath.Add(resourcesConfig);
        AssetDatabase.ForceReserializeAssets(configPath);
        AssetDatabase.Refresh();
        paths = null;
        configPath = null;
    }
     [MenuItem("GameStart/3.GenerateUIScript")]
    public static void GenerateUIScript()
    {
        ScriptCreater.CreateServiceBinder();
        ScriptCreater.CreateTestData();
        ScriptCreater.CreateEventTest();
        ScriptCreater.CreateITestMgr();
        ScriptCreater.CreateTestMgr();
        AssetDatabase.Refresh();
    }
    [MenuItem("GameStart/4.GenerateAdressable")]
    public static void GenerateAdressable()
    {
        string folderPath = Application.dataPath + "/AddressableAssetsData";
        string metaPath = Application.dataPath + "/AddressableAssetsData.meta";

        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, true);
            File.Delete(metaPath);
            AssetDatabase.Refresh();
        }
        var setting = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
        AddressableAssetSettingsDefaultObject.Settings = setting;
        AddressableAssetGroup group = setting.DefaultGroup;

        DirectoryInfo directoryInfo = new DirectoryInfo(assetDynamic);
        DirectoryInfo rootInfo = new DirectoryInfo(assets);

        FileInfo[] fileInfos = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
        StringBuilder stringBuilder = new StringBuilder();
        string pathHead = rootInfo.FullName.Replace(assets, "");
        string path;
        for (int i = 0; i < fileInfos.Length; i++)
        {
            stringBuilder.Append(fileInfos[i].FullName);
            stringBuilder = stringBuilder.Replace(pathHead, "").Replace("\\", "/");
            path = stringBuilder.ToString();
            if (path.EndsWith(meta))
            {
                stringBuilder.Clear();
                continue;
            }
            string guid = AssetDatabase.AssetPathToGUID(path);//要打包的资产条目   将路径转成guid
            AddressableAssetEntry entry = setting.CreateOrMoveEntry(guid, group);//要打包的资产条目   会将要打包的路径移动到group节点下
            string endStr = path.Substring(path.LastIndexOf("."));
            stringBuilder = stringBuilder.Replace(endStr, "");
            entry.SetAddress(stringBuilder.ToString());
            stringBuilder.Clear();
        }
        AssetDatabase.Refresh();
    }
    
    [MenuItem("GameStart/5.GenerateCommonAudioPaths")]
    public static void GenerateCommonAudioPaths()//todo 对uiframe进行拓展
    {
        List<string> audioPaths = new List<string>();
        string[] audios = Directory.GetFiles(commonAudioConfig);
        for (int i = 0; i < audios.Length; i++)
        {
            if (!audios[i].EndsWith(".meta"))
            {
                string endStr = audios[i].Substring(audios[i].LastIndexOf("."));
                string path = audios[i].Replace(endStr, "").Replace("\\", "/").Replace("Assets/Game/AssetDynamic/Audio/", "");
                audioPaths.Add(path);
            }
        }
        AdressablePath adressablePath = Resources.Load<AdressablePath>("AdressablePath");
        adressablePath.commonAudioPaths = audioPaths;
        List<string> configPath = new List<string>();
        configPath.Add(resourcesConfig);
        AssetDatabase.ForceReserializeAssets(configPath);
        AssetDatabase.Refresh();
        audioPaths = null;
        configPath = null;
    }

    //[MenuItem("GameStart/5.GenerateFrameScripts")]
    //public static void GenerateFrameScripts()//todo 对uiframe进行拓展
    //{
    //    //ProjectWindowUtil.CreateScriptAssetFromTemplateFile(scriptPath, gameScriptPath+"AudioMgr.cs");
    //}



}

public class GameEditorHelp : Editor
{
    [MenuItem("Assets/UpdateAdressable",false,0)]
    public static void UpdateAdressable()
    {
        var setting = AddressableAssetSettingsDefaultObject.Settings;
        if (setting == null)
        {
            Debug.LogError("没有adressableassetsdata");
            return;
        }
        AddressableAssetGroup group = setting.DefaultGroup;
        string[] paths = Selection.assetGUIDs;

        for (int i = 0; i < paths.Length; i++)
        {
            AddressableAssetEntry entry = setting.CreateOrMoveEntry(paths[i], group);
            string path = AssetDatabase.GUIDToAssetPath(paths[i]);
            string endStr = path.Substring(path.LastIndexOf("."));
            path = path.Replace(endStr, "");
            entry.SetAddress(path);
        }
        AssetDatabase.Refresh();
    }
}