using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class GpuRoleStyleViewer
{
    private void DrawGroupManagement()
    {
        if (_core.StyleSlots.Count == 0) return;

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Linked Group Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create New Group", GUILayout.Width(160)))
        {
            int id = _core.CreateGroup();
            AutoSave();
            _messages.Add($"Created group (ID: {id}).");
        }

        if (GUILayout.Button("Clear All Groups", GUILayout.Width(160)))
        {
            foreach (var slot in _core.StyleSlots)
            {
                slot.linkedGroupId = -1;
                slot.linkedSubSpriteName = slot.slotName;
            }
            var groups = _core.Groups.ToList();
            foreach (var g in groups) _core.RemoveGroup(g.groupId);
            AutoSave();
            _messages.Add("Cleared all groups.");
        }

        EditorGUILayout.EndHorizontal();

        // 组列表
        EditorGUI.indentLevel++;
        foreach (var g in _core.Groups)
        {
            var names = _core.GetSlotNamesInGroup(g.groupId);
            string list = names.Count > 0 ? string.Join(", ", names) : "(empty)";
            EditorGUILayout.LabelField($"{g.groupName} (ID {g.groupId}): {list}", EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;

        GUILayout.Space(4);

        // ===== 互斥组管理 =====
        EditorGUILayout.LabelField("Exclusive Group Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create New Exclusive Group", GUILayout.Width(200)))
        {
            int id = _core.CreateExclusiveGroup();
            AutoSave();
            _messages.Add($"Created exclusive group (ID: {id}).");
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        // 互斥组列表
        foreach (var eg in _core.ExclusiveGroups)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 标题行
            EditorGUILayout.BeginHorizontal();
            string egName = _core.GetExclusiveGroupName(eg.exclusiveGroupId);
            string newName = EditorGUILayout.TextField(egName, GUILayout.Width(200));
            if (newName != egName) _core.SetExclusiveGroupName(eg.exclusiveGroupId, newName);

            EditorGUILayout.LabelField($"(ID {eg.exclusiveGroupId})", EditorStyles.miniLabel);

            if (GUILayout.Button("Dissolve", GUILayout.Width(80)))
            {
                _core.DissolveExclusiveGroup(eg.exclusiveGroupId);
                AutoSave();
                _messages.Add($"Dissolved exclusive group ID {eg.exclusiveGroupId}.");
                Repaint();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue;
            }
            EditorGUILayout.EndHorizontal();

            // 成员列表
            var memberNames = _core.GetExclusiveGroupMemberNames(eg.exclusiveGroupId);
            if (memberNames.Count > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var m in memberNames)
                    EditorGUILayout.LabelField(m, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("(empty - add groups or slots below)", EditorStyles.miniLabel);
            }

            // 添加成员按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Group to This Exclusive", GUILayout.Width(220)))
                ShowGroupPickerForExclusive(eg.exclusiveGroupId);
            if (GUILayout.Button("Add Slot to This Exclusive", GUILayout.Width(220)))
                ShowSlotPickerForExclusive(eg.exclusiveGroupId);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(4);
    }

    private void ShowGroupPickerForExclusive(int exclusiveGroupId)
    {
        var menu = new GenericMenu();
        foreach (var g in _core.Groups)
        {
            int gId = g.groupId;
            bool alreadyInOther = false;
            foreach (var eg in _core.ExclusiveGroups)
            {
                if (eg.exclusiveGroupId != exclusiveGroupId && eg.memberGroupIds.Contains(gId))
                {
                    alreadyInOther = true;
                    break;
                }
            }
            string label = alreadyInOther ? $"{g.groupName} (ID {gId}) [in other exclusive]" : $"{g.groupName} (ID {gId})";
            menu.AddItem(new GUIContent(label), false, (object id) =>
            {
                int groupId = (int)id;
                _core.AddGroupToExclusive(exclusiveGroupId, groupId);
                AutoSave();
                _messages.Add($"Added group '{_core.GetGroupName(groupId)}' to exclusive group ID {exclusiveGroupId}.");
                Repaint();
            }, gId);
        }
        menu.ShowAsContext();
    }

    private void ShowSlotPickerForExclusive(int exclusiveGroupId)
    {
        var menu = new GenericMenu();
        for (int i = 0; i < _core.StyleSlots.Count; i++)
        {
            int idx = i;
            string label = _core.StyleSlots[i].slotName;
            bool alreadyInOther = false;
            foreach (var eg in _core.ExclusiveGroups)
            {
                if (eg.exclusiveGroupId != exclusiveGroupId && eg.memberSlotIndices.Contains(idx))
                {
                    alreadyInOther = true;
                    break;
                }
            }
            if (alreadyInOther)
                label += " [in other exclusive]";

            menu.AddItem(new GUIContent(label), false, (object index) =>
            {
                int slotIndex = (int)index;
                _core.AddSlotToExclusive(exclusiveGroupId, slotIndex);
                AutoSave();
                _messages.Add($"Added slot '{_core.StyleSlots[slotIndex].slotName}' to exclusive group ID {exclusiveGroupId}.");
                Repaint();
            }, idx);
        }
        menu.ShowAsContext();
    }
}
