using UnityEngine;

public class GPUAnimManager : MonoBehaviour
{
    private const int BatchSize = 1023;

    public Mesh mesh;
    public Material material;
    public Material shadowMaterial;
    public AnimAtlasData data;
    public Camera renderCamera;
    public int initialCapacity = 128;
    public float fps = 10f;
    public float defaultScale = 1f;
    public bool useYAsDepth = true;
    public float yToDepthScale = 0.01f;
    public float depthOffset;
    public bool drawShadow = true;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    public bool cullByCamera = true;
    public float cullPadding = 2f;
    private Matrix4x4[] matrices;
    private Vector3[] positions;
    private float[] renderScales;
    private bool[] roleAlive;
    private bool[] roleVisible;
    private bool[] flipXs;
    private int[] characterIndices;
    private int[] clipIndices;
    private float[] animTimes;
    private float[] animSpeeds;
    private int[] renderIndices;
    private int[] freeIds;

    private Matrix4x4[] batchMatrices;
    private Vector4[] batchUVs;
    private Vector4[] batchOffsetFrames;
    private Vector4[] batchBaseSizes;
    private Vector4[] batchCenterOffsets;
    private Vector4[] batchShadowOffsets;
    private Vector4[] batchShadowSizes;

    private MaterialPropertyBlock mpb;
    private readonly RenderIndexComparer renderIndexComparer = new RenderIndexComparer();
    private AnimAtlasData cachedCullData;
    private Vector2 cachedCullExtents = Vector2.one;
    private bool cachedCullDrawShadow;
    private int activeCount;
    private int renderCount;
    private int roleCount;
    private int freeCount;

    public int RoleCount => roleCount;

    private void Start()
    {
        EnsureRuntime();
    }

    private void Update()
    {
        if (!IsReady())
        {
            return;
        }

        UpdateAnimData(Time.deltaTime);
        Draw();
    }

    public int CreateRole(Vector3 position, string clipName = null)
    {
        return CreateRole(position, defaultScale, clipName);
    }

    public int CreateRole(Vector3 position, float scale, string clipName = null)
    {
        return CreateRole(null, position, scale, clipName);
    }

    public int CreateRole(string characterName, Vector3 position, string clipName = null)
    {
        return CreateRole(characterName, position, defaultScale, clipName);
    }

    public int CreateRole(string characterName, Vector3 position, float scale, string clipName = null)
    {
        EnsureRuntime();
        if (data == null || data.clips == null || data.clips.Count == 0)
        {
            return -1;
        }
        data.EnsureRuntimeCache();

        EnsureCapacity(activeCount + 1);

        int characterIndex = ResolveCharacterIndex(characterName);
        int clipIndex = 0;
        if (data.HasCharacters())
        {
            if (!data.TryGetCharacterClipIndex(characterIndex, clipName, out clipIndex))
            {
                return -1;
            }
        }
        else if (!string.IsNullOrEmpty(clipName) && data != null)
        {
            data.TryGetClipIndex(clipName, out clipIndex);
        }

        int id = AllocRoleId();
        positions[id] = position;
        renderScales[id] = scale;
        roleAlive[id] = true;
        roleVisible[id] = true;
        flipXs[id] = false;
        characterIndices[id] = characterIndex;
        clipIndices[id] = Mathf.Clamp(clipIndex, 0, data.clips.Count - 1);
        animTimes[id] = 0f;
        animSpeeds[id] = 1f;
        UpdateMatrix(id);
        return id;
    }

    public void RemoveRole(int id)
    {
        if (!IsValidRole(id))
        {
            return;
        }

        roleAlive[id] = false;
        roleVisible[id] = false;
        freeIds[freeCount] = id;
        freeCount++;
        roleCount--;
    }

    public void SetPosition(int id, Vector3 position)
    {
        if (!IsValidRole(id))
        {
            return;
        }

        positions[id] = position;
        UpdateMatrix(id);
    }

    public void SetScale(int id, float scale)
    {
        if (!IsValidRole(id))
        {
            return;
        }

        renderScales[id] = scale;
        UpdateMatrix(id);
    }

    public void SetVisible(int id, bool visible)
    {
        if (!IsValidRole(id))
        {
            return;
        }

        roleVisible[id] = visible;
    }

    public void SetFlipX(int id, bool flipX)
    {
        if (!IsValidRole(id))
        {
            return;
        }

        flipXs[id] = flipX;
        UpdateMatrix(id);
    }

