using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GPU 角色换装编辑窗口
/// 数据由 GpuRoleViewerCore（ScriptableObject）持久化，不因重编译丢失
/// </summary>
public partial class GpuRoleStyleViewer : EditorWindow
{
    [SerializeField] private GpuRoleViewerCore _core;
    private GpuRolePreviewRenderer _renderer;
    private Vector2 _scrollPos;
    private readonly List<string> _messages = new List<string>();

    private bool _delayedPreviewRefresh; // 延迟刷新标记

        // ===== 动画播放状态 =====
    [SerializeField] private GpuAnimationBakeData _animBakeData;
    private float _animTime;
    private bool _animPlaying;
    private float _animSpeed = 1f;
    private bool _animLoop = true;
    private int _animSelectedAnimIndex;

        // ===== 烘焙 =====
        [SerializeField] private DefaultAsset _animFolder;

        // ===== 直接预览播放（烘焙前预览） =====
        [SerializeField] private AnimationClip _animDirectClip;
        private GameObject _animPreviewInstance;
        private PreviewRenderUtility _animPreviewUtil;
        private Vector2 _animPreviewDrag;
        private bool _animDirectPlaying;
        private float _animDirectTime;
        private Vector3 _animRootLocalPos;
        private Quaternion _animRootLocalRot;
        private Vector3 _animRootLocalScale;
        private Bounds _animInitialBounds;
        private bool _hasAnimInitialBounds;

    private const string PrefsKey_StyleAssetPath = "GpuRoleStyleViewer_StyleAssetPath";

    [MenuItem("ZFramework/Window/GPU Role")]
    public static void Open()
    {

        GetWindow<GpuRoleStyleViewer>("GPU Role Style Viewer");
    }

        private void OnEnable()
    {
        if (_core == null)
        {
            _core = ScriptableObject.CreateInstance<GpuRoleViewerCore>();
            _core.hideFlags = HideFlags.HideAndDontSave;
            // 从 EditorPrefs 恢复上次的数据
            _core.LoadFromEditorPrefs();
        }

        _renderer = new GpuRolePreviewRenderer();

                if (_core.HasData)
            _renderer.BuildMainPreview(_core.SlotDefinitions, _core.StyleSlots,
                _core.RootPosition, _core.RootRotation, _core.RootScale);

        // 恢复上次使用的 Style Asset
        string lastAssetPath = EditorPrefs.GetString(PrefsKey_StyleAssetPath, "");
        if (!string.IsNullOrEmpty(lastAssetPath))
        {
            var lastAsset = AssetDatabase.LoadAssetAtPath<GpuRoleStyleData>(lastAssetPath);
            if (lastAsset != null)
            {
                _sourceStyleAsset = lastAsset;
            }
        }
    }

    private void OnDisable()
    {
        AutoSave();
        // 保存当前 Style Asset 路径
        if (_sourceStyleAsset != null)
        {
            string path = AssetDatabase.GetAssetPath(_sourceStyleAsset);
            EditorPrefs.SetString(PrefsKey_StyleAssetPath, path);
        }
        CleanupAnimPreview();
        if (_renderer != null)
        {
            _renderer.CleanupAll();
            _renderer = null;
        }
    }

    private void OnDestroy()
    {
        AutoSave();
        CleanupAnimPreview();
    }

    private void CleanupAnimPreview()
    {
        if (_animPreviewInstance != null)
        {
            Object.DestroyImmediate(_animPreviewInstance);
            _animPreviewInstance = null;
        }
        if (_animPreviewUtil != null)
        {
            _animPreviewUtil.Cleanup();
            _animPreviewUtil = null;
        }
        _animDirectPlaying = false;
        _animDirectTime = 0f;
    }

    private void AutoSave()
    {
        if (_core != null)
            _core.SaveToEditorPrefs();
    }

    private void Update()
    {
                // 烘焙数据播放驱动
        if (_animPlaying && _animBakeData != null && _renderer != null && _renderer.HasMainPreview)
        {
            var currentAnim = GetCurrentAnimData();
            if (currentAnim != null && currentAnim.length > 0)
            {
                _animTime += 0.02f * _animSpeed;
                if (_animTime >= currentAnim.length)
                {
                    if (_animLoop)
                        _animTime = 0f;
                    else
                    {
                        _animTime = currentAnim.length;
                        _animPlaying = false;
                    }
                }

                ApplyAnimFrame();
                Repaint();
            }
        }

                // 直接预览播放驱动
        if (_animDirectPlaying && _animPreviewInstance != null && _animDirectClip != null)
        {
            float length = _animDirectClip.length;
            if (length > 0)
            {
                _animDirectTime += 0.02f * _animSpeed;
                if (_animDirectTime >= length)
                {
                    if (_animLoop)
                        _animDirectTime = 0f;
                    else
                    {
                        _animDirectTime = length;
                        _animDirectPlaying = false;
                    }
                }

                SampleAnimClip(_animDirectTime);
                Repaint();
            }
        }
    }

