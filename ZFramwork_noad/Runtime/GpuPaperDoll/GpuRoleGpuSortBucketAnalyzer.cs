using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estimates how many sort layers and draw batches remain when exact internalOrder values
/// are grouped into larger buckets. This is an analysis-only component and does not affect rendering.
/// </summary>
public class GpuRoleGpuSortBucketAnalyzer : MonoBehaviour
{
    public GpuRoleExportData exportData;
    public GpuRoleGpuManager manager;

    [Header("Bucket Test")]
    public int[] bucketSizes = new[] { 1, 2, 5, 10, 20, 50, 100 };
    public bool includeHiddenDefaultSlots = true;
    public bool logOnStart = true;
    public bool drawOnGUI = true;

    [Header("GUI")]
    public Vector2 guiPosition = new Vector2(8f, 112f);
    public Vector2 guiSize = new Vector2(420f, 260f);

    private readonly List<Result> _results = new List<Result>();
    private int _exactOrderCount;
    private int _atlasCount;
    private int _animationCount;
    private int _visibleDefaultSlotCount;

    private struct Result
    {
        public int bucketSize;
        public int bucketCount;
        public int estimatedDraws;
    }

    private void Start()
    {
        Analyze();
        if (logOnStart)
            LogResults();
    }

    [ContextMenu("Analyze Sort Buckets")]
    public void Analyze()
    {
        ResolveReferences();
        _results.Clear();
        _exactOrderCount = 0;
        _atlasCount = 0;
        _animationCount = 0;
        _visibleDefaultSlotCount = 0;

        if (exportData == null)
            return;

        HashSet<int> atlasSet = new HashSet<int>();
        HashSet<int> exactOrders = new HashSet<int>();

        if (exportData.animations != null)
            _animationCount = Mathf.Max(1, exportData.animations.Count);
        else
            _animationCount = 1;

        if (exportData.slots != null)
        {
            for (int i = 0; i < exportData.slots.Count; i++)
            {
                SlotExportData slot = exportData.slots[i];
                if (slot == null)
                    continue;

                int spriteId = slot.defaultSpriteId;
                if (!includeHiddenDefaultSlots && spriteId < 0)
                    continue;

                if (spriteId >= 0)
                {
                    _visibleDefaultSlotCount++;
                    if (TryFindSpriteUV(spriteId, out SpriteUVData uv))
                        atlasSet.Add(uv.atlasIndex);
                }

                exactOrders.Add(slot.internalOrder);
            }
        }

        _atlasCount = Mathf.Max(1, atlasSet.Count);
        _exactOrderCount = exactOrders.Count;

        if (bucketSizes == null || bucketSizes.Length == 0)
            bucketSizes = new[] { 1, 2, 5, 10, 20, 50, 100 };

        for (int i = 0; i < bucketSizes.Length; i++)
        {
            int bucketSize = Mathf.Max(1, bucketSizes[i]);
            HashSet<int> buckets = new HashSet<int>();

            if (exportData.slots != null)
            {
                for (int s = 0; s < exportData.slots.Count; s++)
                {
                    SlotExportData slot = exportData.slots[s];
                    if (slot == null)
                        continue;

                    int bucket = Mathf.FloorToInt(slot.internalOrder / (float)bucketSize);
                    buckets.Add(bucket);
                }
            }

            int bucketCount = buckets.Count;
            _results.Add(new Result
            {
                bucketSize = bucketSize,
                bucketCount = bucketCount,
                estimatedDraws = bucketCount * _atlasCount
            });
        }
    }

    private void LogResults()
    {
        if (exportData == null)
        {
            Debug.LogWarning("[GpuRoleGpuSortBucketAnalyzer] Missing ExportData.");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[GpuRoleGpuSortBucketAnalyzer]");
        sb.AppendLine($"Slots={exportData.slots.Count}, VisibleDefaultSlots={_visibleDefaultSlotCount}, Atlases={_atlasCount}, Animations={_animationCount}");
        sb.AppendLine($"Exact internalOrder layers={_exactOrderCount}, CurrentManagerBatches={(manager != null ? manager.BatchCount : 0)}");
        for (int i = 0; i < _results.Count; i++)
        {
            Result r = _results[i];
            sb.AppendLine($"bucketSize={r.bucketSize}: layers={r.bucketCount}, estimatedDrawsPerAnim={r.estimatedDraws}");
        }

        Debug.Log(sb.ToString());
    }

    private void OnGUI()
    {
        if (!drawOnGUI)
            return;

        Rect rect = new Rect(guiPosition, guiSize);
        GUI.Box(rect, string.Empty);
        GUILayout.BeginArea(rect);
        GUILayout.Label("GpuRole Sort Bucket Analyzer");

        if (exportData == null)
        {
            GUILayout.Label("Missing ExportData");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"Slots: {exportData.slots.Count}  Visible defaults: {_visibleDefaultSlotCount}");
        GUILayout.Label($"Atlases: {_atlasCount}  Exact layers: {_exactOrderCount}");
        GUILayout.Label($"Current manager batches: {(manager != null ? manager.BatchCount : 0)}");
        GUILayout.Space(4f);

        for (int i = 0; i < _results.Count; i++)
        {
            Result r = _results[i];
            GUILayout.Label($"Bucket {r.bucketSize}: layers {r.bucketCount}, draw/anim ~ {r.estimatedDraws}");
        }

        if (GUILayout.Button("Analyze"))
        {
            Analyze();
            LogResults();
        }

        GUILayout.EndArea();
    }

    private void ResolveReferences()
    {
        if (manager == null)
            manager = Object.FindObjectOfType<GpuRoleGpuManager>();

        if (exportData == null)
        {
            GpuRoleGpuAgent agent = Object.FindObjectOfType<GpuRoleGpuAgent>();
            if (agent != null)
                exportData = agent.exportData;
        }
    }

    private bool TryFindSpriteUV(int spriteId, out SpriteUVData result)
    {
        result = null;
        if (exportData == null || exportData.spriteUVs == null)
            return false;

        for (int i = 0; i < exportData.spriteUVs.Count; i++)
        {
            SpriteUVData uv = exportData.spriteUVs[i];
            if (uv != null && uv.spriteId == spriteId)
            {
                result = uv;
                return true;
            }
        }

        return false;
    }
}
