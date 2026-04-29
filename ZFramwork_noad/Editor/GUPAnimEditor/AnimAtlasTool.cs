using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AnimAtlasTool : EditorWindow
{
    private enum InputMode
    {
        Grid,
        Frames
    }

    private DefaultAsset characterFolder;
    private DefaultAsset outputFolder;
    private DefaultAsset animRootFolder;
    private string animRootName = "";
    private string outputName = "";
    private InputMode mode = InputMode.Grid;
    private int defaultColumns = 1;
    private int defaultRows = 1;
    private int atlasMaxSize = 2048;
    private bool showPreviewShadowSettings;
    private Color previewShadowColor = new Color(0f, 0f, 0f, 0.35f);
    private Texture2D previewAtlas;
    private Vector2 scrollPosition;
    private readonly Dictionary<string, CharacterBuildData> characters = new Dictionary<string, CharacterBuildData>();
    private readonly List<string> scanMessages = new List<string>();

    private const string KEY_CHAR = "AnimTool_Char";
    private const string KEY_OUT = "AnimTool_Out";
    private const string KEY_ROOT = "AnimTool_Root";
    private const string KEY_ROOT_NAME = "AnimTool_Root_Name";
    private const string KEY_NAME = "AnimTool_Name";
    private const string KEY_MODE = "AnimTool_Mode";
    private const string KEY_COLUMNS = "AnimTool_Columns";
    private const string KEY_ROWS = "AnimTool_Rows";
    private const string KEY_ATLAS_SIZE = "AnimTool_Atlas_Size";
    private const string KEY_SHOW_PREVIEW_SHADOW = "AnimTool_Show_Preview_Shadow";
    private const string KEY_PREVIEW_SHADOW_R = "AnimTool_Preview_Shadow_R";
    private const string KEY_PREVIEW_SHADOW_G = "AnimTool_Preview_Shadow_G";
    private const string KEY_PREVIEW_SHADOW_B = "AnimTool_Preview_Shadow_B";
    private const string KEY_PREVIEW_SHADOW_A = "AnimTool_Preview_Shadow_A";

    private static readonly int[] AtlasSizeOptions = { 512, 1024, 2048, 4096, 8192 };
    private static readonly string[] AtlasSizeLabels = { "512", "1024", "2048", "4096", "8192" };

    [MenuItem("ZFramework/Window/GPUAnim Atlas Tool")]
    public static void Open()
    {
        GetWindow<AnimAtlasTool>("AnimAtlas Tool");
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;

        string charPath = EditorPrefs.GetString(KEY_CHAR, "");
        string outPath = EditorPrefs.GetString(KEY_OUT, "");
        string rootPath = EditorPrefs.GetString(KEY_ROOT, "");

        if (!string.IsNullOrEmpty(rootPath))
        {
            animRootFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(rootPath);
        }

        if (!string.IsNullOrEmpty(charPath))
        {
            characterFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(charPath);
        }

        if (!string.IsNullOrEmpty(outPath))
        {
            outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outPath);
        }

        animRootName = EditorPrefs.GetString(KEY_ROOT_NAME, "");
        outputName = EditorPrefs.GetString(KEY_NAME, "");
        ResolveAnimRootFolders();
        mode = (InputMode)EditorPrefs.GetInt(KEY_MODE, 0);
        defaultColumns = EditorPrefs.GetInt(KEY_COLUMNS, 1);
        defaultRows = EditorPrefs.GetInt(KEY_ROWS, 1);
        atlasMaxSize = EditorPrefs.GetInt(KEY_ATLAS_SIZE, 2048);
        showPreviewShadowSettings = EditorPrefs.GetBool(KEY_SHOW_PREVIEW_SHADOW, false);
        previewShadowColor = new Color(
            EditorPrefs.GetFloat(KEY_PREVIEW_SHADOW_R, 0f),
            EditorPrefs.GetFloat(KEY_PREVIEW_SHADOW_G, 0f),
            EditorPrefs.GetFloat(KEY_PREVIEW_SHADOW_B, 0f),
            EditorPrefs.GetFloat(KEY_PREVIEW_SHADOW_A, 0.35f)
        );
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (characters.Count > 0)
        {
            Repaint();
        }
    }

    private void SavePrefs()
    {
        if (characterFolder != null)
        {
            EditorPrefs.SetString(KEY_CHAR, AssetDatabase.GetAssetPath(characterFolder));
        }

        if (outputFolder != null)
        {
            EditorPrefs.SetString(KEY_OUT, AssetDatabase.GetAssetPath(outputFolder));
        }

        if (animRootFolder != null)
        {
            EditorPrefs.SetString(KEY_ROOT, AssetDatabase.GetAssetPath(animRootFolder));
        }

        EditorPrefs.SetString(KEY_ROOT_NAME, animRootName);
        EditorPrefs.SetString(KEY_NAME, outputName);
        EditorPrefs.SetInt(KEY_MODE, (int)mode);
        EditorPrefs.SetInt(KEY_COLUMNS, defaultColumns);
        EditorPrefs.SetInt(KEY_ROWS, defaultRows);
        EditorPrefs.SetInt(KEY_ATLAS_SIZE, atlasMaxSize);
        EditorPrefs.SetBool(KEY_SHOW_PREVIEW_SHADOW, showPreviewShadowSettings);
        EditorPrefs.SetFloat(KEY_PREVIEW_SHADOW_R, previewShadowColor.r);
        EditorPrefs.SetFloat(KEY_PREVIEW_SHADOW_G, previewShadowColor.g);
        EditorPrefs.SetFloat(KEY_PREVIEW_SHADOW_B, previewShadowColor.b);
        EditorPrefs.SetFloat(KEY_PREVIEW_SHADOW_A, previewShadowColor.a);

        foreach (var kv in characters)
        {
            SaveCharacterSettings(kv.Value);
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, true, true);

        GUILayout.Label("Multi Character Animation Atlas", EditorStyles.boldLabel);

        animRootFolder = (DefaultAsset)EditorGUILayout.ObjectField("Anim Root Folder", animRootFolder, typeof(DefaultAsset), false);
        animRootName = EditorGUILayout.TextField("Anim Root Name", animRootName);
        ResolveAnimRootFolders();
        using (new EditorGUI.DisabledScope(true))
        {
            characterFolder = (DefaultAsset)EditorGUILayout.ObjectField("Objects Folder", characterFolder, typeof(DefaultAsset), false);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Targets Folder", outputFolder, typeof(DefaultAsset), false);
        }
        outputName = EditorGUILayout.TextField("Output Name", outputName);
        mode = (InputMode)EditorGUILayout.EnumPopup("Input Mode", mode);
        atlasMaxSize = EditorGUILayout.IntPopup("Atlas Max Size", atlasMaxSize, AtlasSizeLabels, AtlasSizeOptions);

        if (mode == InputMode.Grid)
        {
            defaultColumns = Mathf.Max(1, EditorGUILayout.IntField("Default Columns", defaultColumns));
            defaultRows = Mathf.Max(1, EditorGUILayout.IntField("Default Rows", defaultRows));
            EditorGUILayout.HelpBox("Each source image has its own Columns/Rows below. Frame Width/Height are calculated from the image size.", MessageType.Info);
        }

        EditorGUILayout.HelpBox("Per-character Center Offset, Shadow Offset, and Shadow Size are written into AnimAtlasData. Preview Shadow Color is only used by this window.", MessageType.Info);
        showPreviewShadowSettings = EditorGUILayout.Toggle("Preview Shadow Debug", showPreviewShadowSettings);
        if (showPreviewShadowSettings)
        {
            EditorGUI.indentLevel++;
            previewShadowColor = EditorGUILayout.ColorField("Preview Shadow Color", previewShadowColor);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(8);
        if (GUILayout.Button("Create Folder Structure"))
        {
            CreateFolderStructure();
        }

        if (GUILayout.Button("Scan Objects"))
        {
            ScanObjects();
        }

        if (GUILayout.Button("Build Trimmed Atlas"))
        {
            BuildAll();
        }

        GUILayout.Space(8);
        DrawScanMessages();
        DrawCharacterSettings();
        DrawAtlasPreview();

        SavePrefs();
        EditorGUILayout.EndScrollView();
    }

    private void CreateFolderStructure()
    {
        string rootPath = GetOrCreateAnimRootPath();
        if (string.IsNullOrEmpty(rootPath))
        {
            return;
        }

        EnsureFolder(rootPath, "objects");
        EnsureFolder(rootPath, "targets");
        EnsureFolder($"{rootPath}/objects", "obj0");
        EnsureFolder($"{rootPath}/objects/obj0", "idle");

        AssetDatabase.Refresh();
        animRootFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(rootPath);
        ResolveAnimRootFolders();
        scanMessages.Clear();
        scanMessages.Add($"Folder structure ready: {rootPath}");
    }

    private string GetOrCreateAnimRootPath()
    {
        if (animRootFolder != null)
        {
            string path = AssetDatabase.GetAssetPath(animRootFolder);
            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            scanMessages.Add("Anim Root Folder is invalid.");
            return null;
        }

        string folderName = string.IsNullOrWhiteSpace(animRootName) ? "AnimRoot" : animRootName.Trim();
        string rootPath = $"Assets/{folderName}";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            AssetDatabase.CreateFolder("Assets", folderName);
        }

        return rootPath;
    }

    private void EnsureFolder(string parentPath, string folderName)
    {
        string path = $"{parentPath}/{folderName}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }

    private void ResolveAnimRootFolders()
    {
        if (animRootFolder == null)
        {
            return;
        }

        string rootPath = AssetDatabase.GetAssetPath(animRootFolder);
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            return;
        }

        characterFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{rootPath}/objects");
        outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{rootPath}/targets");
    }

    private void ScanObjects()
    {
        characters.Clear();
        scanMessages.Clear();

        ResolveAnimRootFolders();
        if (characterFolder == null)
        {
            scanMessages.Add("Objects folder is empty. Use Create Folder Structure first.");
            return;
        }

        string rootPath = AssetDatabase.GetAssetPath(characterFolder);
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            scanMessages.Add("Objects folder path is invalid.");
            return;
        }

        string[] characterDirs = Directory.GetDirectories(rootPath);
        if (characterDirs.Length == 0)
        {
            scanMessages.Add("No object folders found. Expected: AnimRoot/objects/obj0/idle/*.png");
            return;
        }

        foreach (string characterDir in characterDirs)
        {
            CharacterBuildData character = CreateCharacterData(characterDir.Replace("\\", "/"));
            DiscoverClips(character);

            if (character.clips.Count == 0)
            {
                scanMessages.Add($"{character.name}: no clips found.");
                continue;
            }

            characters[character.name] = character;
        }

        Debug.Log($"Scan complete. Characters: {characters.Count}");
    }

    private CharacterBuildData CreateCharacterData(string characterPath)
    {
        string characterName = Path.GetFileName(characterPath);
        CharacterBuildData character = new CharacterBuildData();
        character.name = characterName;
        character.path = characterPath;
        character.foldout = EditorPrefs.GetBool(GetCharacterPrefKey(characterName, "foldout"), true);
        character.baseScale = EditorPrefs.GetFloat(GetCharacterPrefKey(characterName, "baseScale"), 1f);
        character.centerOffset = new Vector2(
            EditorPrefs.GetFloat(GetCharacterPrefKey(characterName, "centerX"), 0f),
            EditorPrefs.GetFloat(GetCharacterPrefKey(characterName, "centerY"), 0f)
        );
        character.shadowOffset = new Vector2(
            EditorPrefs.GetFloat(GetCharacterPrefKey(characterName, "shadowX"), 0f),
            EditorPrefs.GetFloat(GetCharacterPrefKey(characterName, "shadowY"), 0f)
        );
        character.shadowSize = new Vector2(
            EditorPrefs.GetFloat(GetCharacterPrefKey(characterName, "shadowSizeX"), 44f),
            EditorPrefs.GetFloat(GetCharacterPrefKey(characterName, "shadowSizeY"), 14f)
        );
        return character;
    }

    private void DiscoverClips(CharacterBuildData character)
    {
        string[] clipDirs = Directory.GetDirectories(character.path);
        foreach (string clipDir in clipDirs)
        {
            List<string> pngs = CollectPngs(clipDir);
            if (pngs.Count == 0)
            {
                continue;
            }

            string clipName = Path.GetFileName(clipDir);
            character.clips[clipName] = pngs;
            EnsureClipSettings(character.name, clipName);
        }

        List<string> directPngs = CollectPngs(character.path);
        foreach (string png in directPngs)
        {
            string clipName = Path.GetFileNameWithoutExtension(png);
            character.clips[clipName] = new List<string> { png };
            EnsureClipSettings(character.name, clipName);
        }
    }

    private List<string> CollectPngs(string folder)
    {
        List<string> pngs = new List<string>();
        foreach (string file in Directory.GetFiles(folder))
        {
            if (file.EndsWith(".png"))
            {
                pngs.Add(file.Replace("\\", "/"));
            }
        }

        pngs.Sort();
        return pngs;
    }

    private void BuildAll()
    {
        if (characters.Count == 0)
        {
            ScanObjects();
        }

        if (characters.Count == 0)
        {
            Debug.LogWarning("No characters to build.");
            return;
        }

        scanMessages.Clear();
        List<Texture2D> packedTexs = new List<Texture2D>();
        List<FrameRuntimeData> runtimeFrames = new List<FrameRuntimeData>();
        List<AnimClip> compatibilityClips = new List<AnimClip>();
        List<AnimCharacter> runtimeCharacters = new List<AnimCharacter>();

        foreach (var characterPair in characters)
        {
            CharacterBuildData sourceCharacter = characterPair.Value;
            AnimCharacter runtimeCharacter = new AnimCharacter();
            runtimeCharacter.name = sourceCharacter.name;
            runtimeCharacter.baseScale = sourceCharacter.baseScale;
            runtimeCharacter.centerOffset = sourceCharacter.centerOffset;
            runtimeCharacter.shadowOffset = sourceCharacter.shadowOffset;
            runtimeCharacter.shadowSize = sourceCharacter.shadowSize;
            runtimeCharacter.clips = new List<AnimClip>();

            foreach (var clipPair in sourceCharacter.clips)
            {
                ClipBuildSettings settings = EnsureClipSettings(sourceCharacter.name, clipPair.Key);
                AnimClip clip = new AnimClip();
                clip.name = clipPair.Key;
                clip.startFrame = runtimeFrames.Count;
                clip.fps = Mathf.Max(0f, settings.fps);
                clip.loop = settings.loop;

                foreach (FrameSource frame in BuildFrames(sourceCharacter, clipPair.Key, clipPair.Value))
                {
                    Texture2D tex = Extract(frame);
                    var trim = Trim(tex);
                    if (trim.isEmpty)
                    {
                        scanMessages.Add($"{sourceCharacter.name}/{clipPair.Key}: empty transparent frame at {frame.assetPath} {frame.pixelRect}.");
                    }

                    packedTexs.Add(trim.texture);
                    FrameRuntimeData frameData = new FrameRuntimeData();
                    frameData.offset = trim.offset;
                    frameData.size = new Vector2(trim.texture.width, trim.texture.height);
                    frameData.baseSize = new Vector2(frame.pixelRect.width, frame.pixelRect.height);
                    runtimeFrames.Add(frameData);
                }

                clip.frameCount = runtimeFrames.Count - clip.startFrame;
                if (clip.frameCount <= 0)
                {
                    scanMessages.Add($"{sourceCharacter.name}/{clipPair.Key}: no frames generated.");
                    continue;
                }

                runtimeCharacter.clips.Add(clip);

                AnimClip compatibilityClip = new AnimClip();
                compatibilityClip.name = $"{sourceCharacter.name}/{clip.name}";
                compatibilityClip.startFrame = clip.startFrame;
                compatibilityClip.frameCount = clip.frameCount;
                compatibilityClip.fps = clip.fps;
                compatibilityClip.loop = clip.loop;
                compatibilityClips.Add(compatibilityClip);
            }

            if (runtimeCharacter.clips.Count > 0)
            {
                runtimeCharacters.Add(runtimeCharacter);
            }
        }

        if (packedTexs.Count == 0)
        {
            Debug.LogWarning("No animation frames found.");
            return;
        }

        PackedAtlasResult packedAtlas = PackAtlasTight(packedTexs, 2, atlasMaxSize);
        if (packedAtlas.texture == null || packedAtlas.rects == null || packedAtlas.rects.Length != packedTexs.Count)
        {
            Debug.LogError($"Pack atlas failed. Try a larger Atlas Max Size. Current: {atlasMaxSize}");
            return;
        }

        for (int i = 0; i < runtimeFrames.Count; i++)
        {
            runtimeFrames[i].uv = packedAtlas.rects[i];
        }

        Texture2D savedAtlas = SaveAtlas(packedAtlas.texture);
        SaveData(savedAtlas, runtimeCharacters, compatibilityClips, runtimeFrames);
        previewAtlas = savedAtlas;
        Debug.Log($"Anim atlas build complete. Characters: {runtimeCharacters.Count}, frames: {runtimeFrames.Count}");
    }

    private List<FrameSource> BuildFrames(CharacterBuildData character, string clipName, List<string> pngs)
    {
        List<FrameSource> frames = new List<FrameSource>();
        Vector2 firstSize = Vector2.zero;

        foreach (string file in pngs)
        {
            EnsureTextureReadable(file);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            if (tex == null)
            {
                scanMessages.Add($"{character.name}/{clipName}: cannot load {file}");
                continue;
            }

            if (mode == InputMode.Grid)
            {
                ImageGridSettings grid = EnsureImageGridSettings(character, file);
                if (grid.columns <= 0 || grid.rows <= 0)
                {
                    scanMessages.Add($"{character.name}/{clipName}: Columns/Rows must be greater than 0 for {Path.GetFileName(file)}.");
                    continue;
                }

                if (tex.width % grid.columns != 0 || tex.height % grid.rows != 0)
                {
                    scanMessages.Add($"{character.name}/{clipName}: {Path.GetFileName(file)} size {tex.width}x{tex.height} is padded transparently for {grid.columns} columns x {grid.rows} rows.");
                }

                Vector2Int frameSize = CalculateGridFrameSize(tex, grid);
                int paddedHeight = frameSize.y * grid.rows;
                for (int y = 0; y < grid.rows; y++)
                {
                    for (int x = 0; x < grid.columns; x++)
                    {
                        Rect rect = new Rect(
                            x * frameSize.x,
                            paddedHeight - (y + 1) * frameSize.y,
                            frameSize.x,
                            frameSize.y
                        );
                        frames.Add(new FrameSource(tex, rect, file, character, paddedHeight));
                    }
                }
            }
            else
            {
                Vector2 size = new Vector2(tex.width, tex.height);
                if (firstSize == Vector2.zero)
                {
                    firstSize = size;
                }
                else if (firstSize != size)
                {
                    scanMessages.Add($"{character.name}/{clipName}: frame size mismatch. {Path.GetFileName(file)} is {tex.width}x{tex.height}, first is {firstSize.x}x{firstSize.y}.");
                }

                frames.Add(new FrameSource(tex, new Rect(0, 0, tex.width, tex.height), file, character));
            }
        }

        return frames;
    }

    private string GetOutputPath()
    {
        if (outputFolder == null)
        {
            Debug.LogError("No output folder");
            return null;
        }

        string folderPath = AssetDatabase.GetAssetPath(outputFolder);
        string finalName = string.IsNullOrEmpty(outputName)
            ? (animRootFolder != null ? animRootFolder.name : "AnimRoot")
            : outputName;
        string fullPath = Path.Combine(folderPath, finalName);

        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(folderPath, finalName);
        }

        return fullPath;
    }

    private Texture2D Extract(FrameSource frame)
    {
        Texture2D tex = new Texture2D((int)frame.pixelRect.width, (int)frame.pixelRect.height);
        Color[] clearPixels = new Color[tex.width * tex.height];
        tex.SetPixels(clearPixels);

        int paddingBottom = Mathf.Max(0, frame.paddedHeight - frame.texture.height);
        RectInt frameRect = new RectInt(
            Mathf.RoundToInt(frame.pixelRect.x),
            Mathf.RoundToInt(frame.pixelRect.y),
            Mathf.RoundToInt(frame.pixelRect.width),
            Mathf.RoundToInt(frame.pixelRect.height)
        );
        RectInt sourceInPadded = new RectInt(0, paddingBottom, frame.texture.width, frame.texture.height);
        RectInt overlap = Intersect(frameRect, sourceInPadded);
        if (overlap.width > 0 && overlap.height > 0)
        {
            int sourceX = overlap.x;
            int sourceY = overlap.y - paddingBottom;
            int targetX = overlap.x - frameRect.x;
            int targetY = overlap.y - frameRect.y;
            tex.SetPixels(targetX, targetY, overlap.width, overlap.height, frame.texture.GetPixels(sourceX, sourceY, overlap.width, overlap.height));
        }

        tex.Apply();
        return tex;
    }

    private RectInt Intersect(RectInt a, RectInt b)
    {
        int xMin = Mathf.Max(a.xMin, b.xMin);
        int yMin = Mathf.Max(a.yMin, b.yMin);
        int xMax = Mathf.Min(a.xMax, b.xMax);
        int yMax = Mathf.Min(a.yMax, b.yMax);
        return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
    }

    private (Texture2D texture, Vector2 offset, bool isEmpty) Trim(Texture2D tex)
    {
        int minX = tex.width;
        int minY = tex.height;
        int maxX = 0;
        int maxY = 0;
        Color[] pixels = tex.GetPixels();

        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                if (pixels[y * tex.width + x].a > 0.01f)
                {
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        if (width <= 0 || height <= 0)
        {
            Texture2D emptyTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            emptyTex.SetPixel(0, 0, Color.clear);
            emptyTex.Apply();
            return (emptyTex, Vector2.zero, true);
        }

        Texture2D newTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        newTex.SetPixels(tex.GetPixels(minX, minY, width, height));
        newTex.Apply();
        return (newTex, new Vector2(minX, minY), false);
    }

    private Texture2D SaveAtlas(Texture2D atlas)
    {
        string basePath = GetOutputPath();
        if (string.IsNullOrEmpty(basePath))
        {
            return null;
        }

        string filePath = Path.Combine(basePath, "atlas.png");
        File.WriteAllBytes(filePath, atlas.EncodeToPNG());
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
    }

    private void SaveData(Texture2D atlas, List<AnimCharacter> runtimeCharacters, List<AnimClip> compatibilityClips, List<FrameRuntimeData> frames)
    {
        AnimAtlasData data = ScriptableObject.CreateInstance<AnimAtlasData>();
        data.atlas = atlas;
        data.characters = runtimeCharacters;
        data.clips = compatibilityClips;
        data.frames = frames;

        if (runtimeCharacters.Count > 0)
        {
            data.centerOffset = runtimeCharacters[0].centerOffset;
            data.shadowOffset = runtimeCharacters[0].shadowOffset;
            data.shadowSize = runtimeCharacters[0].shadowSize;
        }

        data.RebuildRuntimeCache();

        string basePath = GetOutputPath();
        if (string.IsNullOrEmpty(basePath))
        {
            return;
        }

        string assetPath = Path.Combine(basePath, "AnimAtlasData.asset");
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();
    }

    private PackedAtlasResult PackAtlasTight(List<Texture2D> textures, int padding, int maxSize)
    {
        if (textures == null || textures.Count == 0)
        {
            return new PackedAtlasResult(null, null);
        }

        List<int> widthCandidates = BuildAtlasWidthCandidates(textures, padding, maxSize);
        AtlasLayout bestLayout = null;
        foreach (int width in widthCandidates)
        {
            AtlasLayout layout = TryPackShelf(textures, padding, width, maxSize);
            if (layout == null)
            {
                continue;
            }

            if (bestLayout == null || layout.Area < bestLayout.Area)
            {
                bestLayout = layout;
            }
        }

        if (bestLayout == null)
        {
            return new PackedAtlasResult(null, null);
        }

        Texture2D atlas = new Texture2D(bestLayout.width, bestLayout.height, TextureFormat.RGBA32, false);
        Color[] clearPixels = new Color[bestLayout.width * bestLayout.height];
        atlas.SetPixels(clearPixels);

        Rect[] rects = new Rect[textures.Count];
        for (int i = 0; i < textures.Count; i++)
        {
            PackedRect packed = bestLayout.rects[i];
            Texture2D tex = textures[i];
            atlas.SetPixels(packed.x, packed.y, tex.width, tex.height, tex.GetPixels());
            rects[i] = new Rect(
                packed.x / (float)bestLayout.width,
                packed.y / (float)bestLayout.height,
                tex.width / (float)bestLayout.width,
                tex.height / (float)bestLayout.height
            );
        }

        atlas.Apply();
        return new PackedAtlasResult(atlas, rects);
    }

    private List<int> BuildAtlasWidthCandidates(List<Texture2D> textures, int padding, int maxSize)
    {
        int minWidth = 1;
        int totalArea = 0;
        for (int i = 0; i < textures.Count; i++)
        {
            minWidth = Mathf.Max(minWidth, textures[i].width + padding * 2);
            totalArea += (textures[i].width + padding) * (textures[i].height + padding);
        }

        minWidth = Mathf.Min(maxSize, Mathf.Max(1, minWidth));
        List<int> candidates = new List<int>();
        AddWidthCandidate(candidates, minWidth, maxSize);
        AddWidthCandidate(candidates, Mathf.CeilToInt(Mathf.Sqrt(totalArea)), maxSize);

        int width = NextPowerOfTwo(minWidth);
        while (width <= maxSize)
        {
            AddWidthCandidate(candidates, width, maxSize);
            if (width == maxSize)
            {
                break;
            }

            width *= 2;
        }

        AddWidthCandidate(candidates, maxSize, maxSize);
        candidates.Sort();
        return candidates;
    }

    private void AddWidthCandidate(List<int> candidates, int width, int maxSize)
    {
        width = Mathf.Clamp(width, 1, maxSize);
        if (!candidates.Contains(width))
        {
            candidates.Add(width);
        }
    }

    private int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value)
        {
            result <<= 1;
        }

        return result;
    }

    private AtlasLayout TryPackShelf(List<Texture2D> textures, int padding, int shelfWidth, int maxSize)
    {
        List<int> order = new List<int>();
        for (int i = 0; i < textures.Count; i++)
        {
            order.Add(i);
        }

        order.Sort((left, right) =>
        {
            int heightCompare = textures[right].height.CompareTo(textures[left].height);
            return heightCompare != 0 ? heightCompare : textures[right].width.CompareTo(textures[left].width);
        });

        PackedRect[] rects = new PackedRect[textures.Count];
        int cursorX = padding;
        int cursorY = padding;
        int rowHeight = 0;
        int usedWidth = 0;
        int usedHeight = 0;

        foreach (int index in order)
        {
            Texture2D tex = textures[index];
            if (tex.width + padding * 2 > shelfWidth || tex.height + padding * 2 > maxSize)
            {
                return null;
            }

            if (cursorX + tex.width + padding > shelfWidth)
            {
                cursorX = padding;
                cursorY += rowHeight + padding;
                rowHeight = 0;
            }

            if (cursorY + tex.height + padding > maxSize)
            {
                return null;
            }

            rects[index] = new PackedRect(cursorX, cursorY);
            usedWidth = Mathf.Max(usedWidth, cursorX + tex.width);
            usedHeight = Mathf.Max(usedHeight, cursorY + tex.height);
            cursorX += tex.width + padding;
            rowHeight = Mathf.Max(rowHeight, tex.height);
        }

        AtlasLayout layout = new AtlasLayout();
        layout.width = Mathf.Clamp(usedWidth + padding, 1, maxSize);
        layout.height = Mathf.Clamp(usedHeight + padding, 1, maxSize);
        layout.rects = rects;
        return layout;
    }

    private PackedAtlasResult CropAtlas(Texture2D atlas, Rect[] rects)
    {
        int atlasWidth = atlas.width;
        int atlasHeight = atlas.height;
        int minX = atlasWidth;
        int minY = atlasHeight;
        int maxX = 0;
        int maxY = 0;
        Color[] pixels = atlas.GetPixels();
        for (int y = 0; y < atlasHeight; y++)
        {
            for (int x = 0; x < atlasWidth; x++)
            {
                if (pixels[y * atlasWidth + x].a > 0.01f)
                {
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x + 1);
                    maxY = Mathf.Max(maxY, y + 1);
                }
            }
        }

        if (maxX <= minX || maxY <= minY)
        {
            minX = 0;
            minY = 0;
            maxX = 1;
            maxY = 1;
        }

        minX = Mathf.Clamp(minX, 0, atlasWidth - 1);
        minY = Mathf.Clamp(minY, 0, atlasHeight - 1);
        maxX = Mathf.Clamp(maxX, minX + 1, atlasWidth);
        maxY = Mathf.Clamp(maxY, minY + 1, atlasHeight);

        int width = maxX - minX;
        int height = maxY - minY;
        Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.SetPixels(atlas.GetPixels(minX, minY, width, height));
        cropped.Apply();

        Rect[] croppedRects = new Rect[rects.Length];
        for (int i = 0; i < rects.Length; i++)
        {
            Rect pixelRect = ToPixelRect(rects[i], atlasWidth, atlasHeight);
            float x = (pixelRect.xMin - minX) / width;
            float y = (pixelRect.yMin - minY) / height;
            float w = pixelRect.width / width;
            float h = pixelRect.height / height;
            croppedRects[i] = new Rect(x, y, w, h);
        }

        return new PackedAtlasResult(cropped, croppedRects);
    }

    private Rect ToPixelRect(Rect uvRect, int atlasWidth, int atlasHeight)
    {
        return Rect.MinMaxRect(
            Mathf.Floor(uvRect.xMin * atlasWidth),
            Mathf.Floor(uvRect.yMin * atlasHeight),
            Mathf.Ceil(uvRect.xMax * atlasWidth),
            Mathf.Ceil(uvRect.yMax * atlasHeight)
        );
    }

    private void DrawScanMessages()
    {
        foreach (string message in scanMessages)
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }
    }

    private void DrawCharacterSettings()
    {
        if (characters.Count == 0)
        {
            return;
        }

        GUILayout.Label("Characters", EditorStyles.boldLabel);
        foreach (var kv in characters)
        {
            CharacterBuildData character = kv.Value;
            CharacterValidation validation = ValidateCharacter(character);
            Color oldColor = GUI.color;
            if (!validation.isValid)
            {
                GUI.color = new Color(1f, 0.45f, 0.45f, 1f);
            }

            character.foldout = EditorGUILayout.Foldout(character.foldout, $"{character.name} ({character.clips.Count} clips)", true);
            GUI.color = oldColor;
            if (!character.foldout)
            {
                continue;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(360f));
            character.baseScale = Mathf.Max(0.01f, EditorGUILayout.FloatField("Base Scale", character.baseScale));
            character.centerOffset = EditorGUILayout.Vector2Field("Center Offset", character.centerOffset);
            character.shadowOffset = EditorGUILayout.Vector2Field("Shadow Offset", character.shadowOffset);
            character.shadowSize = EditorGUILayout.Vector2Field("Shadow Size", character.shadowSize);
            if (!validation.isValid)
            {
                EditorGUILayout.HelpBox(validation.message, MessageType.Error);
            }

            DrawClipSettings(character);
            EditorGUILayout.EndVertical();
            DrawCharacterPreviews(character, validation);
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }
    }

    private void DrawValidatedIntField(string label, ref int value, bool hasError)
    {
        Color oldBackground = GUI.backgroundColor;
        if (hasError)
        {
            GUI.backgroundColor = new Color(1f, 0.25f, 0.25f, 1f);
        }

        value = Mathf.Max(1, EditorGUILayout.IntField(label, value));
        GUI.backgroundColor = oldBackground;
    }

    private void DrawClipSettings(CharacterBuildData character)
    {
        foreach (var kv in character.clips)
        {
            ClipBuildSettings settings = EnsureClipSettings(character.name, kv.Key);
            settings.foldout = EditorGUILayout.Foldout(settings.foldout, $"{kv.Key} ({kv.Value.Count} files)", true);
            if (!settings.foldout)
            {
                continue;
            }

            EditorGUI.indentLevel++;
            settings.fps = EditorGUILayout.FloatField("FPS", Mathf.Max(0f, settings.fps));
            settings.loop = EditorGUILayout.Toggle("Loop", settings.loop);
            if (mode == InputMode.Grid)
            {
                DrawImageGridSettings(character, kv.Value);
            }
            EditorGUILayout.LabelField("Frames", CalculateClipFrameCount(character, kv.Value).ToString());
            EditorGUI.indentLevel--;
        }
    }

    private void DrawImageGridSettings(CharacterBuildData character, List<string> pngs)
    {
        EditorGUILayout.LabelField("Source Images", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        foreach (string file in pngs)
        {
            ImageGridSettings grid = EnsureImageGridSettings(character, file);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            string sizeText = tex != null ? $"{tex.width}x{tex.height}" : "missing";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{Path.GetFileName(file)} ({sizeText})");
            EditorGUILayout.BeginHorizontal();
            DrawValidatedIntField("Columns", ref grid.columns, grid.hasError);
            DrawValidatedIntField("Rows", ref grid.rows, grid.hasError);
            EditorGUILayout.EndHorizontal();

            if (tex != null && grid.columns > 0 && grid.rows > 0)
            {
                if (tex.width % grid.columns == 0 && tex.height % grid.rows == 0)
                {
                    EditorGUILayout.LabelField("Frame Size", $"{tex.width / grid.columns}x{tex.height / grid.rows}");
                }
                else
                {
                    Vector2Int frameSize = CalculateGridFrameSize(tex, grid);
                    Vector2Int paddedSize = new Vector2Int(frameSize.x * grid.columns, frameSize.y * grid.rows);
                    EditorGUILayout.LabelField("Frame Size", $"{frameSize.x}x{frameSize.y}");
                    EditorGUILayout.HelpBox($"Image size is not divisible. Build will pad transparent pixels to {paddedSize.x}x{paddedSize.y}.", MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private void DrawCharacterPreviews(CharacterBuildData character, CharacterValidation validation)
    {
        if (character.clips.Count == 0)
        {
            return;
        }

        EditorGUILayout.BeginVertical(GUILayout.MinWidth(280f));
        GUILayout.Label("Preview", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (var clipPair in character.clips)
        {
            DrawClipPreview(character, clipPair.Key, clipPair.Value, validation);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Green = anchor, Yellow = shadow center");
        EditorGUILayout.EndVertical();
    }

    private void DrawClipPreview(CharacterBuildData character, string clipName, List<string> pngs, CharacterValidation validation)
    {
        FrameSource frame = GetAnimatedPreviewFrame(character, clipName, pngs);
        if (frame == null || frame.texture == null || frame.character == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(GUILayout.Width(240f));
        EditorGUILayout.LabelField(clipName, EditorStyles.boldLabel, GUILayout.Width(220f));
        float previewSize = 210f;
        float aspect = frame.pixelRect.width / Mathf.Max(1f, frame.pixelRect.height);
        Rect previewBounds = GUILayoutUtility.GetRect(previewSize, previewSize / aspect, GUILayout.ExpandWidth(false));
        float safeBaseScale = Mathf.Max(0.01f, character.baseScale);
        Vector2 scaledSize = new Vector2(previewBounds.width * safeBaseScale, previewBounds.height * safeBaseScale);
        Rect previewRect = new Rect(
            previewBounds.center.x - scaledSize.x * 0.5f,
            previewBounds.center.y - scaledSize.y * 0.5f,
            scaledSize.x,
            scaledSize.y
        );
        EditorGUI.DrawRect(previewBounds, new Color(0.16f, 0.16f, 0.16f, 1f));
        if (IsPaddedFrame(frame))
        {
            Texture2D paddedPreview = Extract(frame);
            GUI.DrawTexture(previewRect, paddedPreview, ScaleMode.StretchToFill, true);
            DestroyImmediate(paddedPreview);
        }
        else
        {
            Rect texCoords = new Rect(
                frame.pixelRect.x / frame.texture.width,
                frame.pixelRect.y / frame.texture.height,
                frame.pixelRect.width / frame.texture.width,
                frame.pixelRect.height / frame.texture.height
            );
            GUI.DrawTextureWithTexCoords(previewRect, frame.texture, texCoords, true);
        }

        Vector2 baseSize = new Vector2(frame.pixelRect.width, frame.pixelRect.height);
        Vector2 anchorPixel = new Vector2(baseSize.x * 0.5f + character.centerOffset.x, character.centerOffset.y);
        Vector2 shadowPixel = anchorPixel + character.shadowOffset;
        Vector2 anchorGui = PixelToPreview(previewRect, anchorPixel, baseSize);
        Vector2 shadowGui = PixelToPreview(previewRect, shadowPixel, baseSize);
        float shadowRadiusX = Mathf.Max(1f, character.shadowSize.x) / Mathf.Max(1f, baseSize.x) * previewRect.width;
        float shadowRadiusY = Mathf.Max(1f, character.shadowSize.y) / Mathf.Max(1f, baseSize.y) * previewRect.height;

        Handles.BeginGUI();
        DrawPreviewEllipse(shadowGui, shadowRadiusX, shadowRadiusY, previewShadowColor);
        DrawCross(anchorGui, Color.green, 8f);
        DrawCross(shadowGui, Color.yellow, 6f);
        Handles.EndGUI();
        int frameCount = CalculateClipFrameCount(character, pngs);
        EditorGUILayout.LabelField(validation.isValid ? $"Frames: {frameCount}" : "Frame size error", GUILayout.Width(220f));
        EditorGUILayout.EndVertical();
    }

    private FrameSource GetAnimatedPreviewFrame(CharacterBuildData character, string clipName, List<string> pngs)
    {
        List<FrameSource> frames = BuildPreviewFrames(character, pngs);
        if (frames.Count == 0)
        {
            return null;
        }

        if (frames.Count == 1)
        {
            return frames[0];
        }

        ClipBuildSettings settings = EnsureClipSettings(character.name, clipName);
        float previewFps = settings.fps > 0f ? settings.fps : 10f;
        int index = Mathf.FloorToInt((float)(EditorApplication.timeSinceStartup * previewFps)) % frames.Count;
        return frames[index];
    }

    private FrameSource GetPreviewFrame(CharacterBuildData character, List<string> pngs)
    {
        if (pngs == null || pngs.Count == 0)
        {
            return null;
        }

        string file = pngs[0];
        EnsureTextureReadable(file);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
        if (tex == null)
        {
            return null;
        }

        int paddedHeight;
        Rect rect = GetPreviewRect(character, tex, file, out paddedHeight);
        return new FrameSource(tex, rect, file, character, paddedHeight);
    }

    private List<FrameSource> BuildPreviewFrames(CharacterBuildData character, List<string> pngs)
    {
        List<FrameSource> frames = new List<FrameSource>();
        foreach (string file in pngs)
        {
            EnsureTextureReadable(file);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            if (tex == null)
            {
                continue;
            }

            if (mode == InputMode.Grid)
            {
                ImageGridSettings grid = EnsureImageGridSettings(character, file);
                if (grid.columns <= 0 || grid.rows <= 0)
                {
                    continue;
                }

                Vector2Int frameSize = CalculateGridFrameSize(tex, grid);
                int paddedHeight = frameSize.y * grid.rows;
                for (int y = 0; y < grid.rows; y++)
                {
                    for (int x = 0; x < grid.columns; x++)
                    {
                        Rect rect = new Rect(
                            x * frameSize.x,
                            paddedHeight - (y + 1) * frameSize.y,
                            frameSize.x,
                            frameSize.y
                        );
                        frames.Add(new FrameSource(tex, rect, file, character, paddedHeight));
                    }
                }
            }
            else
            {
                frames.Add(new FrameSource(tex, new Rect(0, 0, tex.width, tex.height), file, character));
            }
        }

        return frames;
    }

    private Rect GetPreviewRect(CharacterBuildData character, Texture2D tex, string file, out int paddedHeight)
    {
        paddedHeight = tex.height;
        if (mode != InputMode.Grid)
        {
            return new Rect(0f, 0f, tex.width, tex.height);
        }

        ImageGridSettings grid = EnsureImageGridSettings(character, file);
        Vector2Int frameSize = CalculateGridFrameSize(tex, grid);
        paddedHeight = frameSize.y * Mathf.Max(1, grid.rows);
        int width = frameSize.x;
        int height = frameSize.y;
        width = Mathf.Clamp(width, 1, tex.width);
        height = Mathf.Max(1, height);
        return new Rect(0f, paddedHeight - height, width, height);
    }

    private int CalculateClipFrameCount(CharacterBuildData character, List<string> pngs)
    {
        int count = 0;
        foreach (string file in pngs)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            if (tex == null)
            {
                continue;
            }

            if (mode == InputMode.Grid)
            {
                ImageGridSettings grid = EnsureImageGridSettings(character, file);
                if (grid.columns <= 0 || grid.rows <= 0)
                {
                    continue;
                }

                count += grid.columns * grid.rows;
            }
            else
            {
                count++;
            }
        }

        return count;
    }

    private CharacterValidation ValidateCharacter(CharacterBuildData character)
    {
        CharacterValidation validation = new CharacterValidation();
        validation.isValid = true;

        if (mode != InputMode.Grid)
        {
            return validation;
        }

        foreach (var clipPair in character.clips)
        {
            foreach (string file in clipPair.Value)
            {
                ImageGridSettings grid = EnsureImageGridSettings(character, file);
                grid.hasError = false;
                if (grid.columns <= 0 || grid.rows <= 0)
                {
                    grid.hasError = true;
                    validation.isValid = false;
                    validation.hasFrameSizeError = true;
                    validation.message = $"{clipPair.Key}/{Path.GetFileName(file)} Columns/Rows must be greater than 0.";
                    return validation;
                }

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                if (tex == null)
                {
                    continue;
                }

            }
        }

        return validation;
    }

    private Vector2Int CalculateGridFrameSize(Texture2D tex, ImageGridSettings grid)
    {
        int columns = Mathf.Max(1, grid != null ? grid.columns : 1);
        int rows = Mathf.Max(1, grid != null ? grid.rows : 1);
        return new Vector2Int(
            Mathf.Max(1, Mathf.CeilToInt(tex.width / (float)columns)),
            Mathf.Max(1, Mathf.CeilToInt(tex.height / (float)rows))
        );
    }

    private bool IsPaddedFrame(FrameSource frame)
    {
        if (frame == null || frame.texture == null)
        {
            return false;
        }

        return frame.paddedHeight != frame.texture.height
            || frame.pixelRect.x < 0f
            || frame.pixelRect.y < 0f
            || frame.pixelRect.xMax > frame.texture.width
            || frame.pixelRect.yMax > frame.texture.height;
    }

    private void DrawAtlasPreview()
    {
        if (previewAtlas == null)
        {
            return;
        }

        GUILayout.Label("Atlas Preview", EditorStyles.boldLabel);
        float size = Mathf.Min(position.width - 20, 300);
        GUILayout.Label(previewAtlas, GUILayout.Width(size), GUILayout.Height(size));
    }

    private Vector2 PixelToPreview(Rect previewRect, Vector2 pixel, Vector2 baseSize)
    {
        float x = previewRect.x + pixel.x / Mathf.Max(1f, baseSize.x) * previewRect.width;
        float y = previewRect.yMax - pixel.y / Mathf.Max(1f, baseSize.y) * previewRect.height;
        return new Vector2(x, y);
    }

    private void DrawCross(Vector2 center, Color color, float size)
    {
        Handles.color = color;
        Handles.DrawLine(new Vector3(center.x - size, center.y, 0f), new Vector3(center.x + size, center.y, 0f));
        Handles.DrawLine(new Vector3(center.x, center.y - size, 0f), new Vector3(center.x, center.y + size, 0f));
    }

    private void DrawPreviewEllipse(Vector2 center, float radiusX, float radiusY, Color color)
    {
        Vector3[] points = new Vector3[48];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.PI * 2f;
            points[i] = new Vector3(center.x + Mathf.Cos(angle) * radiusX, center.y + Mathf.Sin(angle) * radiusY, 0f);
        }

        Handles.color = color;
        Handles.DrawAAConvexPolygon(points);
    }

    private ClipBuildSettings EnsureClipSettings(string characterName, string clipName)
    {
        CharacterBuildData character = characters.ContainsKey(characterName) ? characters[characterName] : null;
        string key = $"{characterName}/{clipName}";
        ClipBuildSettings settings = null;
        if (character != null && character.clipSettings.TryGetValue(clipName, out settings))
        {
            return settings;
        }

        settings = new ClipBuildSettings();
        settings.fps = EditorPrefs.GetFloat(GetClipPrefKey(characterName, clipName, "fps"), 10f);
        settings.loop = EditorPrefs.GetBool(GetClipPrefKey(characterName, clipName, "loop"), true);
        settings.foldout = EditorPrefs.GetBool(GetClipPrefKey(characterName, clipName, "foldout"), false);

        if (character != null)
        {
            character.clipSettings[clipName] = settings;
        }

        return settings;
    }

    private ImageGridSettings EnsureImageGridSettings(CharacterBuildData character, string imagePath)
    {
        ImageGridSettings settings = null;
        if (character != null && character.imageGridSettings.TryGetValue(imagePath, out settings))
        {
            return settings;
        }

        settings = new ImageGridSettings();
        if (character != null)
        {
            settings.columns = EditorPrefs.GetInt(GetImagePrefKey(character.name, imagePath, "columns"), Mathf.Max(1, defaultColumns));
            settings.rows = EditorPrefs.GetInt(GetImagePrefKey(character.name, imagePath, "rows"), Mathf.Max(1, defaultRows));
            character.imageGridSettings[imagePath] = settings;
        }
        else
        {
            settings.columns = Mathf.Max(1, defaultColumns);
            settings.rows = Mathf.Max(1, defaultRows);
        }

        return settings;
    }

    private void SaveCharacterSettings(CharacterBuildData character)
    {
        EditorPrefs.SetBool(GetCharacterPrefKey(character.name, "foldout"), character.foldout);
        EditorPrefs.SetFloat(GetCharacterPrefKey(character.name, "baseScale"), character.baseScale);
        EditorPrefs.SetFloat(GetCharacterPrefKey(character.name, "centerX"), character.centerOffset.x);
        EditorPrefs.SetFloat(GetCharacterPrefKey(character.name, "centerY"), character.centerOffset.y);
        EditorPrefs.SetFloat(GetCharacterPrefKey(character.name, "shadowX"), character.shadowOffset.x);
        EditorPrefs.SetFloat(GetCharacterPrefKey(character.name, "shadowY"), character.shadowOffset.y);
        EditorPrefs.SetFloat(GetCharacterPrefKey(character.name, "shadowSizeX"), character.shadowSize.x);
        EditorPrefs.SetFloat(GetCharacterPrefKey(character.name, "shadowSizeY"), character.shadowSize.y);

        foreach (var kv in character.clipSettings)
        {
            EditorPrefs.SetFloat(GetClipPrefKey(character.name, kv.Key, "fps"), kv.Value.fps);
            EditorPrefs.SetBool(GetClipPrefKey(character.name, kv.Key, "loop"), kv.Value.loop);
            EditorPrefs.SetBool(GetClipPrefKey(character.name, kv.Key, "foldout"), kv.Value.foldout);
        }

        foreach (var kv in character.imageGridSettings)
        {
            EditorPrefs.SetInt(GetImagePrefKey(character.name, kv.Key, "columns"), kv.Value.columns);
            EditorPrefs.SetInt(GetImagePrefKey(character.name, kv.Key, "rows"), kv.Value.rows);
        }
    }

    private string GetCharacterPrefKey(string characterName, string field)
    {
        string root = characterFolder != null ? AssetDatabase.GetAssetPath(characterFolder) : "none";
        return $"AnimTool_Character_{root}_{characterName}_{field}";
    }

    private string GetClipPrefKey(string characterName, string clipName, string field)
    {
        string root = characterFolder != null ? AssetDatabase.GetAssetPath(characterFolder) : "none";
        return $"AnimTool_Clip_{root}_{characterName}_{clipName}_{field}";
    }

    private string GetImagePrefKey(string characterName, string imagePath, string field)
    {
        string root = characterFolder != null ? AssetDatabase.GetAssetPath(characterFolder) : "none";
        return $"AnimTool_ImageGrid_{root}_{characterName}_{imagePath}_{field}";
    }

    private void EnsureTextureReadable(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null || importer.isReadable)
        {
            return;
        }

        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private struct PackedAtlasResult
    {
        public Texture2D texture;
        public Rect[] rects;

        public PackedAtlasResult(Texture2D texture, Rect[] rects)
        {
            this.texture = texture;
            this.rects = rects;
        }
    }

    private class AtlasLayout
    {
        public int width;
        public int height;
        public PackedRect[] rects;
        public int Area => width * height;
    }

    private struct PackedRect
    {
        public int x;
        public int y;

        public PackedRect(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    private class CharacterBuildData
    {
        public string name;
        public string path;
        public bool foldout = true;
        public float baseScale = 1f;
        public Vector2 centerOffset;
        public Vector2 shadowOffset;
        public Vector2 shadowSize = new Vector2(44f, 14f);
        public Dictionary<string, List<string>> clips = new Dictionary<string, List<string>>();
        public Dictionary<string, ClipBuildSettings> clipSettings = new Dictionary<string, ClipBuildSettings>();
        public Dictionary<string, ImageGridSettings> imageGridSettings = new Dictionary<string, ImageGridSettings>();
    }

    private class ClipBuildSettings
    {
        public float fps = 10f;
        public bool loop = true;
        public bool foldout;
    }

    private class ImageGridSettings
    {
        public int columns = 1;
        public int rows = 1;
        public bool hasError;
    }

    private class FrameSource
    {
        public Texture2D texture;
        public Rect pixelRect;
        public string assetPath;
        public CharacterBuildData character;
        public int paddedHeight;

        public FrameSource(Texture2D texture, Rect pixelRect, string assetPath, CharacterBuildData character, int paddedHeight = 0)
        {
            this.texture = texture;
            this.pixelRect = pixelRect;
            this.assetPath = assetPath;
            this.character = character;
            this.paddedHeight = paddedHeight > 0 ? paddedHeight : texture.height;
        }
    }

    private struct CharacterValidation
    {
        public bool isValid;
        public bool hasFrameSizeError;
        public string message;
    }
}
