using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GPU 导出数据检查窗口
/// 显示当前 Style Viewer 中配置的所有数据，用于确认导出内容
/// </summary>
public class GpuRoleExportInspectorWindow : EditorWindow
{
    private GpuRoleViewerCore _core;
    private Vector2 _scrollPos;
    private int _selectedAtlasSizeIndex = 4; // 默认 4096

    // 动画烘焙
    private DefaultAsset _animFolder;
    private GpuAnimationBakeData _animBakeData;
    private bool _animBakeDone;

    public static void Open(GpuRoleViewerCore core, GpuAnimationBakeData animBakeData = null)
    {
        var window = GetWindow<GpuRoleExportInspectorWindow>("GPU Export Inspector");
        window._core = core;
        window._animBakeData = animBakeData;
        window._animBakeDone = animBakeData != null;
        window.Show();
    }

    private void OnGUI()
    {
        if (_core == null || !_core.HasData)
        {
            EditorGUILayout.HelpBox("No data. Load a prefab and configure slots in Style Viewer first.", MessageType.Info);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space();
        DrawAnimationBakeSection();
        EditorGUILayout.Space();
        DrawExportSection();

        EditorGUILayout.Space();
        DrawDataInfoSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawAnimationBakeSection()
    {
        EditorGUILayout.LabelField("=== Animation Bake ===", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newFolder = (DefaultAsset)EditorGUILayout.ObjectField("Anim Folder", _animFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            string path = newFolder != null ? AssetDatabase.GetAssetPath(newFolder) : "";
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                _animFolder = newFolder;
        }

        if (_animFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(_animFolder);
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
            EditorGUILayout.LabelField($"Found {guids.Length} animation clips.");

            if (GUILayout.Button("Bake All Animations", GUILayout.Height(24)))
            {
                BakeAllAnimations();
            }
        }

        if (_animBakeDone && _animBakeData != null)
        {
            EditorGUILayout.LabelField($"Baked: {_animBakeData.animations.Count} animations", EditorStyles.boldLabel);
            foreach (var a in _animBakeData.animations)
                EditorGUILayout.LabelField($"  - {a.animName} ({a.totalFrames} frames)");
        }
    }

    private void DrawExportSection()
    {
        EditorGUILayout.LabelField("=== Export ===", EditorStyles.boldLabel);

        // 图集大小选择
        string[] sizeNames = GpuAtlasExporter.AtlasSizes.Select(s => s.ToString()).ToArray();
        _selectedAtlasSizeIndex = EditorGUILayout.Popup("Max Atlas Size", _selectedAtlasSizeIndex, sizeNames);
        int maxAtlasSize = GpuAtlasExporter.AtlasSizes[_selectedAtlasSizeIndex];

        // 统计信息
        var allSprites = GpuAtlasExporter.CollectSprites(_core);
        long totalArea = GpuAtlasExporter.CalculateTotalArea(allSprites);
        int estimatedAtlases = Mathf.Max(1, Mathf.CeilToInt((float)totalArea / (maxAtlasSize * maxAtlasSize)));
        EditorGUILayout.LabelField($"Total Sprites: {allSprites.Count}");
        EditorGUILayout.LabelField($"Estimated Atlases: {estimatedAtlases} (max {maxAtlasSize}x{maxAtlasSize})");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Set All Textures Readable", GUILayout.Height(24)))
        {
            SetAllTexturesReadable();
        }
        if (GUILayout.Button("Export Atlases", GUILayout.Height(30)))
        {
            ExportAtlases(maxAtlasSize);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Generate Enum Script", GUILayout.Height(24)))
        {
            GenerateEnumScript();
        }
    }

    private void SetAllTexturesReadable()
    {
        var allSprites = GpuAtlasExporter.CollectSprites(_core);
        HashSet<string> processedPaths = new HashSet<string>();
        int count = 0;

        foreach (var s in allSprites)
        {
            if (s == null || s.texture == null) continue;
            string path = AssetDatabase.GetAssetPath(s.texture);
            if (string.IsNullOrEmpty(path) || processedPaths.Contains(path)) continue;
            processedPaths.Add(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                count++;
            }
        }

        Debug.Log($"[Export] Set {count} textures readable.");
    }

    private string _exportAssetPath;

    private void ExportAtlases(int maxAtlasSize)
    {
        string defaultName = _core.SourcePrefab != null ? _core.SourcePrefab.name + "_ExportData" : "ExportData";
        string path = EditorUtility.SaveFilePanelInProject("Save Export Data", defaultName, "asset", "Select save location");
        if (string.IsNullOrEmpty(path)) return;

        string relPath = System.IO.Path.GetDirectoryName(path);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        _exportAssetPath = path;

        var allSprites = GpuAtlasExporter.CollectSprites(_core);
        if (allSprites.Count == 0)
        {
            Debug.LogError("[Export] No sprites to export.");
            return;
        }

        // 1. 导出图集
        var atlasResult = GpuAtlasExporter.ExportAtlases(allSprites, maxAtlasSize, relPath);
        Debug.Log($"[Export] Exported {atlasResult.atlases.Count} atlas(es)");

        // 刷新资源数据库，让 PNG 文件被识别
        AssetDatabase.Refresh();

        // 2. 导出数据文件
        var exportData = ScriptableObject.CreateInstance<GpuRoleExportData>();
        exportData.prefabName = _core.SourcePrefab != null ? _core.SourcePrefab.name : "Unknown";

        // 图集数据 - 从磁盘重新加载纹理
        for (int i = 0; i < atlasResult.atlases.Count; i++)
        {
            string atlasPath = $"{relPath}/atlas_{i}.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            exportData.atlases.Add(new AtlasData
            {
                name = $"atlas_{i}",
                texture = tex,
                width = tex != null ? tex.width : atlasResult.atlases[i].width,
                height = tex != null ? tex.height : atlasResult.atlases[i].height
            });
        }

                // Sprite UV 数据
        foreach (var entry in atlasResult.spriteUVs)
        {
            exportData.spriteUVs.Add(new SpriteUVData
            {
                spriteId = entry.spriteId,
                spriteName = entry.spriteName,
                atlasIndex = entry.atlasIndex,
                uMin = entry.uMin,
                vMin = entry.vMin,
                uMax = entry.uMax,
                vMax = entry.vMax,
                originalWidth = entry.originalWidth,
                originalHeight = entry.originalHeight,
                cropX = entry.cropX,
                cropY = entry.cropY,
                cropW = entry.cropW,
                cropH = entry.cropH,
                pivotX = entry.pivotX,
                pivotY = entry.pivotY,
                meshVertices = entry.meshVertices,
                meshUVs = entry.meshUVs,
                meshTriangles = entry.meshTriangles,
                sourceSprite = entry.sprite
            });
        }

        // Slot 数据
        for (int i = 0; i < _core.SlotDefinitions.Count; i++)
        {
            var def = _core.SlotDefinitions[i];
            var slot = i < _core.StyleSlots.Count ? _core.StyleSlots[i] : null;

            // 收集这个 slot 所有可选的 Sprite ID
            List<int> availableIds = new List<int>();
            if (!string.IsNullOrEmpty(slot.spriteFolder))
            {
                var folderSprites = GpuAtlasExporter.LoadSpritesFromFolder(slot.spriteFolder);
                foreach (var s in folderSprites)
                {
                    int id = atlasResult.GetSpriteId(s);
                    if (id >= 0) availableIds.Add(id);
                }
            }
            // 如果当前选中的 Sprite 不在列表中，也加进去
            if (slot != null && slot.sprite != null)
            {
                int curId = atlasResult.GetSpriteId(slot.sprite);
                if (curId >= 0 && !availableIds.Contains(curId))
                    availableIds.Insert(0, curId);
            }

            bool isInGroup = slot != null && slot.linkedGroupId >= 0;
            string aliasName = isInGroup ? "---" : (slot != null && !string.IsNullOrEmpty(slot.aliasName) ? slot.aliasName : def.slotKey);

            // 从 bindPoseToRoot 矩阵分解出相对于根节点的位置/旋转/缩放
            Vector3 bindPos;
            Quaternion bindRot;
            Vector3 bindScale;
            GpuRoleUtility.DecomposeMatrix(def.bindPoseToRoot, out bindPos, out bindRot, out bindScale);

            exportData.slots.Add(new SlotExportData
            {
                slotId = i,
                slotKey = def.slotKey,
                slotName = def.slotName,
                aliasName = aliasName,
                defaultSpriteId = slot != null && slot.sprite != null ? atlasResult.GetSpriteId(slot.sprite) : -1,
                availableSpriteIds = availableIds.ToArray(),
                canBeEmpty = true,
                localPosition = bindPos,
                localEulerAngles = bindRot.eulerAngles,
                localScale = bindScale,
                sortingOrder = def.sortingOrder,
                sortingLayerId = def.sortingLayerId,
                sortingLayerName = def.sortingLayerName,
                internalOrder = def.internalOrder
            });
        }

        // 组数据
        foreach (var g in _core.Groups)
        {
            var indices = _core.GetSlotIndicesInGroup(g.groupId);

            // 收集这个 Group 的所有方案
            // 每个方案来自 groupSpriteFolder 中的一张 Multiple Sprite 纹理
            List<GroupVariant> variants = new List<GroupVariant>();
            if (!string.IsNullOrEmpty(g.groupSpriteFolder))
            {
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { g.groupSpriteFolder });
                foreach (var guid in guids)
                {
                    string texPath = AssetDatabase.GUIDToAssetPath(guid);
                    string texName = System.IO.Path.GetFileNameWithoutExtension(texPath);
                    var sprites = GpuAtlasExporter.LoadSpritesFromTexture(texPath);

                    // 按 slot 顺序找对应的 Sprite ID
                    int[] spriteIds = new int[indices.Count];
                    for (int si = 0; si < indices.Count; si++)
                    {
                        int slotIdx = indices[si];
                        var slot = _core.StyleSlots[slotIdx];
                        string subName = slot.linkedSubSpriteName;

                        // 找匹配的子 Sprite
                        int foundId = -1;
                        foreach (var sp in sprites)
                        {
                            if (sp.name == subName)
                            {
                                foundId = atlasResult.GetSpriteId(sp);
                                break;
                            }
                        }
                        spriteIds[si] = foundId;
                    }

                    variants.Add(new GroupVariant
                    {
                        variantName = texName,
                        spriteIds = spriteIds
                    });
                }
            }

            exportData.groups.Add(new GroupExportData
            {
                groupId = g.groupId,
                groupName = g.groupName,
                slotIndices = indices.ToArray(),
                variants = variants
            });
        }

        // 动画数据 - 如果有烘焙好的动画，合并进来
        if (_animBakeData != null)
        {
            if (_animBakeData.animations.Count > 0)
            {
                // 多动画模式
                foreach (var anim in _animBakeData.animations)
                {
                    var animData = new AnimExportData
                    {
                        animName = anim.animName,
                        frameRate = anim.frameRate,
                        length = anim.length,
                        totalFrames = anim.totalFrames,
                        slotKeys = _animBakeData.slotKeys,
                        frames = anim.frames
                    };
                    BakeAnimDataTexture(animData);
                    exportData.animations.Add(animData);
                }
            }
            else
            {
                // 单动画模式（兼容旧数据）
                var animData = new AnimExportData
                {
                    animName = _animBakeData.animName,
                    frameRate = _animBakeData.frameRate,
                    length = _animBakeData.length,
                    totalFrames = _animBakeData.totalFrames,
                    slotKeys = _animBakeData.slotKeys,
                    frames = _animBakeData.frames
                };
                BakeAnimDataTexture(animData);
                exportData.animations.Add(animData);
            }
        }

                // 保存数据文件（覆盖时先删除再创建）
        CombineAnimDataTextures(exportData);

        var existing = AssetDatabase.LoadAssetAtPath<GpuRoleExportData>(_exportAssetPath);
        if (existing != null)
        {
            // 先删除旧的子资源（纹理）
            var assets = AssetDatabase.LoadAllAssetsAtPath(_exportAssetPath);
            foreach (var a in assets)
            {
                if (a != existing && a is Texture2D)
                    DestroyImmediate(a, true);
            }
            EditorUtility.CopySerialized(exportData, existing);
            if (exportData.combinedAnimDataTex != null)
            {
                exportData.combinedAnimDataTex.name = "CombinedAnimData";
                AssetDatabase.AddObjectToAsset(exportData.combinedAnimDataTex, existing);
            }
            // 将动画纹理作为子资源添加到 Asset
            foreach (var anim in exportData.animations)
            {
                if (anim.animDataTex != null)
                {
                    anim.animDataTex.name = $"{anim.animName}_AnimData";
                    AssetDatabase.AddObjectToAsset(anim.animDataTex, existing);
                }
            }
            DestroyImmediate(exportData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = existing;
            Debug.Log($"[Export] Overwritten: {_exportAssetPath}");
        }
        else
        {
            AssetDatabase.CreateAsset(exportData, _exportAssetPath);
            if (exportData.combinedAnimDataTex != null)
            {
                exportData.combinedAnimDataTex.name = "CombinedAnimData";
                AssetDatabase.AddObjectToAsset(exportData.combinedAnimDataTex, exportData);
            }
            // 将动画纹理作为子资源添加到 Asset
            foreach (var anim in exportData.animations)
            {
                if (anim.animDataTex != null)
                {
                    anim.animDataTex.name = $"{anim.animName}_AnimData";
                    AssetDatabase.AddObjectToAsset(anim.animDataTex, exportData);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = exportData;
            Debug.Log($"[Export] Created: {_exportAssetPath}");
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("GPU Export Data Inspector", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField($"Source Prefab: {(_core.SourcePrefab != null ? _core.SourcePrefab.name : "None")}");
        EditorGUILayout.LabelField($"Total Slots: {_core.SlotDefinitions.Count}");
        EditorGUILayout.LabelField($"Linked Groups: {_core.Groups.Count}");
        EditorGUILayout.Space();
    }

    private Dictionary<int, bool> _groupFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> _slotFoldouts = new Dictionary<int, bool>();

    private void DrawSlotList()
    {
        EditorGUILayout.LabelField("=== Slot List ===", EditorStyles.boldLabel);

        // 先显示联动组的 slot
        foreach (var g in _core.Groups)
        {
            if (!_groupFoldouts.ContainsKey(g.groupId))
                _groupFoldouts[g.groupId] = false;

            _groupFoldouts[g.groupId] = EditorGUILayout.Foldout(_groupFoldouts[g.groupId], $"Group: {g.groupName} (ID {g.groupId})", true);
            if (_groupFoldouts[g.groupId])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Sprite Path: {g.groupSpritePath}");
                EditorGUILayout.LabelField($"Sprite Folder: {g.groupSpriteFolder}");

                var indices = _core.GetSlotIndicesInGroup(g.groupId);
                foreach (int i in indices)
                {
                    DrawSlotItem(i);
                }
                EditorGUI.indentLevel--;
            }
        }

        // 再显示非联动组的 slot
        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            if (_core.StyleSlots[i].linkedGroupId >= 0) continue;

            if (!_slotFoldouts.ContainsKey(i))
                _slotFoldouts[i] = false;

            _slotFoldouts[i] = EditorGUILayout.Foldout(_slotFoldouts[i], $"Independent Slot [{i}] {_core.StyleSlots[i].slotName}", true);
            if (_slotFoldouts[i])
            {
                EditorGUI.indentLevel++;
                DrawSlotDetail(i);
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawSlotItem(int i)
    {
        var slot = _core.StyleSlots[i];

        if (!_slotFoldouts.ContainsKey(i))
            _slotFoldouts[i] = false;

        _slotFoldouts[i] = EditorGUILayout.Foldout(_slotFoldouts[i], $"Slot [{i}] {slot.slotName}", true);
        if (_slotFoldouts[i])
        {
            EditorGUI.indentLevel++;
            DrawSlotDetail(i);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawSlotDetail(int i)
    {
        var slot = _core.StyleSlots[i];
        var slotDef = i < _core.SlotDefinitions.Count ? _core.SlotDefinitions[i] : null;

        EditorGUILayout.LabelField($"Key: {slot.slotKey}");
        EditorGUILayout.LabelField($"Alias: {slot.aliasName}");
        EditorGUILayout.LabelField($"Sprite Folder: {slot.spriteFolder}");
        EditorGUILayout.LabelField($"Current Sprite: {(slot.sprite != null ? slot.sprite.name : "None")}");
        EditorGUILayout.LabelField($"Linked Sub Sprite: {slot.linkedSubSpriteName}");
        EditorGUILayout.LabelField($"Color: {slot.color}");

        if (slotDef != null)
        {
            // 从 bindPoseToRoot 矩阵分解出相对于根节点的位置/旋转/缩放
            Vector3 bindPos;
            Quaternion bindRot;
            Vector3 bindScale;
            GpuRoleUtility.DecomposeMatrix(slotDef.bindPoseToRoot, out bindPos, out bindRot, out bindScale);

            EditorGUILayout.LabelField($"Sorting: {slotDef.sortingLayerName} / Order: {slotDef.sortingOrder}");
            EditorGUILayout.LabelField($"Bind Pos: {bindPos}");
            EditorGUILayout.LabelField($"Bind Rot: {bindRot.eulerAngles}");
            EditorGUILayout.LabelField($"Bind Scale: {bindScale}");
            EditorGUILayout.LabelField($"Draw Order: {slotDef.drawOrder} / Internal Order: {slotDef.internalOrder}");
        }
    }

    private void DrawGroupList()
    {
        if (_core.Groups.Count == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("=== Linked Groups ===", EditorStyles.boldLabel);

        foreach (var g in _core.Groups)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Group: {g.groupName} (ID {g.groupId})");
            EditorGUILayout.LabelField($"  Sprite Path: {g.groupSpritePath}");
            EditorGUILayout.LabelField($"  Sprite Folder: {g.groupSpriteFolder}");

            var slotNames = _core.GetSlotNamesInGroup(g.groupId);
            EditorGUILayout.LabelField($"  Slots ({slotNames.Count}): {string.Join(", ", slotNames)}");

            EditorGUILayout.EndVertical();
        }
    }

    private bool _showDataInfo = false;

    private void DrawDataInfoSection()
    {
        _showDataInfo = EditorGUILayout.Foldout(_showDataInfo, "=== Data Info ===", true);
        if (_showDataInfo)
        {
            DrawHeader();
            DrawSlotList();
            DrawGroupList();
        }
    }

    private void GenerateEnumScript()
    {
        // 先检查有没有导出的数据文件
        var exportData = AssetDatabase.LoadAssetAtPath<GpuRoleExportData>(_exportAssetPath);
        if (exportData == null)
        {
            Debug.LogError("[Export] No export data found. Please export first.");
            return;
        }

        string script = GpuCodeGen.GenerateEnumScript(exportData);
        string scriptPath = System.IO.Path.ChangeExtension(_exportAssetPath, ".cs");
        System.IO.File.WriteAllText(scriptPath, script);
        AssetDatabase.Refresh();

        Debug.Log($"[Export] Generated enum script: {scriptPath}");
    }

    /// <summary>
    /// 将动画帧数据烘焙成 GPU 纹理
    /// 每像素：posX, posY, scaleX, scaleY | sinRot, cosRot, colorR, colorG | colorB, colorA, visible, _
    /// </summary>
    private void BakeAnimDataTexture(AnimExportData animData)
    {
        if (animData.frames == null || animData.frames.Count == 0) return;
        if (animData.slotKeys == null || animData.slotKeys.Count == 0) return;

        int slotCount = animData.slotKeys.Count;
        int frameCount = animData.frames.Count;

        // 使用 3 张 RGBAHalf 纹理，每像素存 4 个 float
        // Tex0: posX, posY, scaleX, scaleY
        // Tex1: sinRot, cosRot, colorR, colorG
        // Tex2: colorB, colorA, visible, 0
        Texture2D tex0 = new Texture2D(slotCount, frameCount, TextureFormat.RGBAHalf, false);
        Texture2D tex1 = new Texture2D(slotCount, frameCount, TextureFormat.RGBAHalf, false);
        Texture2D tex2 = new Texture2D(slotCount, frameCount, TextureFormat.RGBAHalf, false);

        Color[] pixels0 = new Color[slotCount * frameCount];
        Color[] pixels1 = new Color[slotCount * frameCount];
        Color[] pixels2 = new Color[slotCount * frameCount];

        for (int f = 0; f < frameCount; f++)
        {
            var frame = animData.frames[f];
            for (int s = 0; s < slotCount; s++)
            {
                int idx = f * slotCount + s;

                Vector3 pos = s < frame.positions.Count ? frame.positions[s] : Vector3.zero;
                Quaternion rot = s < frame.rotations.Count ? frame.rotations[s] : Quaternion.identity;
                Vector3 scale = s < frame.scales.Count ? frame.scales[s] : Vector3.one;
                Color color = s < frame.colors.Count ? frame.colors[s] : Color.white;

                // 旋转转 sin/cos
                float rotAngle = rot.eulerAngles.z * Mathf.Deg2Rad;
                float sinRot = Mathf.Sin(rotAngle);
                float cosRot = Mathf.Cos(rotAngle);

                pixels0[idx] = new Color(pos.x, pos.y, scale.x, scale.y);
                pixels1[idx] = new Color(sinRot, cosRot, color.r, color.g);
                pixels2[idx] = new Color(color.b, color.a, 1f, 0f); // visible=1
            }
        }

        tex0.SetPixels(pixels0);
        tex0.Apply(false, false);
        tex1.SetPixels(pixels1);
        tex1.Apply(false, false);
        tex2.SetPixels(pixels2);
        tex2.Apply(false, false);

        // 合并成一张纹理（水平拼接）
        int totalWidth = slotCount * 3;
        Texture2D combined = new Texture2D(totalWidth, frameCount, TextureFormat.RGBAHalf, false);
        Graphics.CopyTexture(tex0, 0, 0, 0, 0, slotCount, frameCount, combined, 0, 0, 0, 0);
        Graphics.CopyTexture(tex1, 0, 0, 0, 0, slotCount, frameCount, combined, 0, 0, slotCount, 0);
        Graphics.CopyTexture(tex2, 0, 0, 0, 0, slotCount, frameCount, combined, 0, 0, slotCount * 2, 0);

        animData.animDataTex = combined;
        animData.animDataTexWidth = totalWidth;
        animData.animDataTexHeight = frameCount;

        Debug.Log($"[Export] 烘焙动画纹理: {animData.animName} slotCount={slotCount} frameCount={frameCount} tex={totalWidth}x{frameCount}");

        Object.DestroyImmediate(tex0);
        Object.DestroyImmediate(tex1);
        Object.DestroyImmediate(tex2);
    }

    private void CombineAnimDataTextures(GpuRoleExportData exportData)
    {
        if (exportData == null || exportData.animations == null || exportData.animations.Count == 0)
            return;

        int width = 0;
        int height = 0;
        for (int i = 0; i < exportData.animations.Count; i++)
        {
            AnimExportData anim = exportData.animations[i];
            if (anim == null || anim.animDataTex == null)
                continue;

            width = Mathf.Max(width, anim.animDataTexWidth);
            height += anim.animDataTexHeight;
        }

        if (width <= 0 || height <= 0)
            return;

        Texture2D combined = new Texture2D(width, height, TextureFormat.RGBAHalf, false);
        Color clear = new Color(0f, 0f, 1f, 1f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        combined.SetPixels(pixels);

        int y = 0;
        for (int i = 0; i < exportData.animations.Count; i++)
        {
            AnimExportData anim = exportData.animations[i];
            if (anim == null || anim.animDataTex == null)
                continue;

            anim.animDataTexY = y;
            Color[] animPixels = anim.animDataTex.GetPixels();
            combined.SetPixels(0, y, anim.animDataTexWidth, anim.animDataTexHeight, animPixels);
            y += anim.animDataTexHeight;
        }

        combined.Apply(false, false);
        exportData.combinedAnimDataTex = combined;
        exportData.combinedAnimDataTexWidth = width;
        exportData.combinedAnimDataTexHeight = height;

        Debug.Log($"[Export] Combined animation texture: anims={exportData.animations.Count} tex={width}x{height}");
    }

    private void BakeAllAnimations()
    {
        var prefab = _core.SourcePrefab;
        if (prefab == null)
        {
            Debug.LogError("[Export] No source prefab assigned.");
            return;
        }
        if (_animFolder == null)
        {
            Debug.LogError("[Export] No animation folder selected.");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(_animFolder);
        var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
        if (guids.Length == 0)
        {
            Debug.LogError("[Export] No animation clips found in folder.");
            return;
        }

        // 烘焙第一个动画获取 slotKeys
        string firstClipPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        var firstClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(firstClipPath);
        var firstData = GpuAnimationBaker.BakeFromPrefab(prefab, firstClip, _core.SlotDefinitions);
        if (firstData == null)
        {
            Debug.LogError("[Export] Failed to bake first animation.");
            return;
        }

        var combinedData = ScriptableObject.CreateInstance<GpuAnimationBakeData>();
        combinedData.animName = prefab.name + "_AllAnims";
        combinedData.frameRate = firstData.frameRate;
        combinedData.slotKeys = firstData.slotKeys;
        combinedData.frames = firstData.frames;

        combinedData.animations.Add(new SingleAnimationData
        {
            animName = firstClip.name,
            frameRate = firstData.frameRate,
            length = firstData.length,
            totalFrames = firstData.totalFrames,
            frames = firstData.frames
        });

        // 烘焙其余动画
        for (int i = 1; i < guids.Length; i++)
        {
            string clipPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) continue;

            var animData = GpuAnimationBaker.BakeFromPrefab(prefab, clip, _core.SlotDefinitions);
            if (animData == null) continue;

            combinedData.animations.Add(new SingleAnimationData
            {
                animName = clip.name,
                frameRate = animData.frameRate,
                length = animData.length,
                totalFrames = animData.totalFrames,
                frames = animData.frames
            });
        }

        _animBakeData = combinedData;
        _animBakeDone = true;
        Debug.Log($"[Export] Baked {combinedData.animations.Count} animations.");
        Repaint();
    }
}