    private void CreateAnimPreviewInstance()
    {
                CleanupAnimPreview();
        if (_core.SourcePrefab == null || _animDirectClip == null) return;

                _animPreviewUtil = new PreviewRenderUtility();
        _animPreviewUtil.camera.orthographic = true;
        _animPreviewUtil.camera.clearFlags = CameraClearFlags.Color;
        _animPreviewUtil.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        _animPreviewUtil.lights[0].intensity = 1f;
        _animPreviewUtil.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
        _animPreviewUtil.lights[1].intensity = 0.5f;

                _animPreviewInstance = Object.Instantiate(_core.SourcePrefab);
                _animPreviewInstance.hideFlags = HideFlags.HideAndDontSave;

                // 禁用所有 MonoBehaviour 和 Animator，防止干扰
                foreach (var mb in _animPreviewInstance.GetComponentsInChildren<MonoBehaviour>(true))
                    mb.enabled = false;
                foreach (var anim in _animPreviewInstance.GetComponentsInChildren<Animator>(true))
                    anim.enabled = false;
                // 也禁用 Animation 组件
                foreach (var animation in _animPreviewInstance.GetComponentsInChildren<Animation>(true))
                    animation.enabled = false;

                Transform root = _animPreviewInstance.transform;
                // 强制根节点归零（动画不应驱动根节点）
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
                _animRootLocalPos = Vector3.zero;
                _animRootLocalRot = Quaternion.identity;
                _animRootLocalScale = Vector3.one;
                _animPreviewUtil.AddSingleGO(_animPreviewInstance);
                _animDirectTime = 0f;
                SampleAnimClip(0f);
                _animInitialBounds = CalculateAnimPreviewBounds();
                _hasAnimInitialBounds = true;
    }