    public void SetClip(int id, string clipName, bool resetTime = true)
    {
        if (!IsValidRole(id) || data == null)
        {
            return;
        }

        bool found = data.HasCharacters()
            ? data.TryGetCharacterClipIndex(characterIndices[id], clipName, out int clipIndex)
            : data.TryGetClipIndex(clipName, out clipIndex);

        if (!found)
        {
            return;
        }

        clipIndices[id] = clipIndex;
        if (resetTime)
        {
            animTimes[id] = 0f;
        }
    }

    public void SetCharacter(int id, string characterName, string clipName = null, bool resetTime = true)
    {
        if (!IsValidRole(id) || data == null || !data.HasCharacters())
        {
            return;
        }

        int characterIndex = ResolveCharacterIndex(characterName);
        if (characterIndex < 0)
        {
            return;
        }

        if (!data.TryGetCharacterClipIndex(characterIndex, clipName, out int clipIndex))
        {
            return;
        }

        characterIndices[id] = characterIndex;
        clipIndices[id] = clipIndex;
        UpdateMatrix(id);
        if (resetTime)
        {
            animTimes[id] = 0f;
        }
    }

    public void SetSpeed(int id, float speed)
    {
        if (!IsValidRole(id))
        {
            return;
        }

        animSpeeds[id] = speed;
    }

    public void SetAnimTime(int id, float time)
    {
        if (!IsValidRole(id))
        {
            return;
        }

        animTimes[id] = Mathf.Max(0f, time);
    }

    public void ClearRoles()
    {
        activeCount = 0;
        roleCount = 0;
        freeCount = 0;
    }

    [ContextMenu("Log Shadow Offset")]
    private void LogShadowOffset()
    {
        Vector2 dataOffset = data != null ? data.shadowOffset : Vector2.zero;
        Vector2 dataSize = data != null ? data.shadowSize : Vector2.zero;
        Debug.Log(
            $"GPUAnim shadow offset={dataOffset}, size={dataSize}. Shadow color/alpha is controlled by GPUAnimManager.",
            this
        );
    }

    private void UpdateAnimData(float deltaTime)
    {
        for (int i = 0; i < activeCount; i++)
        {
            if (!roleAlive[i])
            {
                continue;
            }

            int clipIndex = clipIndices[i];
            if (data.clipFrameCounts == null || clipIndex < 0 || clipIndex >= data.clipFrameCounts.Length)
            {
                continue;
            }

            int frameCount = data.clipFrameCounts[clipIndex];
            if (frameCount <= 0)
            {
                continue;
            }

            float clipFps = data.clipFpsValues[clipIndex] > 0f ? data.clipFpsValues[clipIndex] : fps;
            animTimes[i] += deltaTime * animSpeeds[i];

            if (!data.clipLoops[clipIndex])
            {
                float maxTime = Mathf.Max(0f, (frameCount - 1) / clipFps);
                animTimes[i] = Mathf.Min(animTimes[i], maxTime);
            }
        }
    }

    private void Draw()
    {
        mpb.SetTexture("_MainTex", data.atlas);
        BuildRenderIndices();
        if (renderCount == 0)
        {
            return;
        }

        for (int start = 0; start < renderCount; start += BatchSize)
        {
            int batchCount = Mathf.Min(BatchSize, renderCount - start);
            FillBatch(start, batchCount);

            mpb.SetVectorArray("_UV", batchUVs);
            mpb.SetVectorArray("_OffsetFrame", batchOffsetFrames);
            mpb.SetVectorArray("_BaseSize", batchBaseSizes);
            mpb.SetVectorArray("_CenterOffset", batchCenterOffsets);
            mpb.SetVectorArray("_ShadowOffset", batchShadowOffsets);
            mpb.SetVectorArray("_ShadowSize", batchShadowSizes);
            mpb.SetFloat("_ShadowEnabled", drawShadow ? 1f : 0f);
            mpb.SetColor("_ShadowColor", shadowColor);

            if (drawShadow && shadowMaterial != null)
            {
                Graphics.DrawMeshInstanced(
                    mesh,
                    0,
                    shadowMaterial,
                    batchMatrices,
                    batchCount,
                    mpb
                );
            }

            Graphics.DrawMeshInstanced(
                mesh,
                0,
                material,
                batchMatrices,
                batchCount,
                mpb
            );
        }
    }

