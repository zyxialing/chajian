using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 图集导出工具
/// 将所有 Sprite 合并成图集，超过最大 size 自动分多张
/// 使用 Shelf Bin Packing 算法，紧凑排列，不强制 2 的幂次方
/// </summary>
public static class GpuAtlasExporter
{
    /// <summary>
    /// 支持的图集大小选项
    /// </summary>
    public static readonly int[] AtlasSizes = { 256, 512, 1024, 2048, 4096, 8192 };

    /// <summary>
    /// 导出结果
    /// </summary>
    public class ExportResult
    {
        public List<Texture2D> atlases = new List<Texture2D>();
        public List<SpriteUVEntry> spriteUVs = new List<SpriteUVEntry>();

        public int GetSpriteId(Sprite sprite)
        {
            for (int i = 0; i < spriteUVs.Count; i++)
            {
                if (spriteUVs[i].sprite == sprite)
                    return spriteUVs[i].spriteId;
            }
            return -1;
        }
    }

    public class SpriteUVEntry
    {
        public Sprite sprite;
        public int spriteId;
        public string spriteName;
        public int atlasIndex;
        public float uMin, vMin, uMax, vMax;
        public float originalWidth, originalHeight;
        public float cropX, cropY, cropW, cropH;
        public float pivotX, pivotY;
        public Vector2[] meshVertices;
        public Vector2[] meshUVs;
        public ushort[] meshTriangles;
    }

    /// <summary>
    /// 收集所有需要打入图集的 Sprite
    /// </summary>
    public static List<Sprite> CollectSprites(GpuRoleViewerCore core)
    {
        HashSet<Sprite> uniqueSprites = new HashSet<Sprite>();
        List<Sprite> result = new List<Sprite>();

        foreach (var slot in core.StyleSlots)
        {
            if (slot.sprite != null && uniqueSprites.Add(slot.sprite))
                result.Add(slot.sprite);

            if (!string.IsNullOrEmpty(slot.spriteFolder))
            {
                var folderSprites = LoadSpritesFromFolder(slot.spriteFolder);
                foreach (var s in folderSprites)
                {
                    if (uniqueSprites.Add(s))
                        result.Add(s);
                }
            }
        }

        foreach (var g in core.Groups)
        {
            if (!string.IsNullOrEmpty(g.groupSpritePath))
            {
                var sprites = LoadSpritesFromTexture(g.groupSpritePath);
                foreach (var s in sprites)
                {
                    if (uniqueSprites.Add(s))
                        result.Add(s);
                }
            }
            if (!string.IsNullOrEmpty(g.groupSpriteFolder))
            {
                var folderSprites = LoadSpritesFromFolder(g.groupSpriteFolder);
                foreach (var s in folderSprites)
                {
                    if (uniqueSprites.Add(s))
                        result.Add(s);
                }
            }
        }

        return result;
    }

