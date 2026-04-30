using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
[CanEditMultipleObjects]
[CustomEditor(typeof(AICollider))]
public class AIColliderEditor : Editor
{
    private BoxBoundsHandle handle = new BoxBoundsHandle();

    public override void OnInspectorGUI()
    {
        AICollider col = (AICollider)target;

        serializedObject.Update();

        // ? 遍历所有字段
        SerializedProperty prop = serializedObject.GetIterator();

        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            HashSet<string> hideProps = new HashSet<string>()
            {
                "skyOffset","skySize",
                "bodyOffset","bodySize",
                "groundOffset","groundSize",
                "attackLine"
            };

            if (hideProps.Contains(prop.name))
            {
                continue;
            }

            EditorGUILayout.PropertyField(prop, true);
        }

        GUILayout.Space(10);
        GUILayout.Label("Collider Editor", EditorStyles.boldLabel);

        DrawToggle(ref col.editSky, "Edit Sky", Color.blue);
        DrawToggle(ref col.editBody, "Edit Body", Color.green);
        DrawToggle(ref col.editGround, "Edit Ground", Color.yellow);
        DrawToggle(ref col.editAttack, "Edit Attack", Color.red);

        GUILayout.Space(10);

        // ? 只在编辑时显示对应参数
        if (col.editSky)
        {
            DrawVector2("Sky Offset", ref col.skyOffset);
            DrawVector2("Sky Size", ref col.skySize);
        }

        if (col.editBody)
        {
            DrawVector2("Body Offset", ref col.bodyOffset);
            DrawVector2("Body Size", ref col.bodySize);
        }

        if (col.editGround)
        {
            DrawVector2("Ground Offset", ref col.groundOffset);
            DrawVector2("Ground Size", ref col.groundSize);
        }
        if (col.editAttack && col.attackRangeBox != null)
        {
            DrawVector2("Attack Offset", ref col.attackRangeBox.offset);
            DrawVector2("Attack Size", ref col.attackRangeBox.size);
        }
        // ? ? 就在这里
        if (col.editAttack)
        {
            col.attackLine = EditorGUILayout.FloatField("Attack Line", col.attackLine);
        }
        serializedObject.ApplyModifiedProperties();
    }
    void DrawVector2(string label, ref Vector2 value)
    {
        EditorGUI.BeginChangeCheck();

        Vector2 newValue = EditorGUILayout.Vector2Field(label, value);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Modify Collider");
            value = newValue;
            EditorUtility.SetDirty(target);
        }
    }
    void DrawToggle(ref bool value, string label, Color color)
    {
        GUI.backgroundColor = value ? color : Color.white;

        if (GUILayout.Button(label))
        {
            value = !value;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;
    }

    private void OnSceneGUI()
    {
        AICollider col = (AICollider)target;

        // ? 没开任何编辑就不画
        if (!col.editSky && !col.editBody && !col.editGround) return;

        // ? 防误操作
        Tools.current = Tool.None;

        Transform tf = col.transform;

        if (col.editSky)
        {
            DrawHandle(tf, col.skyOffset, col.skySize, Color.blue,
                (o, s) => { col.skyOffset = o; col.skySize = s; });
        }

        if (col.editBody)
        {
            DrawHandle(tf, col.bodyOffset, col.bodySize, Color.green,
                (o, s) => { col.bodyOffset = o; col.bodySize = s; });
        }

        if (col.editGround)
        {
            DrawHandle(tf, col.groundOffset, col.groundSize, Color.yellow,
                (o, s) => { col.groundOffset = o; col.groundSize = s; });
        }
        if (col.editAttack)
        {

            Vector2 vector2 = new Vector2(col.transform.position.x, col.transform.position.y) + col.bodyOffset;
            Vector2 vectorG2 = new Vector2(col.transform.position.x, col.transform.position.y) + col.groundOffset;

            float emY = vector2.y - vectorG2.y + 0;
            float emX = col.bodySize.x * 0.5f + col.attackLine;

            Vector2 center;

            if (col.transform.localScale.x < 0)
            {
                center = vectorG2 + new Vector2(emX / 2, emY / 2f);
            }
            else
            {
                center = vectorG2 + new Vector2(-emX / 2, emY / 2f);
            }

            // ? 转成 offset（因为你的 DrawHandle 用的是 offset）
            Vector2 offset = center - (Vector2)col.transform.position;
            Vector2 size = new Vector2(emX, emY);

            DrawHandle(col.transform,
                offset,
                size,
                Color.red,
                (o, s) =>
                {
                    // ? 这里暂时不回写（因为你attack是计算的）
                });
        }
    }

    void DrawHandle(
     Transform tf,
     Vector2 offset,
     Vector2 size,
     Color color,
     System.Action<Vector2, Vector2> onChange)
    {
        using (new Handles.DrawingScope(color))
        {
            Handles.matrix = tf.localToWorldMatrix;

            handle.center = offset;
            handle.size = size;

            EditorGUI.BeginChangeCheck();

            // 盒子（缩放）
            handle.DrawHandle();

            // 中心点拖动（无箭头）
#if UNITY_2022_1_OR_NEWER
            Vector3 newCenter = Handles.FreeMoveHandle(
                handle.center,
                0.08f,
                Vector3.zero,
                Handles.DotHandleCap
            );
#else
            Vector3 newCenter = Handles.FreeMoveHandle(
                handle.center,
                Quaternion.identity,
                0.08f,
                Vector3.zero,
                Handles.DotHandleCap
            );
#endif

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modify Collider");

                handle.center = newCenter;

                onChange(handle.center, handle.size);

                EditorUtility.SetDirty(target);
            }

            Handles.matrix = Matrix4x4.identity;
        }
    }

}
