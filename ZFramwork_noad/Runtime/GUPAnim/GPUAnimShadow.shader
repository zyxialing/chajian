Shader "Custom/GPUAnim_Shadow_URP"
{
    Properties
    {
        _BaseSize ("Base Size", Vector) = (1,1,0,0)
        _CenterOffset ("Center Offset", Vector) = (0,0,0,0)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _ShadowEnabled ("Shadow Enabled", Float) = 1
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.35)
        _ShadowSize ("Shadow Size", Vector) = (44,14,0,0)
        _ShadowOffset ("Shadow Offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalRenderPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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

            float _AlphaCutoff;
            float _ShadowEnabled;
            half4 _ShadowColor;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseSize)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CenterOffset)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ShadowOffset)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ShadowSize)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 baseSize = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseSize);
                float4 centerOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _CenterOffset);
                float4 shadowOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _ShadowOffset);
                float4 shadowSizeData = UNITY_ACCESS_INSTANCED_PROP(Props, _ShadowSize);
                float2 anchorPixel = float2(baseSize.x * 0.5 + centerOffset.x, centerOffset.y);
                float2 shadowSize = max(shadowSizeData.xy, float2(1.0, 1.0));
                float2 shadowCenter = anchorPixel + shadowOffset.xy;
                float2 fullPixel = lerp(shadowCenter - shadowSize, shadowCenter + shadowSize, v.uv);
                float2 anchoredPosition = (fullPixel - anchorPixel) / max(baseSize.y, 1.0);

                o.fullPixel = fullPixel;
o.positionHCS = TransformObjectToHClip(
    float3(anchoredPosition.x, anchoredPosition.y, 0)
);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float4 baseSize = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseSize);
                float4 centerOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _CenterOffset);
                float4 shadowOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _ShadowOffset);
                float4 shadowSizeData = UNITY_ACCESS_INSTANCED_PROP(Props, _ShadowSize);
                float2 anchorPixel = float2(baseSize.x * 0.5 + centerOffset.x, centerOffset.y);
                float2 shadowCenter = anchorPixel + shadowOffset.xy;
                float2 shadowSize = max(shadowSizeData.xy, float2(1.0, 1.0));
                float2 shadowDelta = (i.fullPixel - shadowCenter) / shadowSize;
                half shadowAlpha = smoothstep(1.0, 0.25, dot(shadowDelta, shadowDelta)) * _ShadowColor.a * _ShadowEnabled;

                clip(shadowAlpha - _AlphaCutoff);
                return half4(_ShadowColor.rgb, shadowAlpha);
            }

            ENDHLSL
        }
    }
}