    private Bounds CalculateAnimPreviewBounds()
    {
                if (_animPreviewInstance == null) return new Bounds(Vector3.zero, Vector3.one * 2f);
                var renderers = _animPreviewInstance.GetComponentsInChildren<SpriteRenderer>(true);
                bool hasBounds = false;
                Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
                foreach (var r in renderers)
                {
                    if (r == null || r.sprite == null || !r.enabled) continue;
                    if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                    else bounds.Encapsulate(r.bounds);
                }
                return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one * 2f);
    }

    private void SampleAnimClip(float time)
    {
        if (_animPreviewInstance == null || _animDirectClip == null) return;
        Transform root = _animPreviewInstance.transform;
        _animDirectClip.SampleAnimation(_animPreviewInstance, time);
        root.localPosition = _animRootLocalPos;
        root.localRotation = _animRootLocalRot;
        root.localScale = _animRootLocalScale;
    }

        private void DrawAnimDirectPreview(Rect rect)
    {
        if (_animPreviewInstance == null || _animPreviewUtil == null) return;

        Event e = Event.current;
        if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) && e.button == 0)
        {
            _animPreviewDrag += e.delta * 0.01f;
            e.Use();
            Repaint();
        }

        Bounds bounds = _hasAnimInitialBounds ? _animInitialBounds : CalculateAnimPreviewBounds();
        float aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(1f, rect.height));
        float size = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect, 0.5f);

        _animPreviewUtil.BeginPreview(rect, GUIStyle.none);
        _animPreviewUtil.camera.orthographicSize = size * 1.25f;
        _animPreviewUtil.camera.transform.position = bounds.center + new Vector3(_animPreviewDrag.x, _animPreviewDrag.y, -10f);
        _animPreviewUtil.camera.transform.rotation = Quaternion.identity;
        _animPreviewUtil.camera.nearClipPlane = 0.01f;
        _animPreviewUtil.camera.farClipPlane = 100f;
        _animPreviewUtil.Render(true, false);
        Texture tex = _animPreviewUtil.EndPreview();
        if (tex != null)
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
    }

        private SingleAnimationData GetCurrentAnimData()
    {
        if (_animBakeData == null) return null;
        if (_animBakeData.animations.Count > 0)
        {
            int idx = Mathf.Clamp(_animSelectedAnimIndex, 0, _animBakeData.animations.Count - 1);
            return _animBakeData.animations[idx];
        }
        // 兼容旧数据（单动画模式）
        return new SingleAnimationData
        {
            animName = _animBakeData.animName,
            frameRate = _animBakeData.frameRate,
            length = _animBakeData.length,
            totalFrames = _animBakeData.totalFrames,
            frames = _animBakeData.frames
        };
    }

    private void ApplyAnimFrame()
    {
        if (_animBakeData == null) return;
        var currentAnim = GetCurrentAnimData();
        if (currentAnim == null || currentAnim.frames.Count == 0) return;
        int frame = Mathf.RoundToInt(_animTime * currentAnim.frameRate);
        frame = Mathf.Clamp(frame, 0, currentAnim.frames.Count - 1);
        _renderer?.ApplyAnimationFrame(currentAnim.frames[frame], _animBakeData.slotKeys);
    }

    private void OnGUI()
    {
        if (_core == null)
        {
            EditorGUILayout.LabelField("Core data lost. Reopen the window.");
            return;
        }

        if (_renderer == null)
        {
            _renderer = new GpuRolePreviewRenderer();

            if (_core.HasData)
                _renderer.BuildMainPreview(_core.SlotDefinitions, _core.StyleSlots,
                    _core.RootPosition, _core.RootRotation, _core.RootScale);
        }

        // 延迟刷新：避免在 Picker 关闭等事件流中直接操作 PreviewRenderUtility
        if (_delayedPreviewRefresh)
        {
            _delayedPreviewRefresh = false;
            _renderer?.UpdateMainPreview(_core.SlotDefinitions, _core.StyleSlots);
            Repaint();
        }

        // 左右布局：左边可滚动，右边固定预览
        EditorGUILayout.BeginHorizontal();

        // 左边：可滚动区域
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawToolbar();
        DrawGroupManagement();
        DrawSlotList();
        DrawMessages();
        EditorGUILayout.EndScrollView();

        // 右边：固定预览
        DrawPreviewArea();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMessages()
    {
        foreach (var m in _messages)
            EditorGUILayout.HelpBox(m, MessageType.Info);
    }

            private void BakeAllAnimationsInFolder()
            {
                var prefab = _core.SourcePrefab;
                if (prefab == null)
                {
                    _messages.Add("No source prefab assigned.");
                    return;
                }
                if (!_core.HasData)
                {
                    _messages.Add("No slots loaded. Load from prefab first.");
                    return;
                }
                if (_animFolder == null)
                {
                    _messages.Add("No animation folder selected.");
                    return;
                }

                string folderPath = AssetDatabase.GetAssetPath(_animFolder);
                var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
                if (guids.Length == 0)
                {
                    _messages.Add("No animation clips found in folder.");
                    return;
                }

                                // 选择保存路径
                                string path = EditorUtility.SaveFilePanelInProject("Save Animation Bake Data",
                    prefab.name + "_AllAnims", "asset", "Select save location");
                Debug.Log($"[BakeAll] Save path returned: '{path}'");
                if (string.IsNullOrEmpty(path))
                {
                    _messages.Add("Save cancelled.");
                    return;
                }
                _messages.Add($"Saving to: {path}");

                // 先烘焙第一个动画，获取 slotKeys
                string firstClipPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var firstClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(firstClipPath);
                var firstData = GpuAnimationBaker.BakeFromPrefab(prefab, firstClip, _core.SlotDefinitions);
                if (firstData == null)
                {
                    _messages.Add("Failed to bake first animation.");
                    return;
                }

                // 用第一个动画的数据作为基础，把其他动画加进去
                var combinedData = ScriptableObject.CreateInstance<GpuAnimationBakeData>();
                combinedData.animName = prefab.name + "_AllAnims";
                combinedData.frameRate = firstData.frameRate;
                combinedData.slotKeys = firstData.slotKeys;
                combinedData.frames = firstData.frames;

                // 第一个动画作为 animations[0]
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

                AssetDatabase.CreateAsset(combinedData, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = combinedData;
                _messages.Add($"Baked {combinedData.animations.Count} animations into: {path}");
            }

    /// <summary>
    /// 绘制文件夹选择字段，支持从 Project 窗口拖拽文件夹
    /// 使用 ObjectField 样式，可拖拽文件夹或点击选择
    /// </summary>
    private void DrawFolderField(string label, string currentPath, System.Action<string> onPathChanged)
    {
        // 加载当前路径对应的 DefaultAsset
        DefaultAsset folderAsset = null;
        if (!string.IsNullOrEmpty(currentPath))
            folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(currentPath);

        EditorGUI.BeginChangeCheck();
        var newAsset = (DefaultAsset)EditorGUILayout.ObjectField(label, folderAsset, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            string path = newAsset != null ? AssetDatabase.GetAssetPath(newAsset) : "";
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                onPathChanged?.Invoke(path);
                GUI.changed = true;
            }
        }

        // 额外处理文件夹拖拽（ObjectField 对 DefaultAsset 拖拽支持不完善）
        Rect dropRect = GUILayoutUtility.GetLastRect();
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (dropRect.Contains(evt.mousePosition))
            {
                bool hasFolder = false;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string objPath = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(objPath) && AssetDatabase.IsValidFolder(objPath))
                    {
                        hasFolder = true;
                        break;
                    }
                }

                if (hasFolder)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            string objPath = AssetDatabase.GetAssetPath(obj);
                            if (!string.IsNullOrEmpty(objPath) && AssetDatabase.IsValidFolder(objPath))
                            {
                                onPathChanged?.Invoke(objPath);
                                break;
                            }
                        }
                        GUI.changed = true;
                    }
                    evt.Use();
                }
            }
        }
    }

}
