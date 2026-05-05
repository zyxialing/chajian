using UnityEngine;

public class GpuRoleTest_SpriteRendererCheck : MonoBehaviour
{
    public GpuRoleExportData exportData;

    [Header("测试 slot")]
    public int[] testSlotIndices = { 1, 5, 15, 20, 25, 27 };

    [Header("你的 PPU")]
    public float ppu = 32f;

    [Header("排列间距")]
    public float spacing = 1.2f;

    private void Start()
    {
        if (exportData == null)
        {
            Debug.LogError("exportData 为空");
            return;
        }

        for (int i = 0; i < testSlotIndices.Length; i++)
        {
            int slotIndex = testSlotIndices[i];

            if (slotIndex < 0 || slotIndex >= exportData.slots.Count)
                continue;

            CreateSpriteRenderer(slotIndex, i);
        }
    }

    private void CreateSpriteRenderer(int slotIndex, int showIndex)
    {
        var slot = exportData.slots[slotIndex];

        int spriteId = slot.defaultSpriteId;

        if (spriteId < 0 && slot.availableSpriteIds != null && slot.availableSpriteIds.Length > 0)
            spriteId = slot.availableSpriteIds[0];

        if (spriteId < 0)
        {
            Debug.LogWarning($"slot={slotIndex} 没有 spriteId");
            return;
        }

        SpriteUVData uv = FindUV(spriteId);

        if (uv == null)
        {
            Debug.LogWarning($"找不到 UV，slot={slotIndex}, spriteId={spriteId}");
            return;
        }

        if (uv.atlasIndex < 0 || uv.atlasIndex >= exportData.atlases.Count)
        {
            Debug.LogWarning($"atlasIndex 错误，slot={slotIndex}, atlasIndex={uv.atlasIndex}");
            return;
        }

        var atlas = exportData.atlases[uv.atlasIndex];

        if (atlas == null || atlas.texture == null)
        {
            Debug.LogWarning($"atlas texture 为空，slot={slotIndex}");
            return;
        }

        Texture2D tex = atlas.texture;

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        int atlasW = tex.width;
        int atlasH = tex.height;

        int x = Mathf.RoundToInt(uv.uMin * atlasW);
        int y = Mathf.RoundToInt(uv.vMin * atlasH);
        int w = Mathf.RoundToInt((uv.uMax - uv.uMin) * atlasW);
        int h = Mathf.RoundToInt((uv.vMax - uv.vMin) * atlasH);

        Rect rect = new Rect(x, y, w, h);

        Vector2 pivot = new Vector2(uv.pivotX, uv.pivotY);

        Sprite sprite = Sprite.Create(
            tex,
            rect,
            pivot,
            ppu,
            0,
            SpriteMeshType.FullRect
        );

        GameObject go = new GameObject($"Check_{slotIndex}_{slot.slotName}_spriteId_{spriteId}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(showIndex * spacing, 0f, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = slot.sortingOrder;

        Debug.Log(
            $"[SpriteCheck] Slot[{slotIndex}] {slot.slotName}" +
            $" spriteId={spriteId}" +
            $" atlas=({atlasW},{atlasH})" +
            $" uv=({uv.uMin:F6},{uv.vMin:F6},{uv.uMax:F6},{uv.vMax:F6})" +
            $" rect=({x},{y},{w},{h})" +
            $" crop=({uv.cropW},{uv.cropH})" +
            $" pivot=({uv.pivotX},{uv.pivotY})"
        );

        if (w != uv.cropW || h != uv.cropH)
        {
            Debug.LogWarning(
                $"[SpriteCheck] 尺寸不一致！slot={slotIndex}" +
                $" rect=({w},{h}) crop=({uv.cropW},{uv.cropH})"
            );
        }
    }

    private SpriteUVData FindUV(int spriteId)
    {
        if (exportData.spriteUVs == null)
            return null;

        for (int i = 0; i < exportData.spriteUVs.Count; i++)
        {
            if (exportData.spriteUVs[i].spriteId == spriteId)
                return exportData.spriteUVs[i];
        }

        return null;
    }
}