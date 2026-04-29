Shader "Custom/GPUAnim_Instanced_URP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _UV ("UV", Vector) = (0,0,1,1)
        _OffsetFrame ("Offset Frame", Vector) = (0,0,1,1)
        _BaseSize ("Base Size", Vector) = (1,1,0,0)
        _CenterOffset ("Center Offset", Vector) = (0,0,0,0)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 fullPixel : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _AlphaCutoff;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UV)
                UNITY_DEFINE_INSTANCED_PROP(float4, _OffsetFrame)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseSize)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CenterOffset)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 baseSize = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseSize);
                float4 centerOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _CenterOffset);
                float2 anchorPixel = float2(baseSize.x * 0.5 + centerOffset.x, centerOffset.y);
                float2 fullPixel = v.uv * baseSize.xy;
                float2 anchoredPosition = (fullPixel - anchorPixel) / max(baseSize.y, 1.0);

                o.fullPixel = fullPixel;
                o.positionHCS = TransformObjectToHClip(float3(anchoredPosition.x, anchoredPosition.y, 0));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float4 uvRect = UNITY_ACCESS_INSTANCED_PROP(Props, _UV);
                float4 offsetFrame = UNITY_ACCESS_INSTANCED_PROP(Props, _OffsetFrame);
                float2 framePixel = i.fullPixel - offsetFrame.xy;

                if (framePixel.x < 0 || framePixel.y < 0 || framePixel.x > offsetFrame.z || framePixel.y > offsetFrame.w)
                {
                    discard;
                }

                float2 frameUV = framePixel / max(offsetFrame.zw, float2(1.0, 1.0));
                float2 atlasUV = uvRect.xy + frameUV * uvRect.zw;
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV);
                clip(color.a - _AlphaCutoff);
                return color;
            }

            ENDHLSL
        }
    }
}
