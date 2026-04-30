using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GpuRoleSlotWindow : EditorWindow
{
    private GameObject sourcePrefab;
    private DefaultAsset outputFolder;
    private string assetName = "GpuRoleSlotData";
    private Vector2 scrollPosition;
    private Vector2 previewDrag;
    private PreviewRenderUtility previewUtility;
    private GameObject previewInstance;
    private GameObject previewSourcePrefab;
    private readonly List<GpuRoleSlot> previewSlots = new List<GpuRoleSlot>();
    private readonly List<string> messages = new List<string>();

    [MenuItem("ZFramework/Window/GPU Role/Slot Scanner")]
    public static void Open()
    {
        GetWindow<GpuRoleSlotWindow>("GPU Role Slot Scanner");
    }

    private void OnDisable()
    {
        CleanupPreview();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("GPU Role Slot Scanner", EditorStyles.boldLabel);
        sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab", sourcePrefab, typeof(GameObject), false);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
        assetName = EditorGUILayout.TextField("Asset Name", assetName);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan Prefab"))
        {
            Scan();
        }

        if (GUILayout.Button("Create Slot Data Asset"))
        {
            CreateAsset();
        }
        EditorGUILayout.EndHorizontal();

        DrawRolePreview();

        DrawMessages();
        DrawPreview();

        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        messages.Clear();
        previewSlots.Clear();

        if (sourcePrefab == null)
        {
            messages.Add("Source Prefab is empty.");
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(sourcePrefab);
        if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("Source Prefab must be a prefab asset.");
            return;
        }

        previewSlots.AddRange(ScanPrefabSlots(sourcePrefab));
        messages.Add($"Scanned {previewSlots.Count} SpriteRenderer slots from {prefabPath}.");
    }

    private void CreateAsset()
    {
        if (previewSlots.Count == 0)
        {
            Scan();
        }

        if (previewSlots.Count == 0 || sourcePrefab == null)
        {
            return;
        }

        string folderPath = GetOutputFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        GpuRoleSlotData data = ScriptableObject.CreateInstance<GpuRoleSlotData>();
        data.sourcePrefab = sourcePrefab;
        data.sourcePrefabPath = AssetDatabase.GetAssetPath(sourcePrefab);
        data.generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.slots = new List<GpuRoleSlot>(previewSlots);

        string safeName = string.IsNullOrWhiteSpace(assetName) ? "GpuRoleSlotData" : assetName.Trim();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{safeName}.asset");
        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = data;
        messages.Add($"Created slot data asset: {assetPath}");
    }

    private void DrawRolePreview()
    {
        GUILayout.Space(8);
        GUILayout.Label("Role Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Rebuild Preview"))
        {
            RebuildPreview();
        }

        if (GUILayout.Button("Reset Preview"))
        {
            RebuildPreview();
        }
        EditorGUILayout.EndHorizontal();

        Rect rect = GUILayoutUtility.GetRect(320f, 360f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

        if (sourcePrefab == null)
        {
            GUI.Label(rect, "Assign a Source Prefab to preview.");
            return;
        }

        EnsurePreviewInstance();
        if (previewUtility == null || previewInstance == null)
        {
            GUI.Label(rect, "Preview could not be created.");
            return;
        }

        HandlePreviewInput(rect);
        Texture texture = RenderPreview(rect);
        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }
    }

    private void EnsurePreviewInstance()
    {
        if (previewInstance != null && previewSourcePrefab == sourcePrefab && previewUtility != null)
        {
            return;
        }

        RebuildPreview();
    }

    private void RebuildPreview()
    {
        CleanupPreview();

        if (sourcePrefab == null)
        {
            return;
        }

        previewUtility = new PreviewRenderUtility();
        previewUtility.camera.orthographic = true;
        previewUtility.camera.clearFlags = CameraClearFlags.Color;
        previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        previewUtility.lights[0].intensity = 1f;
        previewUtility.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
        previewUtility.lights[1].intensity = 0.5f;

        previewInstance = Instantiate(sourcePrefab);
        previewInstance.hideFlags = HideFlags.HideAndDontSave;
        previewInstance.transform.position = Vector3.zero;
        previewInstance.transform.rotation = Quaternion.identity;
        previewInstance.transform.localScale = Vector3.one;
        DisablePreviewBehaviours(previewInstance);
        previewUtility.AddSingleGO(previewInstance);
        previewSourcePrefab = sourcePrefab;
    }

    private Texture RenderPreview(Rect rect)
    {
        Bounds bounds = CalculatePreviewBounds();
        float aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(1f, rect.height));
        float size = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect, 0.5f);
        Vector3 center = bounds.center;

        previewUtility.BeginPreview(rect, GUIStyle.none);
        previewUtility.camera.orthographicSize = size * 1.25f;
        previewUtility.camera.transform.position = center + new Vector3(previewDrag.x, previewDrag.y, -10f);
        previewUtility.camera.transform.rotation = Quaternion.identity;
        previewUtility.camera.nearClipPlane = 0.01f;
        previewUtility.camera.farClipPlane = 100f;
        previewUtility.Render();
        return previewUtility.EndPreview();
    }

    private Bounds CalculatePreviewBounds()
    {
        SpriteRenderer[] renderers = previewInstance.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(previewInstance.transform.position, Vector3.one);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one * 2f);
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event current = Event.current;
        if (current.type == EventType.MouseDrag && rect.Contains(current.mousePosition) && current.button == 0)
        {
            previewDrag += current.delta * 0.01f;
            current.Use();
            Repaint();
        }
    }

    private void DisablePreviewBehaviours(GameObject obj)
    {
        foreach (MonoBehaviour behaviour in obj.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }

        foreach (Animator animator in obj.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
        }
    }

    private void CleanupPreview()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        if (previewUtility != null)
        {
            previewUtility.Cleanup();
            previewUtility = null;
        }

        previewSourcePrefab = null;
    }

    private List<GpuRoleSlot> ScanPrefabSlots(GameObject prefab)
    {
        List<GpuRoleSlot> slots = new List<GpuRoleSlot>();
        Transform root = prefab.transform;
        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);

        Array.Sort(renderers, (a, b) => string.CompareOrdinal(GetPath(root, a.transform), GetPath(root, b.transform)));

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            Transform transform = renderer.transform;
            Sprite sprite = renderer.sprite;
            string spritePath = sprite != null ? AssetDatabase.GetAssetPath(sprite) : string.Empty;

            GpuRoleSlot slot = new GpuRoleSlot();
            slot.slotId = i;
            slot.slotKey = BuildSlotKey(root, transform);
            slot.slotName = BuildSlotName(transform, renderer);
            slot.objectName = transform.name;
            slot.path = GetPath(root, transform);
            slot.parentPath = transform.parent != null ? GetPath(root, transform.parent) : string.Empty;
            slot.depth = GetDepth(root, transform);

            slot.activeSelf = transform.gameObject.activeSelf;
            slot.activeInHierarchy = transform.gameObject.activeInHierarchy;
            slot.rendererEnabled = renderer.enabled;
            slot.defaultVisible = transform.gameObject.activeSelf && renderer.enabled && sprite != null;

            slot.sortingLayerId = renderer.sortingLayerID;
            slot.sortingLayerName = renderer.sortingLayerName;
            slot.sortingOrder = renderer.sortingOrder;
            slot.color = renderer.color;

            slot.spriteName = sprite != null ? sprite.name : string.Empty;
            slot.spriteAssetPath = spritePath;
            slot.spriteGuid = !string.IsNullOrEmpty(spritePath) ? AssetDatabase.AssetPathToGUID(spritePath) : string.Empty;
            slot.spriteRectSize = sprite != null ? sprite.rect.size : Vector2.zero;
            slot.spritePivotPixels = sprite != null ? sprite.pivot : Vector2.zero;
            slot.spritePivotNormalized = sprite != null
                ? new Vector2(sprite.pivot.x / Mathf.Max(1f, sprite.rect.width), sprite.pivot.y / Mathf.Max(1f, sprite.rect.height))
                : Vector2.zero;
            slot.spriteBoundsSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.zero;
            slot.pixelsPerUnit = sprite != null ? sprite.pixelsPerUnit : 0f;

            slot.localPosition = transform.localPosition;
            slot.localEulerAngles = transform.localEulerAngles;
            slot.localScale = transform.localScale;
            slot.bindPoseToRoot = root.worldToLocalMatrix * transform.localToWorldMatrix;

            slots.Add(slot);
        }

        return slots;
    }

    private string GetOutputFolderPath()
    {
        if (outputFolder != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(outputFolder);
            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return selectedPath;
            }
        }

        const string root = "Assets/ZFramework_noad";
        const string generated = "Assets/ZFramework_noad/Generated";
        if (!AssetDatabase.IsValidFolder(root))
        {
            AssetDatabase.CreateFolder("Assets", "ZFramework_noad");
        }

        if (!AssetDatabase.IsValidFolder(generated))
        {
            AssetDatabase.CreateFolder(root, "Generated");
        }

        AssetDatabase.Refresh();
        return generated;
    }

    private string BuildSlotName(Transform transform, SpriteRenderer renderer)
    {
        string name = transform.name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = renderer.sprite != null ? renderer.sprite.name : "Slot";
        }

        return name;
    }

    private string BuildSlotKey(Transform root, Transform transform)
    {
        string path = GetPath(root, transform);
        return path
            .Replace(root.name + "/", string.Empty)
            .Replace("/", ".")
            .Replace(" ", string.Empty)
            .Trim('.');
    }

    private string GetPath(Transform root, Transform target)
    {
        if (target == root)
        {
            return root.name;
        }

        Stack<string> names = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private int GetDepth(Transform root, Transform target)
    {
        int depth = 0;
        Transform current = target;
        while (current != null && current != root)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private void DrawMessages()
    {
        foreach (string message in messages)
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }
    }

    private void DrawPreview()
    {
        if (previewSlots.Count == 0)
        {
            return;
        }

        GUILayout.Space(8);
        GUILayout.Label($"Slots ({previewSlots.Count})", EditorStyles.boldLabel);

        foreach (GpuRoleSlot slot in previewSlots)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"#{slot.slotId} {slot.slotName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Key", slot.slotKey);
            EditorGUILayout.LabelField("Path", slot.path);
            EditorGUILayout.LabelField("Sort", $"{slot.sortingLayerName} / {slot.sortingOrder}");
            EditorGUILayout.LabelField("Visible", slot.defaultVisible.ToString());
            EditorGUILayout.LabelField("Sprite", string.IsNullOrEmpty(slot.spriteName) ? "<empty>" : slot.spriteName);
            if (slot.spriteRectSize != Vector2.zero)
            {
                EditorGUILayout.LabelField("Sprite Size", $"{slot.spriteRectSize.x} x {slot.spriteRectSize.y}");
                EditorGUILayout.LabelField("Pivot", $"{slot.spritePivotPixels.x}, {slot.spritePivotPixels.y}");
            }
            EditorGUILayout.EndVertical();
        }
    }
}
