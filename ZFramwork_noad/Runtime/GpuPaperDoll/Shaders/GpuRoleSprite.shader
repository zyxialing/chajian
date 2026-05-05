Shader "GpuPaperDoll/Sprite"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        // GPU Paper Doll 参数（通过 MaterialPropertyBlock 传递）
        [HideInInspector] _UVRect("UV Rect", Vector) = (0,0,1,1)
        [HideInInspector] _CropOffset("Crop Offset", Vector) = (0,0,0,0)
        [HideInInspector] _Size("Size", Vector) = (1,1,1,1)
        [HideInInspector] _AtlasWidth("Atlas Width", Float) = 1
        [HideInInspector] _AtlasHeight("Atlas Height", Float) = 1

        // GPU 动画数据（通过 MaterialPropertyBlock 传递）
        [HideInInspector] _AnimTex("Anim Tex", 2D) = "black" {}
        [HideInInspector] _AnimFrame("Anim Frame", Float) = 0
        [HideInInspector] _AnimSlotCount("Anim Slot Count", Float) = 1
        [HideInInspector] _AnimTexHeight("Anim Tex Height", Float) = 1
    }
    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

                        struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                half4   color       : COLOR;
                float2  uv          : TEXCOORD0;
                half2   lightingUV  : TEXCOORD1;
                half3   slotColor   : TEXCOORD3;
                #if defined(DEBUG_DISPLAY)
                    float3  positionWS  : TEXCOORD2;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            half4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;

            // per-batch uniform（每个 batch 固定一个 sprite）
            float4 _UVRect;
            float4x4 _SpriteMatrix;

            // GPU Paper Doll Instanced 属性
            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _CropOffset)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Size)
            UNITY_DEFINE_INSTANCED_PROP(float, _AtlasWidth)
            UNITY_DEFINE_INSTANCED_PROP(float, _AtlasHeight)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            // per-instance 数据（通过 ComputeBuffer 传入）
            StructuredBuffer<float4> _InstanceData;
            // 动画纹理（通过 MaterialPropertyBlock 传入）
            TEXTURE2D(_AnimTex);
            SAMPLER(sampler_AnimTex);
            float _AnimSlotCount;
            float _AnimTexHeight;
            float _AnimFrame;
            int _InstanceOffset;

            #if USE_SHAPE_LIGHT_TYPE_0
                SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
                SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
                SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
                SHAPE_LIGHT(3)
            #endif

            Varyings CombinedShapeLightVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // 从 per-instance 数据获取 slotIndex
                uint instID = 0;
                #if defined(UNITY_INSTANCING_ENABLED)
                    instID = unity_InstanceID;
                #endif
                                float4 instData = _InstanceData[instID + _InstanceOffset];
                float slotIndex = instData.x;
                half3 slotColor = instData.yzw;

                                                // 采样动画纹理，构建 slotMatrix
                float frameIndex = _AnimFrame;
                float texWidth = _AnimSlotCount * 3.0;
                float y = (frameIndex + 0.5) / _AnimTexHeight;

                // 三段式横向拼接：[data0 全部slot][data1 全部slot][data2 全部slot]
                float x0 = (slotIndex + 0.5) / texWidth;
                float x1 = (_AnimSlotCount + slotIndex + 0.5) / texWidth;
                float x2 = (_AnimSlotCount * 2.0 + slotIndex + 0.5) / texWidth;

                float4 animData0 = SAMPLE_TEXTURE2D_LOD(_AnimTex, sampler_AnimTex, float2(x0, y), 0);
                float4 animData1 = SAMPLE_TEXTURE2D_LOD(_AnimTex, sampler_AnimTex, float2(x1, y), 0);
                float4 animData2 = SAMPLE_TEXTURE2D_LOD(_AnimTex, sampler_AnimTex, float2(x2, y), 0);

                                float3 posOffset = float3(animData0.x, animData0.y, 0.0);
                float3 scale = float3(animData0.z, animData0.w, 1.0);
                float sinRot = animData1.x;
                float cosRot = animData1.y;

                // 从动画纹理读取 slot 颜色
                half4 animSlotColor = half4(animData1.b, animData1.a, animData2.r, animData2.g);
                // 乘以 Agent 颜色
                half4 finalSlotColor = animSlotColor * half4(slotColor, 1.0);
                float4x4 slotMatrix = float4x4(
                cosRot * scale.x, -sinRot * scale.y, 0, posOffset.x,
                sinRot * scale.x, cosRot * scale.y, 0, posOffset.y,
                0, 0, scale.z, posOffset.z,
                0, 0, 0, 1
                );

                // 使用当前 batch 固定的 spriteMatrix
                float4x4 spriteMatrix = _SpriteMatrix;

                // 应用 rootMatrix * slotMatrix * spriteMatrix 到顶点
                float4 localPos = mul(spriteMatrix, float4(v.positionOS, 1.0));
                localPos = mul(slotMatrix, localPos);
                float3 worldPos = mul((float4x4)UNITY_MATRIX_M, localPos).xyz;

                o.positionCS = TransformWorldToHClip(worldPos);
                #if defined(DEBUG_DISPLAY)
                    o.positionWS = worldPos;
                #endif
                                o.uv = v.uv;
                                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                                o.color = v.color * _Color * _RendererColor;
                                o.slotColor = finalSlotColor.rgb;
                                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                                // 将 0-1 UV 映射到图集中的实际 UV 区域
                                float4 uvRect = _UVRect;
                                float2 atlasUV;
                                atlasUV.x = lerp(uvRect.x, uvRect.z, i.uv.x);
                                atlasUV.y = lerp(uvRect.y, uvRect.w, i.uv.y);

                half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV);
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, atlasUV);

                                // 应用 Instance 颜色
                main *= UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
                // 应用 Slot 颜色
                main.rgb *= i.slotColor;

                // 裁剪半透明黑边
                // clip(main.a - 0.01);

                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(main.rgb, main.a, mask, surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float4 tangent      : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                half4   color           : COLOR;
                float2  uv              : TEXCOORD0;
                half3   normalWS        : TEXCOORD1;
                half3   tangentWS       : TEXCOORD2;
                half3   bitangentWS     : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            half4 _NormalMap_ST;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _UVRect)
            UNITY_DEFINE_INSTANCED_PROP(float4, _CropOffset)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Size)
            UNITY_DEFINE_INSTANCED_PROP(float, _AtlasWidth)
            UNITY_DEFINE_INSTANCED_PROP(float, _AtlasHeight)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings NormalsRenderingVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 cropOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _CropOffset);
                float4 size = UNITY_ACCESS_INSTANCED_PROP(Props, _Size);
                float3 pos = attributes.positionOS;
                pos.x += cropOffset.x;
                pos.y += cropOffset.y;

                o.positionCS = TransformObjectToHClip(pos);
                o.uv = attributes.uv;
                o.color = attributes.color;
                o.normalWS = -GetViewForwardDir();
                o.tangentWS = TransformObjectToWorldDir(attributes.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * attributes.tangent.w;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            half4 NormalsRenderingFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                                float4 uvRect = UNITY_ACCESS_INSTANCED_PROP(Props, _UVRect);
                                float2 atlasUV;
                                atlasUV.x = lerp(uvRect.x, uvRect.z, i.uv.x);
                                atlasUV.y = lerp(uvRect.y, uvRect.w, i.uv.y);

                                const half4 mainTex = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, atlasUV));
                return NormalsRenderingShared(mainTex, normalTS, i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                float4  color           : COLOR;
                float2  uv              : TEXCOORD0;
                #if defined(DEBUG_DISPLAY)
                    float3  positionWS  : TEXCOORD2;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _UVRect)
            UNITY_DEFINE_INSTANCED_PROP(float4, _CropOffset)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Size)
            UNITY_DEFINE_INSTANCED_PROP(float, _AtlasWidth)
            UNITY_DEFINE_INSTANCED_PROP(float, _AtlasHeight)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings UnlitVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 cropOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _CropOffset);
                float4 size = UNITY_ACCESS_INSTANCED_PROP(Props, _Size);
                float3 pos = attributes.positionOS;
                pos.x += cropOffset.x;
                pos.y += cropOffset.y;

                o.positionCS = TransformObjectToHClip(pos);
                #if defined(DEBUG_DISPLAY)
                    o.positionWS = TransformObjectToWorld(pos);
                #endif
                o.uv = attributes.uv;
                o.color = attributes.color * _Color * _RendererColor;
                return o;
            }

            float4 UnlitFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                                float4 uvRect = UNITY_ACCESS_INSTANCED_PROP(Props, _UVRect);
                                float2 atlasUV;
                                atlasUV.x = lerp(uvRect.x, uvRect.z, i.uv.x);
                                atlasUV.y = lerp(uvRect.y, uvRect.w, i.uv.y);

                                float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV);
                // 应用 Instance 颜色
                mainTex *= UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
                // 裁剪半透明黑边
                // clip(mainTex.a - 0.01);
                #if defined(DEBUG_DISPLAY)
                    SurfaceData2D surfaceData;
                    InputData2D inputData;
                    half4 debugColor = 0;
                    InitializeSurfaceData(mainTex.rgb, mainTex.a, surfaceData);
                    InitializeInputData(i.uv, inputData);
                    SETUP_DEBUG_DATA_2D(inputData, i.positionWS);
                    if(CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
                    {
                        return debugColor;
                    }
                #endif
                return mainTex;
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
