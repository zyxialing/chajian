using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GpuAnimationBaker
{
    public static GpuAnimationBakeData BakeFromPrefab(GameObject prefab, AnimationClip clip, List<GpuRoleSlot> slotDefs)
    {
        if (prefab == null || clip == null || slotDefs == null) return null;

        var slotKeys = slotDefs.Select((s, i) => new BakedSlotData
        {
            slotKey = s.slotKey,
            slotName = s.slotName.Trim(),
            slotPath = s.path,
            drawOrder = i,
            internalOrder = s.internalOrder
        }).ToList();

        return Bake(prefab, clip, slotKeys);
    }

    public static GpuAnimationBakeData Bake(GameObject prefab, AnimationClip clip, List<BakedSlotData> slotKeys)
    {
        if (prefab == null || clip == null) return null;

        float frameRate = clip.frameRate > 0 ? clip.frameRate : 30f;
        float length = clip.length;
        int totalFrames = Mathf.CeilToInt(length * frameRate);
        if (totalFrames < 1) totalFrames = 1;

        GameObject instance = Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        Transform root = instance.transform;

        Vector3 rootLocalPos = root.localPosition;
        Quaternion rootLocalRot = root.localRotation;
        Vector3 rootLocalScale = root.localScale;

        var data = ScriptableObject.CreateInstance<GpuAnimationBakeData>();
        data.animName = clip.name;
        data.frameRate = frameRate;
        data.length = length;
        data.totalFrames = totalFrames;
        data.slotKeys = slotKeys;

        for (int frame = 0; frame < totalFrames; frame++)
        {
            float time = (float)frame / frameRate;
            clip.SampleAnimation(instance, time);

            root.localPosition = rootLocalPos;
            root.localRotation = rootLocalRot;
            root.localScale = rootLocalScale;

                        var frameData = new BakedFrameData();
            foreach (var slot in slotKeys)
            {
                string findPath = !string.IsNullOrEmpty(slot.slotPath) ? slot.slotPath : slot.slotKey;
                Transform t = GpuRoleUtility.FindTransformByPath(root, findPath);
                if (t != null)
                {
                    Vector3 localPos = root.InverseTransformPoint(t.position);
                    Quaternion localRot = Quaternion.Inverse(root.rotation) * t.rotation;
                    Vector3 localScale = t.lossyScale;
                    if (root.lossyScale.x != 0) localScale.x /= root.lossyScale.x;
                    if (root.lossyScale.y != 0) localScale.y /= root.lossyScale.y;
                    if (root.lossyScale.z != 0) localScale.z /= root.lossyScale.z;

                    frameData.positions.Add(localPos);
                    frameData.rotations.Add(localRot);
                    frameData.scales.Add(localScale);

                    // ²É¼¯ SpriteRenderer ÑÕÉ«
                    var sr = t.GetComponent<SpriteRenderer>();
                    frameData.colors.Add(sr != null ? sr.color : Color.white);
                }
                else
                {
                    frameData.positions.Add(Vector3.zero);
                    frameData.rotations.Add(Quaternion.identity);
                    frameData.scales.Add(Vector3.one);
                    frameData.colors.Add(Color.white);
                }
            }
            data.frames.Add(frameData);
        }

        Object.DestroyImmediate(instance);
        return data;
    }
}
