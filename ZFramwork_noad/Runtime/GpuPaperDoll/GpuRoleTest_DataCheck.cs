using UnityEngine;

/// <summary>
/// 测试脚本：检查导出数据是否正确
/// 挂到场景中，拖入 exportData，运行后看控制台输出
/// </summary>
public class GpuRoleTest_DataCheck : MonoBehaviour
{
    public GpuRoleExportData exportData;

    private void Start()
    {
        if (exportData == null)
        {
            Debug.LogError("[Test] exportData is null!");
            return;
        }

        string log = "";
        log += "=== GpuRoleExportData Check ===\n";
        log += $"Prefab: {exportData.prefabName}\n";
        log += $"Atlases: {exportData.atlases.Count}\n";
        log += $"SpriteUVs: {exportData.spriteUVs.Count}\n";
        log += $"Slots: {exportData.slots.Count}\n";
        log += $"Groups: {exportData.groups.Count}\n";
        log += $"Animations: {exportData.animations.Count}\n";

        // 打印图集信息
        for (int i = 0; i < exportData.atlases.Count; i++)
        {
            var atlas = exportData.atlases[i];
            log += $"  Atlas[{i}]: {atlas.name} {atlas.width}x{atlas.height} texture={(atlas.texture != null ? atlas.texture.name : "NULL")}\n";
        }

        // 打印 Slot 信息
        for (int i = 0; i < exportData.slots.Count; i++)
        {
            var slot = exportData.slots[i];
            log += $"  Slot[{i}]: key={slot.slotKey} name={slot.slotName} alias={slot.aliasName} defaultSpriteId={slot.defaultSpriteId} availableIds={slot.availableSpriteIds.Length} canBeEmpty={slot.canBeEmpty}\n";
        }

        // 打印 Group 信息
        for (int i = 0; i < exportData.groups.Count; i++)
        {
            var group = exportData.groups[i];
            log += $"  Group[{i}]: name={group.groupName} slots={string.Join(",", group.slotIndices)} variants={group.variants.Count}\n";
            for (int v = 0; v < group.variants.Count; v++)
            {
                log += $"    Variant[{v}]: {group.variants[v].variantName} spriteIds={string.Join(",", group.variants[v].spriteIds)}\n";
            }
        }

        // 打印动画信息
        for (int i = 0; i < exportData.animations.Count; i++)
        {
            var anim = exportData.animations[i];
            log += $"  Anim[{i}]: {anim.animName} rate={anim.frameRate} length={anim.length} frames={anim.totalFrames} slotKeys={anim.slotKeys.Count}\n";
        }

        log += "=== Check Complete ===";
        Debug.Log(log);
    }
}