    private void FillBatch(int start, int batchCount)
    {
        for (int i = 0; i < batchCount; i++)
        {
            int roleIndex = renderIndices[start + i];
            int clipIndex = clipIndices[roleIndex];
            float clipFps = data.clipFpsValues[clipIndex] > 0f ? data.clipFpsValues[clipIndex] : fps;
            int frameIndex = Mathf.FloorToInt(animTimes[roleIndex] * clipFps);
            int runtimeFrameIndex = data.GetFrameIndex(clipIndex, frameIndex);

            batchMatrices[i] = matrices[roleIndex];
            batchUVs[i] = data.frameUVs[runtimeFrameIndex];
            batchOffsetFrames[i] = data.frameOffsetFrames[runtimeFrameIndex];
            batchBaseSizes[i] = data.frameBaseSizes[runtimeFrameIndex];
            AnimCharacter character = data.GetCharacter(characterIndices[roleIndex]);
            Vector2 centerOffset = character != null ? character.centerOffset : data.centerOffset;
            Vector2 shadowOffset = character != null ? character.shadowOffset : data.shadowOffset;
            Vector2 shadowSize = character != null ? character.shadowSize : data.shadowSize;
            batchCenterOffsets[i] = new Vector4(centerOffset.x, centerOffset.y, 0f, 0f);
            batchShadowOffsets[i] = new Vector4(shadowOffset.x, shadowOffset.y, 0f, 0f);
            batchShadowSizes[i] = new Vector4(shadowSize.x, shadowSize.y, 0f, 0f);
        }
    }

    private void BuildRenderIndices()
    {
        renderCount = 0;
        Rect cameraBounds = default;
        bool canCull = cullByCamera && TryGetCameraBounds(out cameraBounds);
        if (canCull)
        {
            EnsureCullExtents();
        }

        for (int i = 0; i < activeCount; i++)
        {
            if (roleAlive[i] && roleVisible[i] && (!canCull || IsRoleVisible(i, cameraBounds)))
            {
                renderIndices[renderCount] = i;
                renderCount++;
            }
        }

        if (!useYAsDepth)
        {
            renderIndexComparer.Positions = positions;
            System.Array.Sort(renderIndices, 0, renderCount, renderIndexComparer);
        }
    }

