Shader "IronKnuckle/VisorEmissive"
{
    Properties
    {
        _BaseMap ("Visor Atlas", 2D) = "white" {}
        _Color ("LED Color", Color) = (0.15, 0.92, 1.0, 1)
        _BgColor ("Screen Background", Color) = (0.04, 0.06, 0.10, 1)
        _EmissionStrength ("Emission Strength", Range(0.5, 4.0)) = 1.8
        _AtlasOffsetScale ("Atlas ScaleXY OffsetZW", Vector) = (0.5, 0.333333, 0.0, 0.666667)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half4 _BgColor;
                float _EmissionStrength;
                float4 _AtlasOffsetScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _AtlasOffsetScale.xy + _AtlasOffsetScale.zw;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half mask = saturate((tex.r - 0.25) * 2.0);
                half3 col = lerp(_BgColor.rgb, _Color.rgb * _EmissionStrength, mask);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
