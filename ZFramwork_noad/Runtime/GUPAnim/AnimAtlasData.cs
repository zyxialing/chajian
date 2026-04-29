using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AnimAtlasData", menuName = "Anim/AtlasData")]
public class AnimAtlasData : ScriptableObject
{
    public Texture2D atlas;
    public Vector2 centerOffset;
    public Vector2 shadowOffset;
    public Vector2 shadowSize = new Vector2(44f, 14f);
    public List<AnimClip> clips;
    public List<AnimCharacter> characters;
    public List<FrameRuntimeData> frames;
    public Vector4[] frameUVs;
    public Vector4[] frameOffsetFrames;
    public Vector4[] frameBaseSizes;
    public int[] clipStartFrames;
    public int[] clipFrameCounts;
    public float[] clipFpsValues;
    public bool[] clipLoops;

    private Dictionary<string, int> clipIndexMap;
    private Dictionary<string, int> characterIndexMap;

    public void EnsureRuntimeCache()
    {
        if (IsRuntimeCacheValid())
        {
            return;
        }

        RebuildRuntimeCache();
    }

    public void RebuildRuntimeCache()
    {
        int frameCount = frames != null ? frames.Count : 0;
        frameUVs = new Vector4[frameCount];
        frameOffsetFrames = new Vector4[frameCount];
        frameBaseSizes = new Vector4[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            FrameRuntimeData frame = frames[i];
            frameUVs[i] = new Vector4(frame.uv.x, frame.uv.y, frame.uv.width, frame.uv.height);
            frameOffsetFrames[i] = new Vector4(frame.offset.x, frame.offset.y, frame.size.x, frame.size.y);
            frameBaseSizes[i] = new Vector4(frame.baseSize.x, frame.baseSize.y, 0f, 0f);
        }

        int clipCount = clips != null ? clips.Count : 0;
        clipStartFrames = new int[clipCount];
        clipFrameCounts = new int[clipCount];
        clipFpsValues = new float[clipCount];
        clipLoops = new bool[clipCount];

        for (int i = 0; i < clipCount; i++)
        {
            AnimClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            clipStartFrames[i] = clip.startFrame;
            clipFrameCounts[i] = clip.frameCount;
            clipFpsValues[i] = clip.fps;
            clipLoops[i] = clip.loop;
        }
    }

    public bool TryGetClip(string clipName, out AnimClip clip)
    {
        EnsureClipMap();

        if (clipIndexMap.TryGetValue(clipName, out int index))
        {
            clip = clips[index];
            return true;
        }

        clip = null;
        return false;
    }

    public bool TryGetClipIndex(string clipName, out int index)
    {
        EnsureClipMap();
        return clipIndexMap.TryGetValue(clipName, out index);
    }

    public bool HasCharacters()
    {
        return characters != null && characters.Count > 0;
    }

    public int GetDefaultCharacterIndex()
    {
        return HasCharacters() ? 0 : -1;
    }

    public AnimCharacter GetCharacter(int characterIndex)
    {
        if (!HasCharacters() || characterIndex < 0 || characterIndex >= characters.Count)
        {
            return null;
        }

        return characters[characterIndex];
    }

    public bool TryGetCharacterIndex(string characterName, out int index)
    {
        EnsureCharacterMap();
        if (!string.IsNullOrEmpty(characterName) && characterIndexMap.TryGetValue(characterName, out index))
        {
            return true;
        }

        index = -1;
        return false;
    }

    public bool TryGetCharacterClipIndex(int characterIndex, string clipName, out int clipIndex)
    {
        clipIndex = -1;

        if (!HasCharacters())
        {
            return TryGetClipIndex(clipName, out clipIndex);
        }

        AnimCharacter character = GetCharacter(characterIndex);
        if (character == null || character.clips == null || character.clips.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrEmpty(clipName))
        {
            clipName = character.clips[0].name;
        }

        return TryGetClipIndex($"{character.name}/{clipName}", out clipIndex);
    }

    public FrameRuntimeData GetClipFrame(AnimClip clip, int clipFrameIndex)
    {
        if (frames == null || clip == null || clip.frameCount <= 0)
        {
            return default;
        }

        int frameOffset = clip.loop
            ? Mod(clipFrameIndex, clip.frameCount)
            : Mathf.Clamp(clipFrameIndex, 0, clip.frameCount - 1);

        return frames[clip.startFrame + frameOffset];
    }

    public int GetFrameIndex(int clipIndex, int clipFrameIndex)
    {
        EnsureRuntimeCache();

        if (clipFrameCounts == null || clipIndex < 0 || clipIndex >= clipFrameCounts.Length)
        {
            return -1;
        }

        int frameCount = clipFrameCounts[clipIndex];
        if (frameCount <= 0)
        {
            return -1;
        }

        int frameOffset = clipLoops[clipIndex]
            ? Mod(clipFrameIndex, frameCount)
            : Mathf.Clamp(clipFrameIndex, 0, frameCount - 1);

        return clipStartFrames[clipIndex] + frameOffset;
    }

    private bool IsRuntimeCacheValid()
    {
        int frameCount = frames != null ? frames.Count : 0;
        int clipCount = clips != null ? clips.Count : 0;
        return frameUVs != null
            && frameOffsetFrames != null
            && frameBaseSizes != null
            && clipStartFrames != null
            && clipFrameCounts != null
            && clipFpsValues != null
            && clipLoops != null
            && frameUVs.Length == frameCount
            && frameOffsetFrames.Length == frameCount
            && frameBaseSizes.Length == frameCount
            && clipStartFrames.Length == clipCount
            && clipFrameCounts.Length == clipCount
            && clipFpsValues.Length == clipCount
            && clipLoops.Length == clipCount;
    }

    private void EnsureClipMap()
    {
        if (clipIndexMap != null)
        {
            return;
        }

        clipIndexMap = new Dictionary<string, int>();
        if (clips == null)
        {
            return;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null && !string.IsNullOrEmpty(clips[i].name))
            {
                clipIndexMap[clips[i].name] = i;
            }
        }
    }

    private void EnsureCharacterMap()
    {
        if (characterIndexMap != null)
        {
            return;
        }

        characterIndexMap = new Dictionary<string, int>();
        if (characters == null)
        {
            return;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && !string.IsNullOrEmpty(characters[i].name))
            {
                characterIndexMap[characters[i].name] = i;
            }
        }
    }

    private int Mod(int value, int length)
    {
        int result = value % length;
        return result < 0 ? result + length : result;
    }
}

[System.Serializable]
public class AnimClip
{
    public string name;
    public int startFrame;
    public int frameCount;
    public float fps = 10f;
    public bool loop = true;
}

[System.Serializable]
public class AnimCharacter
{
    public string name;
    public float baseScale = 1f;
    public Vector2 centerOffset;
    public Vector2 shadowOffset;
    public Vector2 shadowSize = new Vector2(44f, 14f);
    public List<AnimClip> clips;
}

[System.Serializable]
public class FrameRuntimeData
{
    public Rect uv;
    public Vector2 offset;
    public Vector2 size;
    public Vector2 baseSize;
}
