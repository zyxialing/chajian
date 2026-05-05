Shader "GpuPaperDoll/ExtremeTest"
{
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _AnimTex("Anim Tex", 2D) = "black" {}
        _Color("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_AnimTex);
            SAMPLER(sampler_AnimTex);

            float4 _Color;
            float _AnimSlotCount;
            float _AnimTexHeight;
            float _AnimFrame;
            int _BatchSlotCount;

            StructuredBuffer<float4x4> _AgentMatrices;
            StructuredBuffer<float4> _SlotUVRects;
            StructuredBuffer<float4x4> _SlotSpriteMatrices;
            StructuredBuffer<int> _AnimSlotIndices;

            Varyings Vert(Attributes v, uint instanceID : SV_InstanceID)
            {
                Varyings o;

                uint localSlot = instanceID % (uint)_BatchSlotCount;
                uint agentIndex = instanceID / (uint)_BatchSlotCount;

                int animSlotIndex = _AnimSlotIndices[localSlot];
                float frameIndex = _AnimFrame;
                float texWidth = _AnimSlotCount * 3.0;
                float y = (frameIndex + 0.5) / _AnimTexHeight;
                float x0 = (animSlotIndex + 0.5) / texWidth;
                float x1 = (_AnimSlotCount + animSlotIndex + 0.5) / texWidth;

                float4 animData0 = SAMPLE_TEXTURE2D_LOD(_AnimTex, sampler_AnimTex, float2(x0, y), 0);
                float4 animData1 = SAMPLE_TEXTURE2D_LOD(_AnimTex, sampler_AnimTex, float2(x1, y), 0);

                float2 posOffset = animData0.xy;
                float2 scale = animData0.zw;
                float sinRot = animData1.x;
                float cosRot = animData1.y;

                float4x4 slotMatrix = float4x4(
                    cosRot * scale.x, -sinRot * scale.y, 0, posOffset.x,
                    sinRot * scale.x, cosRot * scale.y, 0, posOffset.y,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                );

                float4 localPos = mul(_SlotSpriteMatrices[localSlot], float4(v.positionOS, 1));
                localPos = mul(slotMatrix, localPos);
                float3 worldPos = mul(_AgentMatrices[agentIndex], localPos).xyz;

                o.positionCS = TransformWorldToHClip(worldPos);

                float4 uvRect = _SlotUVRects[localSlot];
                o.uv = float2(lerp(uvRect.x, uvRect.z, v.uv.x), lerp(uvRect.y, uvRect.w, v.uv.y));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_AnimTex);
            SAMPLER(sampler_AnimTex);

            float4 _Color;
            float _AnimSlotCount;
            float _AnimTexHeight;
            float _AnimFrame;
            int _BatchSlotCount;

            StructuredBuffer<float4x4> _AgentMatrices;
            StructuredBuffer<float4> _SlotUVRects;
            StructuredBuffer<float4x4> _SlotSpriteMatrices;
            StructuredBuffer<int> _AnimSlotIndices;

            Varyings Vert(Attributes v, uint instanceID : SV_InstanceID)
            {
                Varyings o;

                uint localSlot = instanceID % (uint)_BatchSlotCount;
                uint agentIndex = instanceID / (uint)_BatchSlotCount;

                int animSlotIndex = _AnimSlotIndices[localSlot];
                float frameIndex = _AnimFrame;
                float texWidth = _AnimSlotCount * 3.0;
                float y = (frameIndex + 0.5) / _AnimTexHeight;
                float x0 = (animSlotIndex + 0.5) / texWidth;
                float x1 = (_AnimSlotCount + animSlotIndex + 0.5) / texWidth;

                float4 animData0 = SAMPLE_TEXTURE2D_LOD(_AnimTex, sampler_AnimTex, float2(x0, y), 0);
                float4 animData1 = SAMPLE_TEXTURE2D_LOD(_AnimTex, sampler_AnimTex, float2(x1, y), 0);

                float2 posOffset = animData0.xy;
                float2 scale = animData0.zw;
                float sinRot = animData1.x;
                float cosRot = animData1.y;

                float4x4 slotMatrix = float4x4(
                    cosRot * scale.x, -sinRot * scale.y, 0, posOffset.x,
                    sinRot * scale.x, cosRot * scale.y, 0, posOffset.y,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                );

                float4 localPos = mul(_SlotSpriteMatrices[localSlot], float4(v.positionOS, 1));
                localPos = mul(slotMatrix, localPos);
                float3 worldPos = mul(_AgentMatrices[agentIndex], localPos).xyz;

                o.positionCS = TransformWorldToHClip(worldPos);

                float4 uvRect = _SlotUVRects[localSlot];
                o.uv = float2(lerp(uvRect.x, uvRect.z, v.uv.x), lerp(uvRect.y, uvRect.w, v.uv.y));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                return c;
            }
            ENDHLSL
        }
    }
}