    private bool TryGetCameraBounds(out Rect bounds)
    {
        Camera camera = renderCamera?renderCamera:UIManager.Instance.camera_scene;
        if (camera == null)
        {
            bounds = default;
            return false;
        }

        if (camera.orthographic)
        {
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            Vector3 center = camera.transform.position;
            bounds = new Rect(
                center.x - width * 0.5f - cullPadding,
                center.y - height * 0.5f - cullPadding,
                width + cullPadding * 2f,
                height + cullPadding * 2f
            );
            return true;
        }

        float distance = Mathf.Abs(camera.transform.position.z - transform.position.z);
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        float minX = Mathf.Min(bottomLeft.x, topRight.x) - cullPadding;
        float maxX = Mathf.Max(bottomLeft.x, topRight.x) + cullPadding;
        float minY = Mathf.Min(bottomLeft.y, topRight.y) - cullPadding;
        float maxY = Mathf.Max(bottomLeft.y, topRight.y) + cullPadding;
        bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private bool IsRoleVisible(int id, Rect cameraBounds)
    {
        Vector3 position = positions[id];
        float scale = Mathf.Abs(GetFinalScale(id));
        float paddingX = cachedCullExtents.x * scale;
        float paddingY = cachedCullExtents.y * scale;
        return position.x >= cameraBounds.xMin - paddingX
            && position.x <= cameraBounds.xMax + paddingX
            && position.y >= cameraBounds.yMin - paddingY
            && position.y <= cameraBounds.yMax + paddingY;
    }

    private void EnsureCullExtents()
    {
        if (cachedCullData == data
            && cachedCullDrawShadow == drawShadow)
        {
            return;
        }

        cachedCullData = data;
        cachedCullDrawShadow = drawShadow;
        cachedCullExtents = CalculateCullExtents();
    }

    private Vector2 CalculateCullExtents()
    {
        if (data == null || data.frames == null || data.frames.Count == 0)
        {
            return Vector2.one;
        }

        float maxX = 0.5f;
        float maxY = 0.5f;

        if (data.HasCharacters())
        {
            for (int characterIndex = 0; characterIndex < data.characters.Count; characterIndex++)
            {
                AnimCharacter character = data.characters[characterIndex];
                if (character == null || character.clips == null)
                {
                    continue;
                }

                for (int clipIndex = 0; clipIndex < character.clips.Count; clipIndex++)
                {
                    AnimClip clip = character.clips[clipIndex];
                    if (clip == null)
                    {
                        continue;
                    }

                    for (int frameIndex = 0; frameIndex < clip.frameCount; frameIndex++)
                    {
                        AccumulateCullExtents(
                            data.frames[clip.startFrame + frameIndex],
                            character.centerOffset,
                            character.shadowOffset,
                            character.shadowSize,
                            ref maxX,
                            ref maxY
                        );
                    }
                }
            }

            return new Vector2(maxX, maxY);
        }

        for (int i = 0; i < data.frames.Count; i++)
        {
            AccumulateCullExtents(data.frames[i], data.centerOffset, data.shadowOffset, data.shadowSize, ref maxX, ref maxY);
        }

        return new Vector2(maxX, maxY);
    }

    private void AccumulateCullExtents(
        FrameRuntimeData frame,
        Vector2 centerOffset,
        Vector2 shadowOffset,
        Vector2 shadowSize,
        ref float maxX,
        ref float maxY)
    {
        Vector2 baseSize = frame.baseSize;
        float baseHeight = Mathf.Max(baseSize.y, 1f);
        Vector2 anchorPixel = new Vector2(baseSize.x * 0.5f + centerOffset.x, centerOffset.y);

        maxX = Mathf.Max(maxX, Mathf.Abs(0f - anchorPixel.x) / baseHeight);
        maxX = Mathf.Max(maxX, Mathf.Abs(baseSize.x - anchorPixel.x) / baseHeight);
        maxY = Mathf.Max(maxY, Mathf.Abs(0f - anchorPixel.y) / baseHeight);
        maxY = Mathf.Max(maxY, Mathf.Abs(baseSize.y - anchorPixel.y) / baseHeight);

        if (!drawShadow)
        {
            return;
        }

        Vector2 shadowCenter = anchorPixel + shadowOffset;
        Vector2 safeShadowSize = new Vector2(Mathf.Max(1f, shadowSize.x), Mathf.Max(1f, shadowSize.y));
        maxX = Mathf.Max(maxX, Mathf.Abs(shadowCenter.x - safeShadowSize.x - anchorPixel.x) / baseHeight);
        maxX = Mathf.Max(maxX, Mathf.Abs(shadowCenter.x + safeShadowSize.x - anchorPixel.x) / baseHeight);
        maxY = Mathf.Max(maxY, Mathf.Abs(shadowCenter.y - safeShadowSize.y - anchorPixel.y) / baseHeight);
        maxY = Mathf.Max(maxY, Mathf.Abs(shadowCenter.y + safeShadowSize.y - anchorPixel.y) / baseHeight);
    }

    private void UpdateMatrix(int id)
    {
        float finalScale = GetFinalScale(id);
        float xScale = flipXs[id] ? -finalScale : finalScale;
        Vector3 renderPosition = positions[id];
        if (useYAsDepth)
        {
            renderPosition.z = positions[id].z + depthOffset + positions[id].y * yToDepthScale;
        }

        matrices[id] = Matrix4x4.TRS(renderPosition, Quaternion.identity, new Vector3(xScale, finalScale, finalScale));
    }

    private void EnsureRuntime()
    {
        if (mesh == null)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mesh = quad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(quad);
        }

        if (mpb == null)
        {
            mpb = new MaterialPropertyBlock();
        }

        if (material == null)
        {
            Shader bodyShader = Shader.Find("Custom/GPUAnim_Instanced_URP");
            if (bodyShader != null)
            {
                material = new Material(bodyShader);
                material.hideFlags = HideFlags.DontSave;
            }
        }

        if (material != null)
        {
            material.enableInstancing = true;
        }

        if (shadowMaterial == null)
        {
            Shader shadowShader = Shader.Find("Custom/GPUAnim_Shadow_URP");
            if (shadowShader != null)
            {
                shadowMaterial = new Material(shadowShader);
                shadowMaterial.hideFlags = HideFlags.DontSave;
            }
        }

        if (shadowMaterial != null)
        {
            shadowMaterial.enableInstancing = true;
        }

        if (batchMatrices == null)
        {
            batchMatrices = new Matrix4x4[BatchSize];
            batchUVs = new Vector4[BatchSize];
            batchOffsetFrames = new Vector4[BatchSize];
            batchBaseSizes = new Vector4[BatchSize];
            batchCenterOffsets = new Vector4[BatchSize];
            batchShadowOffsets = new Vector4[BatchSize];
            batchShadowSizes = new Vector4[BatchSize];
        }

        EnsureCapacity(Mathf.Max(1, initialCapacity));
        if (data != null)
        {
            data.EnsureRuntimeCache();
        }
    }

