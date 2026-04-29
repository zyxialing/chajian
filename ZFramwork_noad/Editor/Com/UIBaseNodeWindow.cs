using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

public class UIBaseNodeWindow : EditorWindow
{
    UIBaseNode _target;

    ReorderableList nodeList;
    private bool scanFoldout = true;
    private bool scanByPrefix = true;
    private bool scanBySuffix = false;
    private bool scanByComponent = true;
    private bool skipExisting = true;
    private bool skipNoUsefulComponent = true;
    private bool includeInactive = true;
    private int maxDepth = -1;
    private string prefixText = "btn,img,icon,txt,text,toggle,slider,input,scroll,panel,item";
    private string suffixText = "";
    private int lastScanCount = 0;
    private List<string> selectedComponentTypes = new List<string>();
    private Vector2 componentScroll;

    private class ScanCandidate
    {
        public Transform transform;
        public string tag;
        public List<string> types;
    }

    // 缓存上次选中的 GameObject，用于检测选择变化
    private GameObject lastSelectedObject;

    private static readonly string[] UsefulTypes =
    {
        "UnityEngine.RectTransform",
        "UnityEngine.UI.Button",
        "UnityEngine.UI.Image",
        "UnityEngine.UI.Text",
        "TMPro.TextMeshProUGUI",
        "TMPro.TMP_Text",
        "UnityEngine.UI.Toggle",
        "UnityEngine.UI.Slider",
        "UnityEngine.UI.ScrollRect",
        "UnityEngine.UI.InputField",
        "TMPro.TMP_InputField",
        "UnityEngine.UI.Dropdown",
        "TMPro.TMP_Dropdown"
    };

    private void OnEnable()
    {
        this.titleContent.text = "UIBaseNode";
        this.minSize = new Vector2(600f, 600f);
        this.maxSize = new Vector2(600f, 600f);
        TryInitTarget();
    }

    private void OnSelectionChange()
    {
        // 当 Unity 编辑器选中对象变化时自动更新窗口
        TryInitTarget();
        Repaint();
    }

