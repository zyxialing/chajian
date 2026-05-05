using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GPU role stress-test spawner.
/// Random animation, outfit, color, and scale are applied only when spawning.
/// Update only moves transforms when moveAgents is enabled.
/// </summary>
public class GpuRoleGpuSpawner : MonoBehaviour
{
    [Header("References")]
    public GpuRoleGpuManager manager;
    public GpuRoleExportData exportData;

    [Header("Spawn")]
    public bool spawnOnStart = true;
    public int roleCount = 1000;
    public int columns = 50;
    public float spacing = 2.2f;

    [Header("Default Animation")]
    public int animIndex = 0;
    public float playbackSpeed = 1f;

    [Header("Default Visual")]
    public float scale = 1f;
    public Color color = Color.white;

    [Header("Random Seed")]
    public bool useRandomSeed = true;
    public int randomSeed = 12345;

    [Header("Random Animation")]
    public bool randomAnim = true;
    public int randomAnimMinIndex = 0;
    public int randomAnimMaxIndex = -1;

    [Header("Random Playback Speed")]
    public bool randomPlaybackSpeed;
    public Vector2 playbackSpeedRange = new Vector2(0.85f, 1.15f);

    [Header("Random Group Variants")]
    public bool randomGroupVariants = true;
    [Range(0f, 1f)] public float groupNoneChance;

    [Header("Random Independent Slots")]
    public bool randomIndependentSlots = true;
    [Range(0f, 1f)] public float independentSlotNoneChance = 0.1f;

    [Header("Random Color")]
    public bool randomColor;
    [Range(0f, 1f)] public float randomColorSaturation = 0.25f;
    [Range(0f, 1f)] public float randomColorValue = 1f;

    [Header("Random Scale")]
    public bool randomScale;
    public Vector2 scaleRange = new Vector2(0.9f, 1.1f);

    [Header("Move")]
    public bool moveAgents;
    public float moveAmplitude = 0.25f;
    public float moveSpeed = 2f;

    [Header("Debug")]
    public bool logSpawnResult = true;

    private readonly List<Transform> _spawned = new List<Transform>();
    private Vector3[] _basePositions;
    private HashSet<int> _groupSlotSet;

    private void Start()
    {
        if (spawnOnStart)
            Spawn();
    }

    private void Update()
    {
        if (!moveAgents || _basePositions == null)
            return;

        float t = Time.time * moveSpeed;
        for (int i = 0; i < _spawned.Count; i++)
        {
            Transform tr = _spawned[i];
            if (tr == null) continue;

            Vector3 pos = _basePositions[i];
            pos.x += Mathf.Sin(t + i * 0.173f) * moveAmplitude;
            pos.y += Mathf.Cos(t * 0.73f + i * 0.119f) * moveAmplitude;
            tr.position = pos;
        }
    }

    [ContextMenu("Respawn GPU Roles")]
    public void Spawn()
    {
        Clear();
        ResolveReferences();

        if (!ValidateReferences())
            return;

        if (useRandomSeed)
            Random.InitState(randomSeed);

        roleCount = Mathf.Max(1, roleCount);
        columns = Mathf.Max(1, columns);
        spacing = Mathf.Max(0.01f, spacing);

        _groupSlotSet = BuildGroupSlotSet();
        _basePositions = new Vector3[roleCount];

        for (int i = 0; i < roleCount; i++)
        {
            int x = i % columns;
            int y = i / columns;
            Vector3 pos = transform.position + new Vector3(x * spacing, -y * spacing, 0f);
            _basePositions[i] = pos;

            GameObject go = new GameObject($"GpuRoleGpuAgent_{i:0000}");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;

            GpuRoleGpuAgent agent = go.AddComponent<GpuRoleGpuAgent>();
            agent.exportData = exportData;
            agent.manager = manager;
            agent.animIndex = GetRandomAnimIndex();
            agent.playbackSpeed = GetRandomPlaybackSpeed();
            agent.scale = GetRandomScale();
            agent.color = GetRandomColor();

            _spawned.Add(go.transform);
            go.SetActive(true);

            ApplyRandomStyle(agent);
        }

        manager.MarkAgentStyleDirty(null);

        if (logSpawnResult)
        {
            Debug.Log(
                $"[GpuRoleGpuSpawner] Spawned {roleCount} GPU roles. " +
                $"RandomAnim={randomAnim}, RandomGroups={randomGroupVariants}, " +
                $"RandomSlots={randomIndependentSlots}, RandomColor={randomColor}, RandomScale={randomScale}"
            );
        }
    }

    [ContextMenu("Clear GPU Roles")]
    public void Clear()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            Transform tr = _spawned[i];
            if (tr == null) continue;

