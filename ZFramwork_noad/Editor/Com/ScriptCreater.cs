using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ScriptCreater
{
    private const string GameObjectTypeName = "UnityEngine.GameObject";

    private class ComponentBinding
    {
        public string typeName;
        public string fieldName;
    }

    private class NodeBinding
    {
        public UINodeInfo node;
        public string fieldName;
        public string className;
        public List<ComponentBinding> components = new List<ComponentBinding>();
    }

    private static List<NodeBinding> GetNodeBindings(UIBaseNode uiNode)
    {
        List<NodeBinding> bindings = new List<NodeBinding>();
        HashSet<string> usedNodeFieldNames = new HashSet<string>();
        HashSet<string> usedNodeClassNames = new HashSet<string>();

        if (uiNode == null)
        {
            return bindings;
        }

        foreach (UINodeInfo node in uiNode.nodes)
        {
            List<string> selectedTypes = GetSelectedTypeNames(node);
            if (selectedTypes.Count == 0)
            {
                continue;
            }

            if (node.transform == null)
            {
                ZLogUtil.LogError($"节点{node.tag}没有绑定Transform，已跳过");
                continue;
            }

            string nodeFieldName = SanitizeIdentifier(node.tag);
            if (!usedNodeFieldNames.Add(nodeFieldName))
            {
                ZLogUtil.LogError($"生成节点字段名重复：{nodeFieldName}，已跳过");
                continue;
            }

            string nodeClassName = CreateUniqueClassName(nodeFieldName, usedNodeClassNames);
            NodeBinding nodeBinding = new NodeBinding
            {
                node = node,
                fieldName = nodeFieldName,
                className = nodeClassName
            };

            HashSet<string> usedComponentFieldNames = new HashSet<string>();
            foreach (string typeName in selectedTypes)
            {
                if (!HasType(node.transform, typeName))
                {
                    ZLogUtil.LogError($"节点{node.tag}缺少组件{typeName}，已跳过");
                    continue;
                }

                nodeBinding.components.Add(new ComponentBinding
                {
                    typeName = typeName,
                    fieldName = CreateUniqueComponentFieldName(typeName, usedComponentFieldNames)
                });
            }

            if (nodeBinding.components.Count > 0)
            {
                bindings.Add(nodeBinding);
            }
        }

        return bindings;
    }

    private static List<string> GetSelectedTypeNames(UINodeInfo node)
    {
        List<string> result = new List<string>();
        if (node == null)
        {
            return result;
        }

        if (node.useMultiTypes)
        {
            if (node.types == null)
            {
                node.types = new List<string>();
            }

            foreach (string typeName in node.types)
            {
                AddUnique(result, typeName);
            }
        }
        else
        {
            AddUnique(result, node.type);
        }

        return result;
    }

    private static bool HasType(Transform transform, string typeName)
    {
        if (transform == null)
        {
            return false;
        }

        if (typeName == GameObjectTypeName)
        {
            return true;
        }

        Component[] components = transform.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().FullName == typeName)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendNodeFields(StringBuilder stringBuilder, UIBaseNode uiNode, string indent)
    {
        foreach (NodeBinding binding in GetNodeBindings(uiNode))
        {
            stringBuilder.Append($"{indent}private {binding.className} {binding.fieldName};\n");
        }
    }

    private static void AppendNodeAssignments(StringBuilder stringBuilder, UIBaseNode uiNode, string rootName, string rootTransformExpression, string indent)
    {
        foreach (NodeBinding binding in GetNodeBindings(uiNode))
        {
            string componentPath = GetComponentPath(binding.node.transform, rootName);
            if (componentPath == null)
            {
                continue;
            }

            string targetExpression = string.IsNullOrEmpty(componentPath)
                ? rootTransformExpression
                : $"{rootTransformExpression}.Find(\"{componentPath}\")";

            stringBuilder.Append($"{indent}this.{binding.fieldName} = new {binding.className}({targetExpression});\n");
        }
    }

    private static void AppendNodeClasses(StringBuilder stringBuilder, UIBaseNode uiNode, string indent)
    {
        List<NodeBinding> nodeBindings = GetNodeBindings(uiNode);
        if (nodeBindings.Count == 0)
        {
            return;
        }

        AppendUINodeBaseClass(stringBuilder, indent);

        foreach (NodeBinding binding in nodeBindings)
        {
            stringBuilder.Append("\n");
            stringBuilder.Append($"{indent}private class {binding.className} : UINode\n");
            stringBuilder.Append($"{indent}{{\n");

            foreach (ComponentBinding component in binding.components)
            {
                stringBuilder.Append($"{indent}    public {component.typeName} {component.fieldName};\n");
            }

            stringBuilder.Append("\n");
            stringBuilder.Append($"{indent}    public {binding.className}(UnityEngine.Transform root) : base(root)\n");
            stringBuilder.Append($"{indent}    {{\n");

            foreach (ComponentBinding component in binding.components)
            {
                if (component.typeName == GameObjectTypeName)
                {
                    stringBuilder.Append($"{indent}        {component.fieldName} = root.gameObject;\n");
                }
                else
                {
                    stringBuilder.Append($"{indent}        {component.fieldName} = root.GetComponent<{component.typeName}>();\n");
                }
            }

            stringBuilder.Append($"{indent}    }}\n");
            stringBuilder.Append($"{indent}}}\n");
        }
    }

    private static void AppendUINodeBaseClass(StringBuilder stringBuilder, string indent)
    {
        stringBuilder.Append("\n");
        stringBuilder.Append($"{indent}private class UINode\n");
        stringBuilder.Append($"{indent}{{\n");
        stringBuilder.Append($"{indent}    public UnityEngine.GameObject zobj;\n");
        stringBuilder.Append($"{indent}    public UnityEngine.Transform ztrans;\n");
        stringBuilder.Append("\n");
        stringBuilder.Append($"{indent}    public UINode(UnityEngine.Transform root)\n");
        stringBuilder.Append($"{indent}    {{\n");
        stringBuilder.Append($"{indent}        ztrans = root;\n");
        stringBuilder.Append($"{indent}        zobj = root.gameObject;\n");
        stringBuilder.Append($"{indent}    }}\n");
        stringBuilder.Append("\n");
        stringBuilder.Append($"{indent}    public void SetActive(bool value)\n");
        stringBuilder.Append($"{indent}    {{\n");
        stringBuilder.Append($"{indent}        zobj.SetActive(value);\n");
        stringBuilder.Append($"{indent}    }}\n");
        stringBuilder.Append($"{indent}}}\n");
    }

    private static string GetComponentPath(Transform transform, string rootName)
    {
        if (transform == null)
        {
            return null;
        }

        if (transform.name == rootName)
        {
            return string.Empty;
        }

        Transform curTrans = transform;
        string componentPath = "/" + curTrans.name;
        int count = 0;

        while (curTrans.parent != null && curTrans.parent.name != rootName)
        {
            componentPath = componentPath.Insert(0, "/" + curTrans.parent.name);
            count++;
            curTrans = curTrans.parent;

            if (count > 100)
            {
                ZLogUtil.LogError("层级超过100,有毛病吧");
                return null;
            }
        }

        if (curTrans.parent == null)
        {
            ZLogUtil.LogError($"节点{transform.name}不在{rootName}下面，已跳过");
            return null;
        }

        return componentPath.Remove(0, 1);
    }

    private static string CreateFieldName(UINodeInfo node, string typeName, int selectedTypeCount)
    {
        string baseName = SanitizeIdentifier(node.tag);
        bool useBaseName = selectedTypeCount == 1 || (!string.IsNullOrEmpty(node.type) && node.type == typeName);

        if (useBaseName)
        {
            return baseName;
        }

        string suffix = GetTypeSuffix(typeName);
        if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return baseName;
        }

        return SanitizeIdentifier(baseName + suffix);
    }

    private static string CreateUniqueClassName(string nodeFieldName, HashSet<string> usedClassNames)
    {
        string className = ToPascalCase(nodeFieldName) + "Node";
        if (string.IsNullOrEmpty(className) || className == "Node")
        {
            className = "UINode";
        }

        string uniqueName = className;
        int index = 2;
        while (!usedClassNames.Add(uniqueName))
        {
            uniqueName = className + index;
            index++;
        }

        return uniqueName;
    }

    private static string CreateUniqueComponentFieldName(string typeName, HashSet<string> usedFieldNames)
    {
        string fieldName = GetComponentFieldName(typeName);
        string uniqueName = fieldName;
        int index = 2;
        while (!usedFieldNames.Add(uniqueName))
        {
            uniqueName = fieldName + index;
            index++;
        }

        return uniqueName;
    }

    private static string GetComponentFieldName(string typeName)
    {
        string simpleName = GetSimpleTypeName(typeName);
        switch (simpleName)
        {
            case "GameObject":
                return "zobj";
            case "RectTransform":
                return "zrect";
            case "Button":
                return "zbtn";
            case "Image":
                return "zimg";
            case "Text":
            case "TMP_Text":
            case "TextMeshProUGUI":
                return "ztxt";
            case "Toggle":
                return "ztoggle";
            case "Slider":
                return "zslider";
            case "ScrollRect":
                return "zscroll";
            case "InputField":
            case "TMP_InputField":
                return "zinput";
            case "Dropdown":
            case "TMP_Dropdown":
                return "zdropdown";
            case "Animator":
                return "zanimator";
            default:
                return "z" + ToPascalCase(simpleName);
        }
    }

    private static string GetTypeSuffix(string typeName)
    {
        string simpleName = GetSimpleTypeName(typeName);
        switch (simpleName)
        {
            case "GameObject":
                return "Obj";
            case "RectTransform":
                return "Rect";
            case "Button":
                return "Btn";
            case "Image":
                return "Img";
            case "Text":
            case "TMP_Text":
            case "TextMeshProUGUI":
                return "Txt";
            case "InputField":
            case "TMP_InputField":
                return "Input";
            case "ScrollRect":
                return "Scroll";
            default:
                return simpleName;
        }
    }

    private static string GetSimpleTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return "Node";
        }

        int dotIndex = typeName.LastIndexOf(".", StringComparison.Ordinal);
        int plusIndex = typeName.LastIndexOf("+", StringComparison.Ordinal);
        int index = Mathf.Max(dotIndex, plusIndex);
        return index >= 0 ? typeName.Substring(index + 1) : typeName;
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            value = "node";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool valid = char.IsLetterOrDigit(c) || c == '_';
            if (!valid)
            {
                c = '_';
            }

            if (i == 0 && !char.IsLetter(c) && c != '_')
            {
                builder.Append('_');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string ToPascalCase(string value)
    {
        value = SanitizeIdentifier(value);
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string[] parts = value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder builder = new StringBuilder();
        foreach (string part in parts)
        {
            if (part.Length == 1)
            {
                builder.Append(char.ToUpper(part[0]));
            }
            else if (part.Length > 1)
            {
                builder.Append(char.ToUpper(part[0]));
                builder.Append(part.Substring(1));
            }
        }

        if (builder.Length == 0)
        {
            builder.Append(value);
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static string ToCamelCase(string value)
    {
        value = ToPascalCase(value);
        if (string.IsNullOrEmpty(value))
        {
            return "component";
        }

        if (value.Length > 1 && char.IsUpper(value[0]) && char.IsUpper(value[1]))
        {
            int upperCount = 1;
            while (upperCount < value.Length && char.IsUpper(value[upperCount]))
            {
                upperCount++;
            }

            int prefixLength = upperCount == value.Length ? upperCount : upperCount - 1;
            string prefix = value.Substring(0, prefixLength).ToLower();
            return prefix + value.Substring(prefixLength);
        }

        return char.ToLower(value[0]) + value.Substring(1);
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (string.IsNullOrEmpty(value) || values.Contains(value))
        {
            return;
        }

        values.Add(value);
    }

    public static string  CreatePanelClassName(string path,string name, UINodePanel uIPanel)
    {
        StringBuilder stringBuilder = new StringBuilder();
        string adressPath = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI/", "");
        string panelName = adressPath.Split('/')[0];
        string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI","Assets/Game/Scripts/UI")+"/";
        string voDir = dir.Replace("Assets/Game/Scripts/UI", "Assets/Game/Scripts/UIVO");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        if (!Directory.Exists(voDir))
        {
            Directory.CreateDirectory(voDir);
        }
        voDir = voDir + name + "VO.cs";
        dir = dir + name + ".cs";
        stringBuilder.Append("using System;\n");
        stringBuilder.Append("using System.Collections;\n");
        stringBuilder.Append("using System.Collections.Generic;\n");
        stringBuilder.Append("using UnityEngine;\n");
        stringBuilder.Append("using UnityEngine.UI;\n");
        stringBuilder.Append(string.Format("public partial class {0} : BasePanel\n", name));
        stringBuilder.Append("{\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    public override void Init(params object[] args)\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("        base.Init(args);\n");
        stringBuilder.Append($"        panelLayer = PanelLayer.{panelName};\n");
        stringBuilder.Append($"        adressPath = \"{adressPath}\";\n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("    public override void OnShowing()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    public override void OnOpen()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("        RefreshPanel();\n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    public override void OnHide()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("     \n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    public override void OnClosing()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("     \n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    private void RefreshPanel()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("}");


        if (!File.Exists(dir))
        {
            File.WriteAllText(dir, stringBuilder.ToString());
            ZLogUtil.Log("创建"+name+"成功");
        }

        //VO
        stringBuilder.Clear();
        stringBuilder.Append(string.Format("public partial class {0} : BasePanel\n", name));
        stringBuilder.Append("{\n");
        AppendNodeFields(stringBuilder, uIPanel, "   ");
        stringBuilder.Append("\n");
        stringBuilder.Append("   public override void AutoInit()\n");
        stringBuilder.Append("   {\n");
        stringBuilder.Append("        ServiceBinder.Instance.RegisterObj(this);\n");
        AppendNodeAssignments(stringBuilder, uIPanel, name, "panel.transform", "    ");
        stringBuilder.Append("   }\n");
        AppendNodeClasses(stringBuilder, uIPanel, "   ");
        stringBuilder.Append("}\n");

 


        File.WriteAllText(voDir, stringBuilder.ToString());
        stringBuilder.Clear();
        AssetDatabase.Refresh();
        return dir;
    }


    public static void CreateScrollerClassName(string path,string panelName,string scrollName,UINodeScoller uINodeScoller)
    {
        StringBuilder stringBuilder = new StringBuilder();
        string cellName = uINodeScoller.cellView.name;
        if (uINodeScoller.cellView.GetComponent(uINodeScoller.cellView.name) == null)
        {
            ZLogUtil.LogError($"请先完成预制体{uINodeScoller.cellView.name}");
            return;
        }
        string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UI")+"/";
        string voDir = dir.Replace("Assets/Game/Scripts/UI", "Assets/Game/Scripts/UIVO");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        if (!Directory.Exists(voDir))
        {
            Directory.CreateDirectory(voDir);
        }
        string scrollerDir = dir + "/Scoller/";
        string voscrollerDir = voDir + "/ScollerVO/";
        if (!Directory.Exists(scrollerDir))
        {
            Directory.CreateDirectory(scrollerDir);
        }
        if (!Directory.Exists(voscrollerDir))
        {
            Directory.CreateDirectory(voscrollerDir);
        }
        string scrollerScript = scrollerDir + scrollName + ".cs";
        string voScrollerScript = voscrollerDir + scrollName + "VO.cs";

        stringBuilder.Append("using EnhancedUI.EnhancedScroller;\n");
        stringBuilder.Append("using UnityEngine;\n");
        stringBuilder.Append("using System;\n\n");
        stringBuilder.Append("[RequireComponent(typeof(EnhancedScroller))]\n");
        stringBuilder.Append($"public partial class {scrollName} : MonoBehaviour, IEnhancedScrollerDelegate\n");
        stringBuilder.Append("{\n");
        stringBuilder.Append("    public void Init()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("     \n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("    public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append($"       EnhancedScrollerCellView cellView = scroller.GetCellView(cellViewPrefab);\n");
        stringBuilder.Append("        cellView.dataIndex = dataIndex;\n");
        stringBuilder.Append("        cellView.cellIndex = cellIndex;\n");
        stringBuilder.Append("        cellView.scroller = scroller;");
        stringBuilder.Append("        cellView.InitData(this);");
        stringBuilder.Append("        cellView.RefreshCellView();\n");
        stringBuilder.Append("        return cellView;\n");
        stringBuilder.Append("    }\n\n");
        stringBuilder.Append("    public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("        return cellRectTransform.rect.height;\n");
        stringBuilder.Append("    }\n\n");
        stringBuilder.Append("    public int GetNumberOfCells(EnhancedScroller scroller)\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("        return 6;\n");
        stringBuilder.Append("    }\n\n");
        stringBuilder.Append("}\n");

        if (!File.Exists(scrollerScript))
        {
            File.WriteAllText(scrollerScript, stringBuilder.ToString());
            stringBuilder.Clear();
            ZLogUtil.Log("创建" + scrollerScript + "成功");
        }

        ///VO
        stringBuilder.Clear();
        stringBuilder.Append("using EnhancedUI.EnhancedScroller;\n");
        stringBuilder.Append("using UnityEngine;\n");
        stringBuilder.Append("using System;\n\n");
        stringBuilder.Append("[RequireComponent(typeof(EnhancedScroller))]\n");
        stringBuilder.Append($"public partial class {scrollName} : MonoBehaviour, IEnhancedScrollerDelegate\n");
        stringBuilder.Append("{\n");
        stringBuilder.Append("    [NonSerialized]\n");
        stringBuilder.Append("    public EnhancedScroller scroller;\n");
        stringBuilder.Append("    [NonSerialized]\n");
        stringBuilder.Append("    public RectTransform cellRectTransform;\n");
        stringBuilder.Append("    [NonSerialized]\n");
        stringBuilder.Append("    public RectTransform rectTransform;\n");
        stringBuilder.Append("    public EnhancedScrollerCellView cellViewPrefab;\n\n");
        AppendNodeFields(stringBuilder, uINodeScoller, "    ");
        stringBuilder.Append("    void Start()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("        ServiceBinder.Instance.RegisterObj(this);\n");
        stringBuilder.Append("        scroller = GetComponent<EnhancedScroller>();\n");
        stringBuilder.Append("        rectTransform = GetComponent<RectTransform>();\n");
        stringBuilder.Append("        cellRectTransform = cellViewPrefab.GetComponent<RectTransform>();\n");
        stringBuilder.Append("        scroller.Delegate = this;\n");
        AppendNodeAssignments(stringBuilder, uINodeScoller, scrollName, "transform", "        ");
        stringBuilder.Append("        Init();\n");
        stringBuilder.Append("     }\n");
        AppendNodeClasses(stringBuilder, uINodeScoller, "    ");
        stringBuilder.Append("}\n");

        File.WriteAllText(voScrollerScript, stringBuilder.ToString());
        stringBuilder.Clear();
        AssetDatabase.Refresh();
    }

    public static void CreateScrollerCellClassName(string path, string panelName, string cellName, UINodeScollerCell uINodeScoller)
    {
        StringBuilder stringBuilder = new StringBuilder();
        string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UI")+"/";
        string voDir = dir.Replace("Assets/Game/Scripts/UI", "Assets/Game/Scripts/UIVO");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
 


        string scrollerDir = dir + "/Scoller/";
        string voscrollerDir = voDir + "/ScollerVO/";
        if (!Directory.Exists(scrollerDir))
        {
            Directory.CreateDirectory(scrollerDir);
        }
        string scrollerCell = scrollerDir + "Cell/";
        string voscrollerCellDir = voscrollerDir + "CellVO/";
        if (!Directory.Exists(scrollerCell))
        {
            Directory.CreateDirectory(scrollerCell);
        }
        if (!Directory.Exists(voscrollerCellDir))
        {
            Directory.CreateDirectory(voscrollerCellDir);
        }
        string scrollerCellScript = scrollerCell + cellName + ".cs";
        string scrollerCellVOScript = voscrollerCellDir + cellName + "VO.cs";
        stringBuilder.Append("using EnhancedUI.EnhancedScroller;\n");
        stringBuilder.Append("using UnityEngine.UI;\n\n");
        stringBuilder.Append($"public partial class {cellName} : EnhancedScrollerCellView\n");
        stringBuilder.Append("{\n");
        stringBuilder.Append("    public override void RefreshCellView()\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("        AutoInit();\n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("}");


        if (!File.Exists(scrollerCellScript))
        {
            File.WriteAllText(scrollerCellScript, stringBuilder.ToString());
            stringBuilder.Clear();
            ZLogUtil.Log("创建" + scrollerCellScript + "成功");
        }

        //VO

        stringBuilder.Clear();
        stringBuilder.Append("using EnhancedUI.EnhancedScroller;\n\n");
        stringBuilder.Append(string.Format("public partial class {0} : EnhancedScrollerCellView\n", cellName));
        stringBuilder.Append("{\n");
        stringBuilder.Append("   private bool onceInit = true;\n");
        AppendNodeFields(stringBuilder, uINodeScoller, "   ");
        stringBuilder.Append("\n");
        stringBuilder.Append("   public override void AutoInit()\n");
        stringBuilder.Append("   {\n");
        stringBuilder.Append("       ServiceBinder.Instance.RegisterObj(this);\n");
        AppendNodeAssignments(stringBuilder, uINodeScoller, cellName, "transform", "        ");
        stringBuilder.Append("   }\n");
        AppendNodeClasses(stringBuilder, uINodeScoller, "   ");
        stringBuilder.Append("}\n");

        File.WriteAllText(scrollerCellVOScript, stringBuilder.ToString());
        stringBuilder.Clear();
        AssetDatabase.Refresh();

        }

    public static string CreateItemClassName(string path, string name, UINodeItem uIPanel)
    {
        StringBuilder stringBuilder = new StringBuilder();
        string adressPath = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI/", "");
        string panelName = adressPath.Split('/')[0];
        string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UI") + "/others/";
        string voDir = dir.Replace("Assets/Game/Scripts/UI", "Assets/Game/Scripts/UIVO");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        if (!Directory.Exists(voDir))
        {
            Directory.CreateDirectory(voDir);
        }
        voDir = voDir + name + "VO.cs";
        dir = dir + name + ".cs";
        stringBuilder.Append("using System;\n");
        stringBuilder.Append("using System.Collections;\n");
        stringBuilder.Append("using System.Collections.Generic;\n");
        stringBuilder.Append("using UnityEngine;\n");
        stringBuilder.Append("using UnityEngine.UI;\n");
        stringBuilder.Append(string.Format("public partial class {0} : BaseItemMono\n", name));
        stringBuilder.Append("{\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    public override void show(params object[] args)\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("}");


        if (!File.Exists(dir))
        {
            File.WriteAllText(dir, stringBuilder.ToString());
            ZLogUtil.Log("创建" + name + "成功");
        }

        //VO
        stringBuilder.Clear();
        stringBuilder.Append(string.Format("public partial class {0} : BaseItemMono\n", name));
        stringBuilder.Append("{\n");
        AppendNodeFields(stringBuilder, uIPanel, "   ");
        stringBuilder.Append("\n");
        stringBuilder.Append("   public override void InitNode()\n");
        stringBuilder.Append("   {\n");
        stringBuilder.Append("        ServiceBinder.Instance.RegisterObj(this);\n");
        AppendNodeAssignments(stringBuilder, uIPanel, name, "transform", "    ");
        stringBuilder.Append("   }\n");
        AppendNodeClasses(stringBuilder, uIPanel, "   ");
        stringBuilder.Append("}\n");




        File.WriteAllText(voDir, stringBuilder.ToString());
        stringBuilder.Clear();
        AssetDatabase.Refresh();
        return dir;
    }
    public static string CreateItem_InitClassName(string path, string name, UINodeItem_Init uIPanel)
    {
        StringBuilder stringBuilder = new StringBuilder();
        string adressPath = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI/", "");
        string panelName = adressPath.Split('/')[0];
        string dir = path.Replace(".prefab", "").Replace("Assets/Game/AssetDynamic/Prefab/UI", "Assets/Game/Scripts/UI") + "/others/";
        string voDir = dir.Replace("Assets/Game/Scripts/UI", "Assets/Game/Scripts/UIVO");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        if (!Directory.Exists(voDir))
        {
            Directory.CreateDirectory(voDir);
        }
        voDir = voDir + name + "VO.cs";
        dir = dir + name + ".cs";
        stringBuilder.Append("using System;\n");
        stringBuilder.Append("using System.Collections;\n");
        stringBuilder.Append("using System.Collections.Generic;\n");
        stringBuilder.Append("using UnityEngine;\n");
        stringBuilder.Append("using UnityEngine.UI;\n");
        stringBuilder.Append(string.Format("public partial class {0} : BaseItemMono\n", name));
        stringBuilder.Append("{\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("    public override void show(params object[] args)\n");
        stringBuilder.Append("    {\n");
        stringBuilder.Append("    }\n");
        stringBuilder.Append("\n");
        stringBuilder.Append("}");


        if (!File.Exists(dir))
        {
            File.WriteAllText(dir, stringBuilder.ToString());
            ZLogUtil.Log("创建" + name + "成功");
        }

        //VO
        stringBuilder.Clear();
        stringBuilder.Append(string.Format("public partial class {0} : BaseItemMono\n", name));
        stringBuilder.Append("{\n");
        AppendNodeFields(stringBuilder, uIPanel, "   ");
        stringBuilder.Append("\n");
        stringBuilder.Append("   public override void customInitNode()\n");
        stringBuilder.Append("   {\n");
        stringBuilder.Append("        ServiceBinder.Instance.RegisterObj(this);\n");
        AppendNodeAssignments(stringBuilder, uIPanel, name, "transform", "    ");
        stringBuilder.Append("   }\n");
        AppendNodeClasses(stringBuilder, uIPanel, "   ");
        stringBuilder.Append("}\n");




        File.WriteAllText(voDir, stringBuilder.ToString());
        stringBuilder.Clear();
        AssetDatabase.Refresh();
        return dir;
    }


    #region uiscript
        public static void CreateServiceBinder()
        {
                string dir = "Assets/Game/Scripts/Core/";
                string name = "ServiceBinder";

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                StringBuilder stringBuilder = new StringBuilder();

                dir = dir + name + ".cs";
                stringBuilder.Append("public class ServiceBinder : BaseServiceBinder\n");
                stringBuilder.Append("{\n");
                stringBuilder.Append("     protected  ServiceBinder()\n");
                stringBuilder.Append("     {\n");
                stringBuilder.Append("     }\n");
                stringBuilder.Append("     public override void Binder()\n");
                stringBuilder.Append("     {\n");
                stringBuilder.Append("        container.RegisterInstance<ITestMgr>(new TestMgr());\n");
                stringBuilder.Append("     }\n");
                stringBuilder.Append("}\n");
                stringBuilder.Append("//使用方法 在panel 中  [Inject] public ITestMgr testMgr; 注册后直接使用");

                if (!File.Exists(dir))
                {
                    File.WriteAllText(dir, stringBuilder.ToString());
                    ZLogUtil.Log("创建"+name+"成功");
                }

                File.WriteAllText(dir, stringBuilder.ToString());
                stringBuilder.Clear();
                AssetDatabase.Refresh();
         }
            public static void CreateTestData()
        {
                string dir = "Assets/Game/Scripts/Model/Test/Data/";
                string name = "TestData";

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                StringBuilder stringBuilder = new StringBuilder();

                dir = dir + name + ".cs";
                stringBuilder.Append("public class TestData\n");
                stringBuilder.Append("{\n");
                stringBuilder.Append("}\n");

                if (!File.Exists(dir))
                {
                    File.WriteAllText(dir, stringBuilder.ToString());
                    ZLogUtil.Log("创建"+name+"成功");
                }

                File.WriteAllText(dir, stringBuilder.ToString());
                stringBuilder.Clear();
                AssetDatabase.Refresh();
         }
         public static void CreateEventTest()
        {
                string dir = "Assets/Game/Scripts/Model/Test/Event/";
                string name = "EventTest";

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                StringBuilder stringBuilder = new StringBuilder();

                dir = dir + name + ".cs";
                stringBuilder.Append("public class Event_Test_SelectTestMonster : Event<Event_Test_SelectTestMonster> {\n");
                stringBuilder.Append("    public override void Clear() { }\n");
                stringBuilder.Append("}\n");

                if (!File.Exists(dir))
                {
                    File.WriteAllText(dir, stringBuilder.ToString());
                    ZLogUtil.Log("创建"+name+"成功");
                }

                File.WriteAllText(dir, stringBuilder.ToString());
                stringBuilder.Clear();
                AssetDatabase.Refresh();
         }
         public static void CreateITestMgr()
        {
                string dir = "Assets/Game/Scripts/Model/Test/Mgr/";
                string name = "ITestMgr";

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                StringBuilder stringBuilder = new StringBuilder();

                dir = dir + name + ".cs";
                stringBuilder.Append("public interface ITestMgr\n");
                stringBuilder.Append("{\n");
                stringBuilder.Append("    public string GetTestStr();\n");
                stringBuilder.Append("}\n");

                if (!File.Exists(dir))
                {
                    File.WriteAllText(dir, stringBuilder.ToString());
                    ZLogUtil.Log("创建"+name+"成功");
                }

                File.WriteAllText(dir, stringBuilder.ToString());
                stringBuilder.Clear();
                AssetDatabase.Refresh();
         }
        public static void CreateTestMgr()
        {
                string dir = "Assets/Game/Scripts/Model/Test/Mgr/";
                string name = "TestMgr";

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                StringBuilder stringBuilder = new StringBuilder();

                dir = dir + name + ".cs";
                stringBuilder.Append("public class TestMgr : ITestMgr\n");
                stringBuilder.Append("{\n");
                stringBuilder.Append("   private string testStr = \"test text\";\n");
                stringBuilder.Append("    public string GetTestStr()\n");
                stringBuilder.Append("    {\n");
                stringBuilder.Append("        return testStr;\n");
                stringBuilder.Append("    }\n");
                stringBuilder.Append("}\n");

                if (!File.Exists(dir))
                {
                    File.WriteAllText(dir, stringBuilder.ToString());
                    ZLogUtil.Log("创建"+name+"成功");
                }

                File.WriteAllText(dir, stringBuilder.ToString());
                stringBuilder.Clear();
                AssetDatabase.Refresh();
         }
    #endregion
}