    private int AllocRoleId()
    {
        if (freeCount > 0)
        {
            freeCount--;
            roleCount++;
            return freeIds[freeCount];
        }

        EnsureCapacity(activeCount + 1);
        int id = activeCount;
        activeCount++;
        roleCount++;
        return id;
    }

    private void EnsureCapacity(int capacity)
    {
        if (matrices != null && matrices.Length >= capacity)
        {
            return;
        }

        int newCapacity = matrices == null ? Mathf.Max(1, capacity) : Mathf.Max(capacity, matrices.Length * 2);

        Matrix4x4[] newMatrices = new Matrix4x4[newCapacity];
        Vector3[] newPositions = new Vector3[newCapacity];
        float[] newRenderScales = new float[newCapacity];
        bool[] newRoleAlive = new bool[newCapacity];
        bool[] newRoleVisible = new bool[newCapacity];
        bool[] newFlipXs = new bool[newCapacity];
        int[] newCharacterIndices = new int[newCapacity];
        int[] newClipIndices = new int[newCapacity];
        float[] newAnimTimes = new float[newCapacity];
        float[] newAnimSpeeds = new float[newCapacity];
        int[] newRenderIndices = new int[newCapacity];
        int[] newFreeIds = new int[newCapacity];

        if (matrices != null)
        {
            System.Array.Copy(matrices, newMatrices, activeCount);
            System.Array.Copy(positions, newPositions, activeCount);
            System.Array.Copy(renderScales, newRenderScales, activeCount);
            System.Array.Copy(roleAlive, newRoleAlive, activeCount);
            System.Array.Copy(roleVisible, newRoleVisible, activeCount);
            System.Array.Copy(flipXs, newFlipXs, activeCount);
            System.Array.Copy(characterIndices, newCharacterIndices, activeCount);
            System.Array.Copy(clipIndices, newClipIndices, activeCount);
            System.Array.Copy(animTimes, newAnimTimes, activeCount);
            System.Array.Copy(animSpeeds, newAnimSpeeds, activeCount);
            System.Array.Copy(renderIndices, newRenderIndices, activeCount);
            System.Array.Copy(freeIds, newFreeIds, freeCount);
        }

        matrices = newMatrices;
        positions = newPositions;
        renderScales = newRenderScales;
        roleAlive = newRoleAlive;
        roleVisible = newRoleVisible;
        flipXs = newFlipXs;
        characterIndices = newCharacterIndices;
        clipIndices = newClipIndices;
        animTimes = newAnimTimes;
        animSpeeds = newAnimSpeeds;
        renderIndices = newRenderIndices;
        freeIds = newFreeIds;
    }

    private bool IsReady()
    {
        return mesh != null
            && material != null
            && data != null
            && data.atlas != null
            && data.clips != null
            && data.clips.Count > 0
            && data.frames != null
            && data.frames.Count > 0
            && data.frameUVs != null
            && data.frameOffsetFrames != null
            && data.frameBaseSizes != null
            && data.clipStartFrames != null
            && data.clipFrameCounts != null
            && data.clipFpsValues != null
            && data.clipLoops != null
            && roleCount > 0;
    }

    public bool IsValidRole(int id)
    {
        return id >= 0 && id < activeCount && roleAlive[id];
    }

    private int ResolveCharacterIndex(string characterName)
    {
        if (data == null || !data.HasCharacters())
        {
            return -1;
        }

        if (!string.IsNullOrEmpty(characterName) && data.TryGetCharacterIndex(characterName, out int characterIndex))
        {
            return characterIndex;
        }

        return data.GetDefaultCharacterIndex();
    }

    private float GetFinalScale(int id)
    {
        float scale = renderScales[id];
        AnimCharacter character = data != null ? data.GetCharacter(characterIndices[id]) : null;
        return scale * (character != null ? Mathf.Max(0.01f, character.baseScale) : 1f);
    }

    private class RenderIndexComparer : System.Collections.Generic.IComparer<int>
    {
        public Vector3[] Positions;

        public int Compare(int left, int right)
        {
            return Positions[right].y.CompareTo(Positions[left].y);
        }
    }
}