            if (Application.isPlaying)
                Destroy(tr.gameObject);
            else
                DestroyImmediate(tr.gameObject);
        }

        _spawned.Clear();
        _basePositions = null;
    }

    private void OnDestroy()
    {
        Clear();
    }

    private void ResolveReferences()
    {
        if (manager == null)
            manager = Object.FindObjectOfType<GpuRoleGpuManager>();
    }

    private bool ValidateReferences()
    {
        if (manager == null)
        {
            Debug.LogError("[GpuRoleGpuSpawner] Missing GpuRoleGpuManager.");
            return false;
        }

        if (exportData == null)
        {
            Debug.LogError("[GpuRoleGpuSpawner] Missing GpuRoleExportData.");
            return false;
        }

        if (exportData.slots == null || exportData.slots.Count == 0)
        {
            Debug.LogError("[GpuRoleGpuSpawner] ExportData has no slots.");
            return false;
        }

        if (exportData.animations == null || exportData.animations.Count == 0)
        {
            Debug.LogError("[GpuRoleGpuSpawner] ExportData has no animations.");
            return false;
        }

        return true;
    }

    private void ApplyRandomStyle(GpuRoleGpuAgent agent)
    {
        if (agent == null || exportData == null)
            return;

        if (randomGroupVariants)
            ApplyRandomGroupVariants(agent);

        if (randomIndependentSlots)
            ApplyRandomIndependentSlots(agent);
    }

    private void ApplyRandomGroupVariants(GpuRoleGpuAgent agent)
    {
        if (exportData.groups == null)
            return;

        for (int g = 0; g < exportData.groups.Count; g++)
        {
            GroupExportData group = exportData.groups[g];
            if (group == null || group.variants == null || group.variants.Count == 0)
                continue;

            if (groupNoneChance > 0f && Random.value < groupNoneChance)
            {
                agent.SetGroupVariant(group.groupName, string.Empty);
                continue;
            }

            int variantIndex = Random.Range(0, group.variants.Count);
            agent.SetGroupVariant(group.groupId, variantIndex);
        }
    }

    private void ApplyRandomIndependentSlots(GpuRoleGpuAgent agent)
    {
        if (exportData.slots == null)
            return;

        if (_groupSlotSet == null)
            _groupSlotSet = BuildGroupSlotSet();

        for (int s = 0; s < exportData.slots.Count; s++)
        {
            if (_groupSlotSet.Contains(s))
                continue;

            SlotExportData slot = exportData.slots[s];
            if (slot == null)
                continue;

            if (slot.canBeEmpty && independentSlotNoneChance > 0f && Random.value < independentSlotNoneChance)
            {
                agent.SetSlotVisible(slot.slotKey, false, true);
                continue;
            }

            if (slot.availableSpriteIds == null || slot.availableSpriteIds.Length == 0)
                continue;

            int spriteId = slot.availableSpriteIds[Random.Range(0, slot.availableSpriteIds.Length)];
            agent.SetSlotSprite(slot.slotKey, spriteId, true);
        }
    }

    private int GetRandomAnimIndex()
    {
        int maxAnim = exportData.animations.Count - 1;
        if (!randomAnim)
            return Mathf.Clamp(animIndex, 0, maxAnim);

        int min = Mathf.Clamp(randomAnimMinIndex, 0, maxAnim);
        int max = randomAnimMaxIndex < 0 ? maxAnim : Mathf.Clamp(randomAnimMaxIndex, 0, maxAnim);
        if (max < min) max = min;

        return Random.Range(min, max + 1);
    }

    private float GetRandomPlaybackSpeed()
    {
        if (!randomPlaybackSpeed)
            return playbackSpeed;

        float min = Mathf.Min(playbackSpeedRange.x, playbackSpeedRange.y);
        float max = Mathf.Max(playbackSpeedRange.x, playbackSpeedRange.y);
        return Random.Range(min, max);
    }

    private float GetRandomScale()
    {
        if (!randomScale)
            return scale;

        float min = Mathf.Min(scaleRange.x, scaleRange.y);
        float max = Mathf.Max(scaleRange.x, scaleRange.y);
        return Random.Range(min, max);
    }

    private Color GetRandomColor()
    {
        if (!randomColor)
            return color;

        Color c = Color.HSVToRGB(Random.value, randomColorSaturation, randomColorValue);
        c.a = color.a;
        return c;
    }

    private HashSet<int> BuildGroupSlotSet()
    {
        HashSet<int> set = new HashSet<int>();
        if (exportData == null || exportData.groups == null)
            return set;

        for (int g = 0; g < exportData.groups.Count; g++)
        {
            GroupExportData group = exportData.groups[g];
            if (group == null || group.slotIndices == null)
                continue;

            for (int i = 0; i < group.slotIndices.Length; i++)
                set.Add(group.slotIndices[i]);
        }

        return set;
    }
}
