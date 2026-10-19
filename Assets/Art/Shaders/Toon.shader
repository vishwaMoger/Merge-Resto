// Cute stylized lit shader for Merge Resto.
// Soft 2-band toon lighting with tinted shadows, rim light, emission,
// optional vertex sway (plants, awnings) and a 0-1 "wear" value that
// drives the run-down look (Covered/Broken/Worn) without extra textures.
//
// Kept to a handful of variants: web browsers compile every keyword combination on first use,
// and each big lit program takes over a second there. Only the lighting keywords the game uses
// (cascaded sun shadows, per-pixel lamps, soft shadows), each with an "off" variant so the build
// always keeps a working fallback; no fog or instancing variants; sway is a material switch.
Shader "MergeResto/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Toon Lighting)]
        _ShadeColor ("Shadow Tint", Color) = (0.70, 0.60, 0.86, 1)
        _ShadeThreshold ("Shadow Threshold", Range(-1, 1)) = 0.05
        _ShadeSoftness ("Band Softness", Range(0.001, 0.5)) = 0.06
        _MidTone ("Mid Band Strength", Range(0, 1)) = 0.25
        _ShadowStrength ("Cast Shadow Strength", Range(0, 1)) = 0.85

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1, 0.96, 0.88, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25
        _RimPower ("Rim Width", Range(0.5, 8)) = 3

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [Header(Wear  Run Down)]
        _Wear ("Wear", Range(0, 1)) = 0
        _DirtColor ("Dirt Color", Color) = (0.42, 0.34, 0.26, 1)
        _GrimeScale ("Grime Scale", Float) = 5

        [Header(Sway)]
        [ToggleUI] _Sway ("Enable Sway", Float) = 0
        _SwayStrength ("Sway Strength", Range(0, 0.3)) = 0.05
        _SwaySpeed ("Sway Speed", Range(0, 5)) = 1.5

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half _MidTone;
            half _ShadowStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half4 _EmissionColor;
            half _Wear;
            half4 _DirtColor;
            float _GrimeScale;
            half _Sway;
            half _SwayStrength;
            half _SwaySpeed;
        CBUFFER_END

        // Global scene ambient, set by ToonLookSettings. Defaults to black.
        half4 _ToonAmbient;

        float3 ApplySway(float3 positionOS)
        {
            float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
            float phase = (pivotWS.x + pivotWS.z) * 1.7;
            float bend = max(positionOS.y, 0) * _Sway;
            positionOS.x += sin(_Time.y * _SwaySpeed + phase) * _SwayStrength * bend;
            positionOS.z += cos(_Time.y * _SwaySpeed * 0.8 + phase) * _SwayStrength * 0.5 * bend;
            return positionOS;
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            // Soft shadow quality is picked at runtime (no LOW/MEDIUM/HIGH variants).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = ApplySway(input.positionOS.xyz);
                VertexPositionInputs pos = GetVertexPositionInputs(positionOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            // Cheap value noise for procedural grime.
            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                         lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                         lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half ToonBand(half ndotl, half atten)
            {
                // Lit/shade split, plus a soft mid band just inside the terminator.
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndotl);
                half mid = smoothstep(_ShadeThreshold + 0.25 - _ShadeSoftness, _ShadeThreshold + 0.25 + _ShadeSoftness, ndotl);
                lit *= lerp(1.0 - _MidTone, 1.0, mid);
                lit *= lerp(1.0, atten, _ShadowStrength);
                return lit;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Wear: faded colour, a light overall dirt tint, and soft grime patches
                // that spread as wear rises. Kept gentle so it reads as dusty, not spotted.
                float3 grimePos = input.positionWS * _GrimeScale;
                half grime = ValueNoise(grimePos) * 0.55 + ValueNoise(grimePos * 2.3 + 7.1) * 0.3 + ValueNoise(grimePos * 6.1) * 0.15;
                half grimeMask = smoothstep(0.75 - _Wear * 0.35, 0.95 - _Wear * 0.25, grime) * _Wear;
                half grey = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
                half3 baseRgb = lerp(albedo.rgb, grey.xxx * 0.92, _Wear * 0.55);
                half3 dirtRgb = baseRgb * lerp(1.0, _DirtColor.rgb * 1.6, 0.5);
                baseRgb = lerp(baseRgb, dirtRgb, _Wear * 0.6);
                baseRgb = lerp(baseRgb, baseRgb * _DirtColor.rgb * 1.3, grimeMask * 0.7);

                half3 shadeRgb = baseRgb * _ShadeColor.rgb;

                // Main light
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = dot(normalWS, mainLight.direction);
                half band = ToonBand(ndotl, mainLight.shadowAttenuation);
                half3 color = lerp(shadeRgb, baseRgb, band) * mainLight.color;

                // Additional lights (lamps, oven glow): same banding, added on top.
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);

                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    half b = ToonBand(dot(normalWS, light.direction), light.shadowAttenuation);
                    color += baseRgb * light.color * b * light.distanceAttenuation;
                }
                #endif

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    half b = ToonBand(dot(normalWS, light.direction), light.shadowAttenuation);
                    color += baseRgb * light.color * b * light.distanceAttenuation;
                LIGHT_LOOP_END
            #endif

                // Ambient fill and rim
                color += baseRgb * _ToonAmbient.rgb;
                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength * saturate(band + 0.3) * (1.0 - _Wear);

                color += _EmissionColor.rgb * (1.0 - _Wear * 0.8);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment NullFrag
            // Only the sun casts shadows (lamp shadows are off), so no punctual-light variant.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(ApplySway(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 NullFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(ApplySway(input.positionOS.xyz));
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        // Feeds the camera normals texture used by the outline post effect.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(ApplySway(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void NormalsFrag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