    /// <summary>
    /// 尝试初始化目标，如果选中对象没有 UIBaseNode 则置空并显示提示
    /// </summary>
    private void TryInitTarget()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            _target = null;
            nodeList = null;
            return;
        }

        // 如果选中的是同一个对象，不用重复初始化
        if (selected == lastSelectedObject && _target != null)
        {
            return;
        }

        lastSelectedObject = selected;
        _target = selected.GetComponent<UIBaseNode>();

        if (_target != null)
        {
            InitNodes();
        }
        else
        {
            nodeList = null;
        }
    }

    private void InitNodes()
    {
        if (_target == null) return;

        nodeList = new ReorderableList(_target.nodes, typeof(UINodeInfo));
        nodeList.elementHeightCallback = index => UINodeInfoEditorDrawer.GetElementHeight(_target.nodes[index], _target);
        nodeList.drawElementCallback = (Rect rect, int index, bool selected, bool focused) =>
        {
            UINodeInfoEditorDrawer.DrawElement(rect, _target.nodes[index], _target, _target);
        };
        nodeList.drawHeaderCallback = (Rect rect) =>
        {
            GUI.Label(rect, "UI Node List");
        };
        nodeList.onRemoveCallback = (ReorderableList list) =>
        {
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            EditorUtility.SetDirty(_target);
        };
        nodeList.onAddCallback = (ReorderableList list) =>
        {
            UINodeInfo newNode = new UINodeInfo();
            _target.nodes.Add(newNode);
            EditorUtility.SetDirty(_target);
        };
    }

    private Vector2 scrollView;
    private void OnGUI()
    {
        // 先检测选中对象变化（OnSelectionChange 可能不及时）
        CheckSelectionChanged();

        // 如果没有目标，显示提示
        if (_target == null || nodeList == null)
        {
            EditorGUILayout.HelpBox("请选中一个带有 UIBaseNode 组件的 GameObject", MessageType.Info);
            if (GUILayout.Button("刷新") || Event.current.commandName == "ObjectSelectorSelection")
            {
                TryInitTarget();
            }
            return;
        }

        FindBack();

        EditorGUILayout.Space();
        DrawScanPanel();
        EditorGUILayout.Space();

        scrollView = GUILayout.BeginScrollView(scrollView);
        if (nodeList != null)
            nodeList.DoLayoutList();
        GUILayout.EndScrollView();
    }

    /// <summary>
    /// 校验目标是否有效
    /// </summary>
    private bool ValidateTarget()
    {
        if (_target == null)
        {
            EditorUtility.DisplayDialog("提示", "请先选中一个带有 UIBaseNode 组件的 GameObject", "确定");
            return false;
        }

        if (_target.gameObject == null)
        {
            EditorUtility.DisplayDialog("提示", "目标 GameObject 已被销毁", "确定");
            _target = null;
            nodeList = null;
            return false;
        }

        if (_target.nodes == null)
        {
            _target.nodes = new List<UINodeInfo>();
            EditorUtility.SetDirty(_target);
        }

        return true;
    }

    /// <summary>
    /// 在 OnGUI 中检测选中对象是否变化
    /// </summary>
    private void CheckSelectionChanged()
    {
        GameObject currentSelected = Selection.activeGameObject;
        if (currentSelected != lastSelectedObject)
        {
            TryInitTarget();
            // 切换目标时重置组件过滤状态
            selectedComponentTypes.Clear();
            componentScroll = Vector2.zero;
        }
    }

    private void DrawScanPanel()
    {
        scanFoldout = EditorGUILayout.Foldout(scanFoldout, "自动扫描设置", true);
        if (!scanFoldout)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        scanByPrefix = EditorGUILayout.ToggleLeft("按前缀", scanByPrefix);
        if (scanByPrefix)
        {
            prefixText = EditorGUILayout.TextField("前缀", prefixText);
        }

        scanBySuffix = EditorGUILayout.ToggleLeft("按后缀", scanBySuffix);
        if (scanBySuffix)
        {
            suffixText = EditorGUILayout.TextField("后缀", suffixText);
        }

        scanByComponent = EditorGUILayout.ToggleLeft("按组件", scanByComponent);
        if (scanByComponent)
        {
            DrawComponentSelector();
        }

        skipExisting = EditorGUILayout.ToggleLeft("跳过已添加节点", skipExisting);
        skipNoUsefulComponent = EditorGUILayout.ToggleLeft("跳过无可选组件节点", skipNoUsefulComponent);
        includeInactive = EditorGUILayout.ToggleLeft("包含隐藏节点", includeInactive);
        maxDepth = EditorGUILayout.IntField("最大扫描深度(-1全部)", maxDepth);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("扫描子节点"))
        {
            lastScanCount = ScanChildren();
        }
        if (GUILayout.Button("清除所有节点"))
        {
            ClearAllNodes();
        }
        EditorGUILayout.LabelField("上次新增: " + lastScanCount, GUILayout.Width(100f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawComponentSelector()
    {
        if (!ValidateTarget()) return;

        List<string> componentTypes = GetChildComponentTypes();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("组件过滤", EditorStyles.boldLabel);
        if (GUILayout.Button("全选", GUILayout.Width(50f)))
        {
            selectedComponentTypes = new List<string>(componentTypes);
        }
        if (GUILayout.Button("清空", GUILayout.Width(50f)))
        {
            selectedComponentTypes.Clear();
        }
        EditorGUILayout.EndHorizontal();

        if (componentTypes.Count == 0)
        {
            EditorGUILayout.LabelField("没有可选择的组件");
        }
        else
        {
            float height = Mathf.Min(130f, componentTypes.Count * 22f + 6f);
            componentScroll = EditorGUILayout.BeginScrollView(componentScroll, GUILayout.Height(height));
            foreach (string typeName in componentTypes)
            {
                bool selected = selectedComponentTypes.Contains(typeName);
                bool newSelected = EditorGUILayout.ToggleLeft(GetDisplayName(typeName), selected);
                if (newSelected != selected)
                {
                    if (newSelected)
                    {
                        selectedComponentTypes.Add(typeName);
                    }
                    else
                    {
                        selectedComponentTypes.Remove(typeName);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
    }

    private void ClearAllNodes()
    {
        if (_target == null)
        {
            return;
        }

        if (!EditorUtility.DisplayDialog("清除所有节点", "确定清空当前 UI 节点列表吗？", "清空", "取消"))
        {
            return;
        }

        _target.nodes.Clear();
        lastScanCount = 0;
        EditorUtility.SetDirty(_target);
        Repaint();
    }

    private int ScanChildren()
    {
        if (!ValidateTarget())
        {
            return 0;
        }

        if (_target.transform == null)
        {
            EditorUtility.DisplayDialog("错误", "目标 GameObject 已被销毁", "确定");
            TryInitTarget();
            return 0;
        }

        List<ScanCandidate> candidates = GetScanCandidates();
        int addCount = 0;
        foreach (ScanCandidate candidate in candidates)
        {
            UINodeInfo node = new UINodeInfo();
            node.transform = candidate.transform;
            node.tag = candidate.tag;
            node.useMultiTypes = true;
            node.types = candidate.types;
            node.type = candidate.types.Count > 0 ? candidate.types[0] : string.Empty;
            node.foldout = false;
            _target.nodes.Add(node);
            addCount++;
        }

        EditorUtility.SetDirty(_target);
        Repaint();
        return addCount;
    }

    private List<ScanCandidate> GetScanCandidates()
    {
        List<ScanCandidate> candidates = new List<ScanCandidate>();
        if (!ValidateTarget() || _target.transform == null)
        {
            return candidates;
        }

        HashSet<Transform> existing = new HashSet<Transform>();
        foreach (var node in _target.nodes)
        {
            if (node != null && node.transform != null)
            {
                existing.Add(node.transform);
            }
        }

        List<string> prefixes = SplitRuleText(prefixText);
        List<string> suffixes = SplitRuleText(suffixText);
        List<Transform> children = new List<Transform>();
        CollectChildren(_target.transform, children, 0);

        foreach (Transform child in children)
        {
            if (!includeInactive && !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (skipExisting && existing.Contains(child))
            {
                continue;
            }

            List<string> selectedTypes = PickTypes(child);
            if (skipNoUsefulComponent && selectedTypes.Count == 0)
            {
                continue;
            }

            bool matchName = MatchNameRule(child.name, prefixes, suffixes);
            bool matchComponent = scanByComponent && MatchSelectedComponent(child);
            bool hasNameRule = scanByPrefix || scanBySuffix;

            // 启用多个规则时，需要同时满足所有启用的规则（"与"关系）
            bool allRulesPassed = true;
            if (hasNameRule && !matchName)
                allRulesPassed = false;
            if (scanByComponent && !matchComponent)
                allRulesPassed = false;

            if (allRulesPassed)
            {
                candidates.Add(new ScanCandidate
                {
                    transform = child,
                    tag = CreateTagName(child.name),
                    types = selectedTypes
                });

                existing.Add(child);
            }
        }

        return candidates;
    }

    private void CollectChildren(Transform root, List<Transform> results, int depth)
    {
        if (maxDepth >= 0 && depth >= maxDepth)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            results.Add(child);
            CollectChildren(child, results, depth + 1);
        }
    }

    private bool MatchNameRule(string nodeName, List<string> prefixes, List<string> suffixes)
    {
        string lowerName = nodeName.ToLower();
        bool prefixEnabled = scanByPrefix && prefixes.Count > 0;
        bool suffixEnabled = scanBySuffix && suffixes.Count > 0;
        bool prefixMatched = !prefixEnabled;
        bool suffixMatched = !suffixEnabled;

        if (prefixEnabled)
        {
            prefixMatched = false;
            foreach (string prefix in prefixes)
            {
                if (lowerName.StartsWith(prefix))
                {
                    prefixMatched = true;
                    break;
                }
            }
        }

        if (suffixEnabled)
        {
            suffixMatched = false;
            foreach (string suffix in suffixes)
            {
                if (lowerName.EndsWith(suffix))
                {
                    suffixMatched = true;
                    break;
                }
            }
        }

        return prefixMatched && suffixMatched;
    }

    private List<string> PickTypes(Transform target)
    {
        List<string> result = new List<string>();

        string lowerName = target.name.ToLower();
        if (lowerName.StartsWith("btn") || HasComponent(target, "UnityEngine.UI.Button"))
        {
            AddIfHas(target, result, "UnityEngine.UI.Button");
            AddIfHas(target, result, "UnityEngine.UI.Image");
        }
        else if (lowerName.StartsWith("txt") || lowerName.StartsWith("text"))
        {
            AddIfHas(target, result, "UnityEngine.UI.Text");
            AddIfHas(target, result, "TMPro.TextMeshProUGUI");
            AddIfHas(target, result, "TMPro.TMP_Text");
        }
        else if (lowerName.StartsWith("img") || lowerName.StartsWith("icon"))
        {
            AddIfHas(target, result, "UnityEngine.UI.Image");
        }

        foreach (string typeName in selectedComponentTypes)
        {
            if (typeName == "UnityEngine.RectTransform")
            {
                continue;
            }

            AddIfHas(target, result, typeName);
        }

        bool shouldAddRect = result.Count > 0 ||
            lowerName.StartsWith("panel") ||
            lowerName.StartsWith("item");

        if (shouldAddRect)
        {
            AddIfHas(target, result, "UnityEngine.RectTransform");
        }

        return result;
    }

    private bool MatchSelectedComponent(Transform target)
    {
        if (selectedComponentTypes.Count == 0)
        {
            return false;
        }

        foreach (string typeName in selectedComponentTypes)
        {
            if (HasComponent(target, typeName))
            {
                return true;
            }
        }

        return false;
    }

    private void AddIfHas(Transform target, List<string> result, string typeName)
    {
        if (!result.Contains(typeName) && HasComponent(target, typeName))
        {
            result.Add(typeName);
        }
    }

    private bool HasComponent(Transform target, string typeName)
    {
        Component[] components = target.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().FullName == typeName)
            {
                return true;
            }
        }

        return false;
    }

    private List<string> SplitRuleText(string text)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        string[] values = text.Split(new[] { ',', '，', ';', '；', '|', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string value in values)
        {
            string rule = value.Trim().ToLower();
            if (!string.IsNullOrEmpty(rule) && !result.Contains(rule))
            {
                result.Add(rule);
            }
        }

        return result;
    }

    private List<string> GetChildComponentTypes()
    {
        if (_target == null) return new List<string>();

        List<Transform> children = new List<Transform>();
        CollectChildren(_target.transform, children, 0);

        List<string> types = new List<string>();
        foreach (Transform child in children)
        {
            if (!includeInactive && !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            Component[] components = child.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().FullName;
                if (typeName == "UnityEngine.CanvasRenderer" || typeName == "UnityEngine.Transform")
                {
                    continue;
                }

                if (!types.Contains(typeName))
                {
                    types.Add(typeName);
                }
            }
        }

        types.Sort();
        selectedComponentTypes.RemoveAll(typeName => !types.Contains(typeName));
        return types;
    }

    private string GetDisplayName(string typeName)
    {
        int index = typeName.LastIndexOf(".", System.StringComparison.Ordinal);
        return index >= 0 ? typeName.Substring(index + 1) : typeName;
    }

    private string CreateTagName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "node";
        }

        if (char.IsUpper(name[0]))
        {
            return name.Length > 1 ? char.ToLower(name[0]) + name.Substring(1) : char.ToLower(name[0]).ToString();
        }

        return name;
    }

    public string strIpt = "";

    /// <summary>
    /// 通过 tag 或 name 查找节点并高亮选中
    /// </summary>
    public void FindBack()
    {
        if (_target == null)
            return;

        try
        {
            EditorGUILayout.BeginHorizontal();
            strIpt = GUILayout.TextField(strIpt);

            if (GUILayout.Button("查找节点"))
            {
                string searchText = strIpt.Trim();
                if (string.IsNullOrEmpty(searchText))
                    return;

                bool isFound = false;

                // 先按 tag 匹配
                foreach (var item in _target.nodes)
                {
                    if (item == null || item.transform == null)
                        continue;

                    if (!string.IsNullOrEmpty(item.tag) && item.tag.ToLower() == searchText.ToLower())
                    {
                        isFound = true;
                        Selection.activeGameObject = item.transform.gameObject;
                        EditorGUIUtility.PingObject(item.transform.gameObject);
                        break;
                    }
                }

                // 没找到再按 name 匹配
                if (!isFound)
                {
                    foreach (var item in _target.nodes)
                    {
                        if (item == null || item.transform == null)
                            continue;

                        string objName = item.transform.gameObject.name;
                        if (!string.IsNullOrEmpty(objName) && objName.ToLower() == searchText.ToLower())
                        {
                            isFound = true;
                            Selection.activeGameObject = item.transform.gameObject;
                            EditorGUIUtility.PingObject(item.transform.gameObject);
                            break;
                        }
                    }
                }

                if (isFound)
                {
                    strIpt = "";
                }
                else
                {
                    Debug.LogWarning($"查找节点: 未找到匹配 \"{searchText}\" 的节点");
                }
            }

            EditorGUILayout.EndHorizontal();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"UIBaseNodeWindow.FindBack 异常: {e.Message}");
        }
    }
}