    public static List<Sprite> LoadSpritesFromFolder(string folderPath)
    {
        List<Sprite> sprites = new List<Sprite>();
        if (string.IsNullOrEmpty(folderPath)) return sprites;

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            sprites.AddRange(LoadSpritesFromTexture(path));
        }
        return sprites;
    }

    public static List<Sprite> LoadSpritesFromTexture(string texturePath)
    {
        List<Sprite> sprites = new List<Sprite>();
        if (string.IsNullOrEmpty(texturePath)) return sprites;

        var objs = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        foreach (var obj in objs)
        {
            if (obj is Sprite sprite)
                sprites.Add(sprite);
        }
        return sprites;
    }

    /// <summary>
    /// 导出图集
    /// </summary>
    public static ExportResult ExportAtlases(List<Sprite> sprites, int maxAtlasSize, string saveFolder)
    {
        var result = new ExportResult();
        if (sprites.Count == 0) return result;

        // 按 sprite rect 面积从大到小排序
        var sorted = sprites
            .Where(s => s != null && s.texture != null)
            .OrderByDescending(s => s.rect.width * s.rect.height)
            .ToList();

        int atlasIndex = 0;
        int startIdx = 0;
        int nextSpriteId = 0;

        while (startIdx < sorted.Count)
        {
            var batch = sorted.Skip(startIdx).ToList();
            var packResult = PackSpritesCompact(batch, maxAtlasSize);

            if (packResult.texture != null)
            {
                result.atlases.Add(packResult.texture);
                string atlasPath = $"{saveFolder}/atlas_{atlasIndex}.png";
                SaveAtlasToDisk(packResult.texture, atlasPath);

                // 记录 UV 信息
                for (int i = 0; i < packResult.placements.Count; i++)
                {
                    var p = packResult.placements[i];
                    var sd = packResult.spriteDataList[p.spriteIndex];
                    float atlasW = packResult.texture.width;
                    float atlasH = packResult.texture.height;

                    const int pad = 2;

                    // 提取 Sprite 的 Tight Mesh
                    Vector2[] meshVerts, meshUVs;
                    ushort[] meshTris;
                    ExtractSpriteMesh(sd.sprite, sd.cropX, sd.cropY, sd.cropW, sd.cropH, out meshVerts, out meshUVs, out meshTris);

                    result.spriteUVs.Add(new SpriteUVEntry
                    {
                        sprite = sd.sprite,
                        spriteId = nextSpriteId++,
                        spriteName = sd.sprite.name,
                        atlasIndex = atlasIndex,
                        uMin = (p.x + pad) / atlasW,
                        vMin = (p.y + pad) / atlasH,
                        uMax = (p.x + pad + sd.cropW) / atlasW,
                        vMax = (p.y + pad + sd.cropH) / atlasH,
                        originalWidth = sd.originalWidth,
                        originalHeight = sd.originalHeight,
                        cropX = sd.cropX,
                        cropY = sd.cropY,
                        cropW = sd.cropW,
                        cropH = sd.cropH,
                        pivotX = sd.sprite.pivot.x / sd.originalWidth,
                        pivotY = sd.sprite.pivot.y / sd.originalHeight,
                        meshVertices = meshVerts,
                        meshUVs = meshUVs,
                        meshTriangles = meshTris
                    });
                }

                atlasIndex++;
                startIdx += packResult.packedCount;
            }
            else
            {
                if (batch.Count > 0)
                {
                    var single = PackSingleSprite(batch[0]);
                    if (single.texture != null)
                    {
                        result.atlases.Add(single.texture);
                        string atlasPath = $"{saveFolder}/atlas_{atlasIndex}.png";
                        SaveAtlasToDisk(single.texture, atlasPath);

                        var sd = single.spriteData;
                        float atlasW = single.texture.width;
                        float atlasH = single.texture.height;

                        Vector2[] meshVerts, meshUVs;
                        ushort[] meshTris;
                        ExtractSpriteMesh(sd.sprite, sd.cropX, sd.cropY, sd.cropW, sd.cropH, out meshVerts, out meshUVs, out meshTris);

                        result.spriteUVs.Add(new SpriteUVEntry
                        {
                            sprite = sd.sprite,
                            spriteId = nextSpriteId++,
                            spriteName = sd.sprite.name,
                            atlasIndex = atlasIndex,
                            uMin = 0,
                            vMin = 0,
                            uMax = sd.cropW / atlasW,
                            vMax = sd.cropH / atlasH,
                            originalWidth = sd.originalWidth,
                            originalHeight = sd.originalHeight,
                            cropX = sd.cropX,
                            cropY = sd.cropY,
                            cropW = sd.cropW,
                            cropH = sd.cropH,
                            pivotX = sd.sprite.pivot.x / sd.originalWidth,
                            pivotY = sd.sprite.pivot.y / sd.originalHeight,
                            meshVertices = meshVerts,
                            meshUVs = meshUVs,
                            meshTriangles = meshTris
                        });

                        atlasIndex++;
                        startIdx++;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        return result;
    }

    private struct PackResult
    {
        public Texture2D texture;
        public int packedCount;
        public List<Placement> placements;
        public List<SpriteData> spriteDataList;
    }

    private struct SinglePackResult
    {
        public Texture2D texture;
        public SpriteData spriteData;
    }

    /// <summary>
    /// 紧凑打包，裁剪透明边，返回实际大小的图集
    /// </summary>
    private static PackResult PackSpritesCompact(List<Sprite> sprites, int maxSize)
    {
        const int padding = 2;

        // 提取像素数据，裁剪透明边
        List<SpriteData> spriteDataList = new List<SpriteData>();
        foreach (var s in sprites)
        {
            if (s == null || s.texture == null) continue;
            Texture2D tex = s.texture;
            if (!tex.isReadable) continue;

            Rect spriteRect = s.rect;
            Color[] pixels = tex.GetPixels(
                (int)spriteRect.x,
                (int)spriteRect.y,
                (int)spriteRect.width,
                (int)spriteRect.height
            );

            // 裁剪透明边
            int cropX, cropY, cropW, cropH;
            CropTransparentEdges(pixels, (int)spriteRect.width, (int)spriteRect.height,
                out cropX, out cropY, out cropW, out cropH);

            // 提取裁剪后的像素
            Color[] croppedPixels = new Color[cropW * cropH];
            for (int y = 0; y < cropH; y++)
            {
                for (int x = 0; x < cropW; x++)
                {
                    croppedPixels[y * cropW + x] = pixels[(y + cropY) * (int)spriteRect.width + (x + cropX)];
                }
            }

            spriteDataList.Add(new SpriteData
            {
                sprite = s,
                width = cropW + padding * 2,
                height = cropH + padding * 2,
                pixels = croppedPixels,
                originalWidth = (int)spriteRect.width,
                originalHeight = (int)spriteRect.height,
                cropX = cropX,
                cropY = cropY,
                cropW = cropW,
                cropH = cropH
            });
        }

        if (spriteDataList.Count == 0) return new PackResult();

        // Shelf Bin Packing
        var placements = ShelfBinPack(spriteDataList, maxSize);
        if (placements == null || placements.Count == 0) return new PackResult();

        // 计算实际需要的图集大小
        int atlasW = 0, atlasH = 0;
        foreach (var p in placements)
        {
            atlasW = Mathf.Max(atlasW, p.x + p.w);
            atlasH = Mathf.Max(atlasH, p.y + p.h);
        }
        atlasW = Mathf.Clamp(atlasW, 1, maxSize);
        atlasH = Mathf.Clamp(atlasH, 1, maxSize);

        // 创建图集
        var atlas = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, false);
        Color32[] clear = new Color32[atlasW * atlasH];
        for (int i = 0; i < clear.Length; i++)
            clear[i] = new Color32(0, 0, 0, 0);
        atlas.SetPixels32(clear);

        // 把裁剪后的像素拷贝到图集对应位置
        for (int i = 0; i < placements.Count; i++)
        {
            var p = placements[i];
            var sd = spriteDataList[p.spriteIndex];
            int destX = p.x + padding;
            int destY = p.y + padding;

            atlas.SetPixels(destX, destY, sd.cropW, sd.cropH, sd.pixels);
        }

        atlas.Apply();
        return new PackResult { texture = atlas, packedCount = placements.Count, placements = placements, spriteDataList = spriteDataList };
    }

    /// <summary>
    /// 裁剪透明边，返回裁剪后的区域
    /// </summary>
    private static void CropTransparentEdges(Color[] pixels, int w, int h,
        out int cropX, out int cropY, out int cropW, out int cropH)
    {
        int minX = w, minY = h, maxX = 0, maxY = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = pixels[y * w + x];
                if (c.a > 0.01f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            // 全透明，保留 1x1
            cropX = 0; cropY = 0; cropW = 1; cropH = 1;
        }
        else
        {
            cropX = minX;
            cropY = minY;
            cropW = maxX - minX + 1;
            cropH = maxY - minY + 1;
        }
    }

    private class SpriteData
    {
        public Sprite sprite;
        public int width;       // 裁剪后 + padding
        public int height;      // 裁剪后 + padding
        public Color[] pixels;  // 裁剪后的像素
        public int originalWidth;
        public int originalHeight;
        public int cropX, cropY, cropW, cropH; // 裁剪偏移
    }

    private struct Placement
    {
        public int x, y, w, h;
        public int spriteIndex;
    }

    /// <summary>
    /// MaxRects 算法 - Best Short Side Fit
    /// 维护空闲矩形列表，每次选择浪费面积最小的位置放置 Sprite
    /// 放置后生成 left/right/bottom/top 四个剩余矩形，并做 Prune
    /// </summary>
    private static List<Placement> ShelfBinPack(List<SpriteData> sprites, int maxSize)
    {
        var placements = new List<Placement>();

        // 按面积从大到小排序
        var sorted = sprites.Select((sd, idx) => new { sd, idx })
            .OrderByDescending(x => x.sd.width * x.sd.height)
            .ToList();

        // 空闲矩形列表，初始为整张图集
        List<RectInt> freeRects = new List<RectInt>();
        freeRects.Add(new RectInt(0, 0, maxSize, maxSize));

        foreach (var item in sorted)
        {
            int idx = item.idx;
            var sd = item.sd;
            int w = sd.width;
            int h = sd.height;

            if (w > maxSize || h > maxSize)
            {
                Debug.LogWarning($"[AtlasExporter] Sprite {sd.sprite.name} ({w}x{h}) exceeds max atlas size {maxSize}");
                continue;
            }

            // Best Short Side Fit: 找最短边浪费最小的位置
            int bestIndex = -1;
            int bestShortSide = int.MaxValue;
            int bestLongSide = int.MaxValue;
            RectInt bestRect = new RectInt();

            for (int i = 0; i < freeRects.Count; i++)
            {
                var rect = freeRects[i];
                if (rect.width >= w && rect.height >= h)
                {
                    int shortSide = Mathf.Min(rect.width - w, rect.height - h);
                    int longSide = Mathf.Max(rect.width - w, rect.height - h);
                    if (shortSide < bestShortSide ||
                        (shortSide == bestShortSide && longSide < bestLongSide))
                    {
                        bestIndex = i;
                        bestShortSide = shortSide;
                        bestLongSide = longSide;
                        bestRect = rect;
                    }
                }
            }

            if (bestIndex >= 0)
            {
                placements.Add(new Placement
                {
                    x = bestRect.x,
                    y = bestRect.y,
                    w = w,
                    h = h,
                    spriteIndex = idx
                });

                // 分割所有与放置矩形相交的空闲矩形
                SplitFreeRects(freeRects, bestRect.x, bestRect.y, w, h);
                // 修剪被包含的矩形
                PruneFreeRects(freeRects);
            }
            else
            {
                // 放不下了，返回已放置的数量
                return placements.Count > 0 ? placements : null;
            }
        }

        return placements;
    }

    /// <summary>
    /// 在 (px,py) 放置一个 w×h 的矩形后，分割所有相交的空闲矩形
    /// 对每个相交的 freeRect，生成 left/right/bottom/top 四个剩余矩形
    /// </summary>
    private static void SplitFreeRects(List<RectInt> freeRects, int px, int py, int w, int h)
    {
        int count = freeRects.Count;
        for (int i = 0; i < count; i++)
        {
            var rect = freeRects[i];

            // 检查是否相交
            if (rect.x >= px + w || rect.x + rect.width <= px ||
                rect.y >= py + h || rect.y + rect.height <= py)
                continue;

            // 左侧剩余
            if (px > rect.x && px < rect.x + rect.width)
            {
                freeRects.Add(new RectInt(rect.x, rect.y, px - rect.x, rect.height));
            }
            // 右侧剩余
            if (px + w < rect.x + rect.width)
            {
                freeRects.Add(new RectInt(px + w, rect.y, rect.x + rect.width - (px + w), rect.height));
            }
            // 底部剩余
            if (py > rect.y && py < rect.y + rect.height)
            {
                freeRects.Add(new RectInt(rect.x, rect.y, rect.width, py - rect.y));
            }
            // 顶部剩余
            if (py + h < rect.y + rect.height)
            {
                freeRects.Add(new RectInt(rect.x, py + h, rect.width, rect.y + rect.height - (py + h)));
            }

            // 标记原矩形为删除
            freeRects[i] = new RectInt(0, 0, 0, 0);
        }

        freeRects.RemoveAll(r => r.width <= 0 || r.height <= 0);
    }

    /// <summary>
    /// 修剪空闲矩形列表：移除被其他矩形完全包含的矩形
    /// </summary>
    private static void PruneFreeRects(List<RectInt> rects)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            for (int j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];

                // 如果 a 包含 b，移除 b
                if (a.x <= b.x && a.y <= b.y &&
                    a.x + a.width >= b.x + b.width &&
                    a.y + a.height >= b.y + b.height)
                {
                    rects[j] = new RectInt(0, 0, 0, 0);
                }
                // 如果 b 包含 a，移除 a
                else if (b.x <= a.x && b.y <= a.y &&
                         b.x + b.width >= a.x + a.width &&
                         b.y + b.height >= a.y + a.height)
                {
                    rects[i] = new RectInt(0, 0, 0, 0);
                    break;
                }
            }
        }
        rects.RemoveAll(r => r.width <= 0 || r.height <= 0);
    }

    /// <summary>
    /// 单个 Sprite 单独一张图集
    /// </summary>
    private static SinglePackResult PackSingleSprite(Sprite sprite)
    {
        var result = new SinglePackResult { texture = null, spriteData = null };
        if (sprite == null || sprite.texture == null) return result;
        Texture2D tex = sprite.texture;
        if (!tex.isReadable) return result;

        Rect r = sprite.rect;
        Color[] pixels = tex.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);

        int cropX, cropY, cropW, cropH;
        CropTransparentEdges(pixels, (int)r.width, (int)r.height, out cropX, out cropY, out cropW, out cropH);

        Color[] croppedPixels = new Color[cropW * cropH];
        for (int y = 0; y < cropH; y++)
            for (int x = 0; x < cropW; x++)
                croppedPixels[y * cropW + x] = pixels[(y + cropY) * (int)r.width + (x + cropX)];

        var atlas = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
        atlas.SetPixels(croppedPixels);
        atlas.Apply();

        result.texture = atlas;
        result.spriteData = new SpriteData
        {
            sprite = sprite,
            originalWidth = (int)r.width,
            originalHeight = (int)r.height,
            cropX = cropX,
            cropY = cropY,
            cropW = cropW,
            cropH = cropH
        };
        return result;
    }

    private static void SaveAtlasToDisk(Texture2D atlas, string path)
    {
        byte[] bytes = atlas.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = Mathf.Max(atlas.width, atlas.height);
            importer.SaveAndReimport();
        }

        Debug.Log($"[AtlasExporter] Saved atlas: {path} ({atlas.width}x{atlas.height})");
    }

    /// <summary>
    /// 提取 Sprite 的 Tight Mesh 数据，并转换到裁剪后的坐标系
    /// </summary>
    private static void ExtractSpriteMesh(Sprite sprite, int cropX, int cropY, int cropW, int cropH,
        out Vector2[] outVertices, out Vector2[] outUVs, out ushort[] outTriangles)
    {
        ushort[] srcTris = sprite.triangles;
        Vector2[] srcVerts = sprite.vertices;

        if (srcTris == null || srcVerts == null || srcTris.Length == 0 || srcVerts.Length == 0)
        {
            outVertices = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(cropW, 0),
                new Vector2(0, cropH),
                new Vector2(cropW, cropH)
            };

            outUVs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            outTriangles = new ushort[] { 0, 1, 2, 2, 1, 3 };
            return;
        }

        float ppu = sprite.pixelsPerUnit;
        Vector2 pivot = sprite.pivot;

        List<Vector2> newVerts = new List<Vector2>();
        List<Vector2> newUVs = new List<Vector2>();
        List<ushort> newTris = new List<ushort>();

        Dictionary<int, int> vertMap = new Dictionary<int, int>();

        for (int i = 0; i < srcTris.Length; i++)
        {
            int srcIdx = srcTris[i];

            if (!vertMap.ContainsKey(srcIdx))
            {
                Vector2 v = srcVerts[srcIdx];

                // sprite.vertices 是以 pivot 为原点的 world unit
                // 转回原始 sprite 像素坐标
                float pixelX = v.x * ppu + pivot.x;
                float pixelY = v.y * ppu + pivot.y;

                                // 转到 crop 后的局部像素坐标（clamp 到裁剪区域内）
                float localX = Mathf.Clamp(pixelX - cropX, 0, cropW);
                float localY = Mathf.Clamp(pixelY - cropY, 0, cropH);

                newVerts.Add(new Vector2(localX, localY));

                // 局部 UV（0~1 范围）
                newUVs.Add(new Vector2(
                    localX / cropW,
                    localY / cropH
                ));

                vertMap[srcIdx] = newVerts.Count - 1;
            }

            newTris.Add((ushort)vertMap[srcIdx]);
        }

        outVertices = newVerts.ToArray();
        outUVs = newUVs.ToArray();
        outTriangles = newTris.ToArray();
    }

    public static long CalculateTotalArea(List<Sprite> sprites)
    {
        long total = 0;
        foreach (var s in sprites)
        {
            if (s != null && s.texture != null)
                total += s.texture.width * s.texture.height;
        }
        return total;
    }
}
