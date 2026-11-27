Shader "IronKnuckle/StylizedRobotToon"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.2, 0.55, 0.95, 1)
        _ShadeTint ("Shadow Tint", Color) = (0.42, 0.48, 0.66, 1)
        _Steps ("Shading Steps", Range(1, 5)) = 3

        [Header(Normal Map)]
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0.0, 2.0)) = 0.85

        [Header(Stylized Specular and Rim)]
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecSmoothness ("Specular Threshold", Range(0.45, 0.99)) = 0.86
        _MetallicSpec ("Specular Boost", Range(0.0, 2.5)) = 0.75
        _RimColor ("Rim Color", Color) = (0.4, 0.9, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0.0, 2.0)) = 0.75

        [Header(Emission and Impact Flash)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _HitFlash ("Hit Flash", Range(0, 1)) = 0.0

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.04, 0.05, 0.08, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.04)) = 0.011
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // =====================================================================
        // PASS 1: INVERTED HULL OUTLINE (respects global _GlobalOutlineScale)
        // =====================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _DisableOutline;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                half4 _BaseColor;
                half4 _ShadeTint;
                float _Steps;
                float _BumpScale;
                half4 _SpecColor;
                float _SpecSmoothness;
                float _MetallicSpec;
                half4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                half4 _EmissionColor;
                float _HitFlash;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                // Default to 1 if _GlobalOutlineScale has not been explicitly set yet (note: Unity defaults unset floats to 0, so check <= 0.001? Wait: GraphicsSettingsManager sets it to 1, and in EditMode/uninitialized we use _OutlineWidth unless explicitly disabled by negative or flag! Let's use _OutlineDisabled global float: 0 = enabled, 1 = disabled!)
                float outlineEnabled = 1.0 - step(0.5, _DisableOutline);
                float dist = distance(_WorldSpaceCameraPos, vertexInput.positionWS);
                float width = _OutlineWidth * clamp(dist * 0.16, 0.65, 1.8) * outlineEnabled;
                float3 extrudedWS = vertexInput.positionWS + normalInput.normalWS * width;
                output.positionCS = TransformWorldToHClip(extrudedWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 2: STYLIZED TOON + NORMAL MAP + HDR EMISSION FORWARD PASS
        // =====================================================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                half4 _BaseColor;
                half4 _ShadeTint;
                float _Steps;
                float _BumpScale;
                half4 _SpecColor;
                float _SpecSmoothness;
                float _MetallicSpec;
                half4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                half4 _EmissionColor;
                float _HitFlash;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 tangentWS   : TEXCOORD2;
                float2 uv          : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(normalInput.tangentWS, sign);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                half4 packedNormal = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(packedNormal, _BumpScale);

                float3 geomN = normalize(input.normalWS);
                float3 T = normalize(input.tangentWS.xyz);
                float3 B = cross(geomN, T) * input.tangentWS.w;
                float3 N = normalize(TransformTangentToWorld(normalTS, half3x3(T, B, geomN)));
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Half-Lambert quantized cel-shading
                float ndotl = dot(N, mainLight.direction) * 0.5 + 0.5;
                ndotl *= lerp(0.55, 1.0, mainLight.shadowAttenuation);

                float stepsSafe = max(_Steps, 2.0);
                float band = saturate(floor(ndotl * stepsSafe) / (stepsSafe - 1.0));

                half3 shaded = albedo * _ShadeTint.rgb;
                half3 litColor = lerp(shaded, albedo, band) * mainLight.color;

                // Crisp stylized metallic specular band
                float3 H = normalize(mainLight.direction + V);
                float ndoth = saturate(dot(N, H));
                float specBand = smoothstep(_SpecSmoothness, _SpecSmoothness + 0.03, ndoth) * band;
                half3 spec = specBand * _SpecColor.rgb * _MetallicSpec;

                // Lit-side rim light
                float rimFactor = 1.0 - saturate(dot(N, V));
                float rim = pow(rimFactor, _RimPower) * max(band, 0.25);
                half3 rimCol = rim * _RimColor.rgb * _RimIntensity;

                // Ambient fill
                half3 ambient = SampleSH(N) * albedo * 0.35;

                half3 finalRgb = litColor + spec + rimCol + ambient + _EmissionColor.rgb;
                finalRgb = lerp(finalRgb, half3(1.0, 0.96, 0.85), saturate(_HitFlash));

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 3: SHADOW CASTER
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
