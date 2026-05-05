using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class GpuRoleStyleViewer
{
    [SerializeField] private GpuRoleStyleData _sourceStyleAsset;

        private void DrawToolbar()
    {
        GUILayout.Label("GPU Role Style Viewer", EditorStyles.boldLabel);

                if (GUILayout.Button("Open GPU Export Inspector", GUILayout.Height(30)))
        {
            GpuRoleExportInspectorWindow.Open(_core, _animBakeData);
        }

        EditorGUI.BeginChangeCheck();
        var newPrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab", _core.SourcePrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (newPrefab != null)
            {
                _core.LoadFromPrefab(newPrefab);
                AutoSave();
                RebuildPreview();
            }
            else
            {
                _core.SourcePrefab = null;
                _messages.Add("Source Prefab cleared.");
            }
            Repaint();
        }

        EditorGUI.BeginChangeCheck();
        _sourceStyleAsset = (GpuRoleStyleData)EditorGUILayout.ObjectField("Source Style Asset", _sourceStyleAsset, typeof(GpuRoleStyleData), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (_sourceStyleAsset != null)
            {
                LoadFromStyleAsset(_sourceStyleAsset);
            }
            else
            {
                _messages.Add("Source Style Asset cleared.");
            }
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Load From Prefab"))
        {
            _core.LoadFromPrefab(_core.SourcePrefab);
            AutoSave();
            RebuildPreview();
            Repaint();
        }

        if (GUILayout.Button("Random All Groups"))
        {
            RandomizeAllGroups();
            AutoSave();
            Repaint();
        }

        if (GUILayout.Button("Clear All Slots"))
        {
            _core.ClearAllSprites();
            AutoSave();
            _delayedPreviewRefresh = true;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Save Style Asset"))
        {
            SaveStyleAsset();
        }

        if (GUILayout.Button("Load Style Asset"))
        {
            if (_sourceStyleAsset == null)
            {
                _messages.Add("No Source Style Asset assigned. Drag a style asset to the field above or use the file picker.");
                return;
            }
            LoadFromStyleAsset(_sourceStyleAsset);
        }

        EditorGUILayout.EndHorizontal();

        // ===== 动画区域 =====
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);

        // ---- 直接预览播放（烘焙前） ----
                EditorGUILayout.LabelField("Direct Preview", EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();
        var newClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _animDirectClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            _animDirectClip = newClip;
            CleanupAnimPreview();
            _animDirectTime = 0f;
            _animDirectPlaying = false;
        }

        if (_animDirectClip != null && _core.SourcePrefab != null)
        {
            EditorGUILayout.BeginHorizontal();
            if (_animPreviewInstance == null)
            {
                if (GUILayout.Button("Instantiate & Play", GUILayout.Width(140)))
                {
                    CreateAnimPreviewInstance();
                    _animDirectPlaying = true;
                }
            }
            else
            {
                if (GUILayout.Button(_animDirectPlaying ? "Pause" : "Play", GUILayout.Width(80)))
                {
                    _animDirectPlaying = !_animDirectPlaying;
                    if (_animDirectPlaying && _animDirectTime >= _animDirectClip.length)
                        _animDirectTime = 0f;
                }
                if (GUILayout.Button("Stop", GUILayout.Width(60)))
                {
                    _animDirectPlaying = false;
                    _animDirectTime = 0f;
                    SampleAnimClip(0f);
                    Repaint();
                }
                if (GUILayout.Button("Destroy", GUILayout.Width(80)))
                {
                    CleanupAnimPreview();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_animPreviewInstance != null)
            {
                float maxTime = Mathf.Max(0.01f, _animDirectClip.length);
                float newTime = EditorGUILayout.Slider("Time", _animDirectTime, 0f, maxTime);
                if (newTime != _animDirectTime)
                {
                    _animDirectTime = newTime;
                    SampleAnimClip(_animDirectTime);
                    Repaint();
                }
                EditorGUILayout.LabelField($"Time: {_animDirectTime:F2}s / {_animDirectClip.length:F2}s", EditorStyles.miniLabel);
            }
        }
        else if (_animDirectClip != null)
        {
            EditorGUILayout.HelpBox("Assign a Source Prefab first.", MessageType.Info);
        }

                // ---- 烘焙保存 ----
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Bake & Save", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                var newAnimFolder = (DefaultAsset)EditorGUILayout.ObjectField("Anim Folder", _animFolder, typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    string path = newAnimFolder != null ? AssetDatabase.GetAssetPath(newAnimFolder) : "";
                    if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                        _animFolder = newAnimFolder;
                }
                if (GUILayout.Button("Bake All Animations in Folder", GUILayout.Height(24)))
                {
                    BakeAllAnimationsInFolder();
                }
                if (_animFolder != null)
                {
                    string folderPath = AssetDatabase.GetAssetPath(_animFolder);
                    var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
                    EditorGUILayout.LabelField($"Found {guids.Length} animation clips in folder.", EditorStyles.miniLabel);
                }

                // ---- 烘焙数据播放 ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Baked Data Playback", EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();
        var newBakeData = (GpuAnimationBakeData)EditorGUILayout.ObjectField("Bake Data", _animBakeData, typeof(GpuAnimationBakeData), false);
        if (EditorGUI.EndChangeCheck())
        {
            _animBakeData = newBakeData;
            _animTime = 0f;
            _animPlaying = false;
            _animSelectedAnimIndex = 0;
            if (_animBakeData != null && _renderer != null && _renderer.HasMainPreview)
            {
                ApplyAnimFrame();
                Repaint();
            }
        }

        if (_animBakeData != null && _renderer != null && _renderer.HasMainPreview)
        {
            // 多动画选择
            if (_animBakeData.animations.Count > 0)
            {
                string[] animNames = _animBakeData.animations.Select(a => a.animName).ToArray();
                EditorGUI.BeginChangeCheck();
                int newIdx = EditorGUILayout.Popup("Animation", _animSelectedAnimIndex, animNames);
                if (EditorGUI.EndChangeCheck())
                {
                    _animSelectedAnimIndex = newIdx;
                    _animTime = 0f;
                    _animPlaying = false;
                    ApplyAnimFrame();
                    Repaint();
                }
            }

            var currentAnim = GetCurrentAnimData();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_animPlaying ? "Pause" : "Play", GUILayout.Width(80)))
            {
                _animPlaying = !_animPlaying;
                if (_animPlaying && _animTime >= currentAnim.length)
                    _animTime = 0f;
            }
            if (GUILayout.Button("Stop", GUILayout.Width(60)))
            {
                _animPlaying = false;
                _animTime = 0f;
                ApplyAnimFrame();
                Repaint();
            }
            if (GUILayout.Button("Apply Bake Order", GUILayout.Width(120)))
            {
                if (_animBakeData != null && _renderer != null)
                {
                    _renderer.ReorderBySlotKeys(_animBakeData.slotKeys);
                    Debug.Log($"[StyleViewer] Reordered renderers by BakeData slotKeys ({_animBakeData.slotKeys.Count} slots)");
                    Repaint();
                }
            }
            _animLoop = EditorGUILayout.Toggle("Loop", _animLoop, GUILayout.Width(80));
            _animSpeed = EditorGUILayout.Slider("Speed", _animSpeed, 0.1f, 5f);
            EditorGUILayout.EndHorizontal();

            float maxTime = Mathf.Max(0.01f, currentAnim.length);
            float newTime = EditorGUILayout.Slider("Time", _animTime, 0f, maxTime);
            if (newTime != _animTime)
            {
                _animTime = newTime;
                ApplyAnimFrame();
                Repaint();
            }

            int totalFrames = currentAnim.totalFrames;
            int currentFrame = Mathf.Clamp(Mathf.RoundToInt(_animTime * currentAnim.frameRate), 0, totalFrames - 1);
            EditorGUILayout.LabelField($"{currentAnim.animName}  |  Frame: {currentFrame} / {totalFrames}  |  Time: {_animTime:F2}s / {currentAnim.length:F2}s", EditorStyles.miniLabel);
        }
        else if (_animBakeData != null)
        {
            EditorGUILayout.HelpBox("Load a prefab and slots first to see animation.", MessageType.Info);
        }
    }

    private void RandomizeAllGroups()
    {
        if (!_core.HasData)
        {
            _messages.Add("No slots loaded.");
            return;
        }

        HashSet<int> done = new HashSet<int>();
        int count = 0;

        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            var slot = _core.StyleSlots[i];
            if (slot.linkedGroupId >= 0)
            {
                if (done.Add(slot.linkedGroupId))
                {
                    if (_core.RandomizeLinkedGroup(slot.linkedGroupId))
                    {
                        _core.ApplyGroupExclusive(slot.linkedGroupId);
                        count++;
                    }
                }
            }
            else
            {
                var s = _core.PickRandomSpriteFromFolder(slot.spriteFolder);
                if (s != null)
                {
                    slot.sprite = s;
                    slot.color = Color.white;
                    _core.ApplySlotExclusive(i);
                    count++;
                }
            }
        }

        _messages.Add($"Randomized {count} groups/slots.");
        _delayedPreviewRefresh = true; // 修复 Bug 7：独立槽位随机后触发预览刷新
    }
}
