using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UINodeInfoEditorDrawer
{
    private const float LineHeight = 18f;
    private const float LineStep = 22f;
    private const float Padding = 5f;
    private const string GameObjectTypeName = "UnityEngine.GameObject";
    private static readonly HashSet<string> HiddenTypeNames = new HashSet<string>
    {
        "UnityEngine.CanvasRenderer",
        "UnityEngine.Transform"
    };

    private static readonly HashSet<string> CommonTypeNames = new HashSet<string>
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

    public static float GetElementHeight(UINodeInfo node, UIBaseNode owner = null)
    {
        if (node == null || !node.foldout)
        {
            return 57f;
        }

        if (node.transform == null)
        {
            return 127f;
        }

        int typeCount = GetAvailableTypeNames(node.transform).Count;
        int missingCount = GetMissingSelectedTypes(node).Count;
        int validationCount = GetValidationMessages(node, owner).Count;
        return 132f + typeCount * LineStep + missingCount * LineStep + validationCount * LineStep;
    }

    public static void DrawElement(Rect rect, UINodeInfo node, UnityEngine.Object dirtyTarget, UIBaseNode owner = null)
    {
        float height = GetElementHeight(node, owner) - 10f;
        Rect boxRect = new Rect(rect.x + Padding, rect.y + Padding, rect.width - Padding, height);
        GUI.Box(boxRect, "", EditorStyles.helpBox);
        if (HasMissingTransform(node))
        {
            DrawWarningBackground(boxRect);
        }

        Rect line = new Rect(boxRect.x + Padding, boxRect.y + Padding, boxRect.width - Padding * 2f, LineHeight);
        DrawSummary(line, node, dirtyTarget);
        if (!node.foldout)
        {
            return;
        }

        line.y += LineStep * 2f;

        string tag = EditorGUI.TextField(line, "Node Tag", node.tag).Trim();
        if (tag != node.tag)
        {
            node.tag = tag;
            SetDirty(dirtyTarget);
        }

        line.y += LineStep;
        Transform transform = (Transform)EditorGUI.ObjectField(line, "Transform", node.transform, typeof(Transform), true);
        if (transform != node.transform)
        {
            node.transform = transform;
            TryFillTagFromTransform(node);
            SelectDefaultType(node);
            SetDirty(dirtyTarget);
        }

        line.y += LineStep;
        if (node.transform == null)
        {
            EditorGUI.HelpBox(line, "Missing Transform. Drag the UI node again.", MessageType.Error);
            return;
        }

        DrawToolbar(line, node, dirtyTarget);
        line.y += LineStep;

        List<string> availableTypes = GetAvailableTypeNames(node.transform);
        foreach (string typeName in availableTypes)
        {
            bool selected = IsSelected(node, typeName);
            bool newSelected = EditorGUI.ToggleLeft(line, new GUIContent(GetDisplayName(typeName), typeName), selected);
            if (newSelected != selected)
            {
                SetSelected(node, typeName, newSelected);
                SetDirty(dirtyTarget);
            }

            line.y += LineStep;
        }

        foreach (string missingType in GetMissingSelectedTypes(node))
        {
            EditorGUI.HelpBox(line, "Missing: " + missingType, MessageType.Warning);
            line.y += LineStep;
        }

        foreach (string message in GetValidationMessages(node, owner))
        {
            EditorGUI.HelpBox(line, message, MessageType.Warning);
            line.y += LineStep;
        }
    }

    private static void DrawSummary(Rect line, UINodeInfo node, UnityEngine.Object dirtyTarget)
    {
        float foldoutWidth = 44f;
        Rect foldoutRect = new Rect(line.x, line.y, foldoutWidth, LineHeight);
        Rect tagRect = new Rect(foldoutRect.xMax + 6f, line.y, Mathf.Min(190f, line.width * 0.32f), LineHeight);
        string tag = EditorGUI.TextField(tagRect, node.tag);
        if (tag != node.tag)
        {
            node.tag = tag.Trim();
            SetDirty(dirtyTarget);
        }

        Rect objectRect = new Rect(tagRect.xMax + 6f, line.y, line.xMax - tagRect.xMax - 6f, LineHeight);
        Transform transform = (Transform)EditorGUI.ObjectField(objectRect, node.transform, typeof(Transform), true);
        if (transform != node.transform)
        {
            node.transform = transform;
            TryFillTagFromTransform(node);
            SelectDefaultType(node);
            node.foldout = true;
            SetDirty(dirtyTarget);
        }

        string foldoutText = node.foldout ? "▲" : "▼";
        if (GUI.Button(foldoutRect, foldoutText, EditorStyles.miniButton))
        {
            node.foldout = !node.foldout;
            SetDirty(dirtyTarget);
        }

        Rect summaryRect = new Rect(line.x, line.y + LineStep, line.width, LineHeight);
        string summary = GetSelectedSummary(node);
        List<string> messages = GetValidationMessages(node, dirtyTarget as UIBaseNode);
        GUIStyle summaryStyle = EditorStyles.miniLabel;
        if (HasMissingTransform(node))
        {
            summary = "[节点丢失] " + summary;
            summaryStyle = GetRedMiniLabelStyle();
        }
        else if (messages.Count > 0)
        {
            summary = "[命名冲突] " + summary;
            summaryStyle = GetRedMiniLabelStyle();
        }

        EditorGUI.LabelField(summaryRect, summary, summaryStyle);
    }

    private static bool HasMissingTransform(UINodeInfo node)
    {
        return node == null || node.transform == null;
    }

    private static void DrawWarningBackground(Rect rect)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 0.25f, 0.25f, 0.16f);
        GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
        GUI.color = oldColor;
    }

    private static GUIStyle GetRedMiniLabelStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(1f, 0.35f, 0.35f);
        style.fontStyle = FontStyle.Bold;
        return style;
    }

    private static void DrawToolbar(Rect line, UINodeInfo node, UnityEngine.Object dirtyTarget)
    {
        EditorGUI.LabelField(line, "Components");

        float gap = 6f;
        float maxButtonWidth = 72f;
        float buttonWidth = Mathf.Min(maxButtonWidth, (line.width - gap * 2f) / 3f);
        Rect clearRect = new Rect(line.xMax - buttonWidth, line.y, buttonWidth, LineHeight);
        Rect allRect = new Rect(clearRect.x - buttonWidth - gap, line.y, buttonWidth, LineHeight);
        Rect commonRect = new Rect(allRect.x - buttonWidth - gap, line.y, buttonWidth, LineHeight);

        if (GUI.Button(commonRect, "Common", EditorStyles.miniButton))
        {
            List<string> commonTypes = new List<string>();
            foreach (string typeName in GetAvailableTypeNames(node.transform))
            {
                if (CommonTypeNames.Contains(typeName))
                {
                    AddUnique(commonTypes, typeName);
                }
            }

            node.useMultiTypes = true;
            node.types = commonTypes;
            if (!string.IsNullOrEmpty(node.type) && !commonTypes.Contains(node.type))
            {
                node.type = string.Empty;
            }
            SetDirty(dirtyTarget);
        }

        if (GUI.Button(allRect, "All", EditorStyles.miniButton))
        {
            List<string> allTypes = GetAvailableTypeNames(node.transform);
            node.useMultiTypes = true;
            node.types = allTypes;
            if (!string.IsNullOrEmpty(node.type) && !allTypes.Contains(node.type))
            {
                node.type = string.Empty;
            }
            SetDirty(dirtyTarget);
        }

        if (GUI.Button(clearRect, "Clear", EditorStyles.miniButton))
        {
            node.useMultiTypes = true;
            if (node.types == null)
            {
                node.types = new List<string>();
            }
            node.types.Clear();
            node.type = string.Empty;
            SetDirty(dirtyTarget);
        }
    }

    private static void TryFillTagFromTransform(UINodeInfo node)
    {
        string tagName = node.tag;
        if (!string.IsNullOrEmpty(tagName) && !tagName.Equals("Node Name"))
        {
            return;
        }

        if (node.transform == null)
        {
            return;
        }

        tagName = node.transform.gameObject.name;
        if (!string.IsNullOrEmpty(tagName) && char.IsUpper(tagName[0]))
        {
            tagName = tagName.Length > 1
                ? char.ToLower(tagName[0]) + tagName.Substring(1)
                : char.ToLower(tagName[0]).ToString();
        }

        node.tag = tagName;
    }

    private static void SelectDefaultType(UINodeInfo node)
    {
        if (node.transform == null || !string.IsNullOrEmpty(node.type) || (node.types != null && node.types.Count > 0))
        {
            return;
        }

        List<string> availableTypes = GetAvailableTypeNames(node.transform);
        string typeName = PickDefaultType(availableTypes);
        if (string.IsNullOrEmpty(typeName))
        {
            return;
        }

        node.useMultiTypes = true;
        node.types = new List<string> { typeName };
        node.type = typeName;
    }

    private static string PickDefaultType(List<string> availableTypes)
    {
        string[] priorities =
        {
            "UnityEngine.UI.Button",
            "UnityEngine.UI.Text",
            "TMPro.TextMeshProUGUI",
            "TMPro.TMP_Text",
            "UnityEngine.UI.Image",
            "UnityEngine.RectTransform"
        };

        foreach (string typeName in priorities)
        {
            if (availableTypes.Contains(typeName))
            {
                return typeName;
            }
        }

        return availableTypes.Count > 0 ? availableTypes[0] : string.Empty;
    }

    private static bool IsSelected(UINodeInfo node, string typeName)
    {
        if (node.useMultiTypes)
        {
            return node.types != null && node.types.Contains(typeName);
        }

        return node.type == typeName;
    }

    private static void SetSelected(UINodeInfo node, string typeName, bool selected)
    {
        List<string> selectedTypes = GetSelectedTypeNames(node);

        if (selected)
        {
            if (!selectedTypes.Contains(typeName))
            {
                selectedTypes.Add(typeName);
            }
        }
        else
        {
            selectedTypes.Remove(typeName);
        }

        node.useMultiTypes = true;
        node.types = selectedTypes;

        if (selectedTypes.Count == 0)
        {
            node.type = string.Empty;
        }
        else if (string.IsNullOrEmpty(node.type) || !selectedTypes.Contains(node.type))
        {
            node.type = selectedTypes[0];
        }
    }

    private static List<string> GetSelectedTypeNames(UINodeInfo node)
    {
        List<string> result = new List<string>();

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

    private static List<string> GetMissingSelectedTypes(UINodeInfo node)
    {
        List<string> missing = new List<string>();
        if (node == null || node.transform == null)
        {
            return missing;
        }

        List<string> available = GetAvailableTypeNames(node.transform);
        foreach (string typeName in GetSelectedTypeNames(node))
        {
            if (typeName == GameObjectTypeName)
            {
                continue;
            }

            if (!available.Contains(typeName))
            {
                AddUnique(missing, typeName);
            }
        }

        return missing;
    }

    private static List<string> GetValidationMessages(UINodeInfo node, UIBaseNode owner)
    {
        List<string> messages = new List<string>();
        if (node == null || owner == null)
        {
            return messages;
        }

        string fieldName = SanitizeIdentifier(node.tag);
        if (string.IsNullOrEmpty(fieldName))
        {
            messages.Add("Node Tag 为空，无法生成变量名");
            return messages;
        }

        int sameFieldNameCount = 0;
        foreach (UINodeInfo item in owner.nodes)
        {
            if (item == null)
            {
                continue;
            }

            if (SanitizeIdentifier(item.tag) == fieldName)
            {
                sameFieldNameCount++;
            }
        }

        if (sameFieldNameCount > 1)
        {
            messages.Add("Node Tag 生成变量名重复：" + fieldName);
        }

        return messages;
    }

    private static List<string> GetAvailableTypeNames(Transform transform)
    {
        List<string> names = new List<string>();
        if (transform == null)
        {
            return names;
        }

        Component[] components = transform.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().FullName;
            if (!HiddenTypeNames.Contains(typeName))
            {
                AddUnique(names, typeName);
            }
        }

        return names;
    }

    private static string GetSelectedSummary(UINodeInfo node)
    {
        List<string> selectedTypes = GetSelectedTypeNames(node);
        if (selectedTypes.Count == 0)
        {
            return "No components";
        }

        List<string> names = new List<string>();
        foreach (string typeName in selectedTypes)
        {
            names.Add(GetDisplayName(typeName));
        }

        return string.Join(", ", names.ToArray());
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (string.IsNullOrEmpty(value) || values.Contains(value))
        {
            return;
        }

        values.Add(value);
    }

    private static string GetDisplayName(string typeName)
    {
        if (typeName == GameObjectTypeName)
        {
            return "GameObject";
        }

        int index = typeName.LastIndexOf(".", StringComparison.Ordinal);
        return index >= 0 ? typeName.Substring(index + 1) : typeName;
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
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

    private static void SetDirty(UnityEngine.Object dirtyTarget)
    {
        if (dirtyTarget != null)
        {
            EditorUtility.SetDirty(dirtyTarget);
        }
    }
}
