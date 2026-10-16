// Screen-space outline for the toon look, run by a Full Screen Pass Renderer Feature
// (requirements: Depth + Normal). Draws thin dark lines on silhouettes (depth
// curvature, so flat surfaces at any angle stay clean) and on creases (normals).
Shader "MergeResto/ToonOutline"
{
    Properties
    {
        _OutlineColor ("Line Color", Color) = (0.13, 0.09, 0.08, 1)
        _Thickness ("Thickness (px)", Range(0.5, 3)) = 1
        _DepthThreshold ("Depth Sensitivity", Range(0.001, 1)) = 0.04
        _NormalThreshold ("Crease Sensitivity", Range(0.01, 1)) = 0.35
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "ToonOutline"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _OutlineColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;
            half _Opacity;

            // Linear eye depth for both orthographic (iso camera) and perspective cameras.
            float EyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (unity_OrthoParams.w > 0.5)
                {
                #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                float2 o = _CameraDepthTexture_TexelSize.xy * _Thickness;
                float2 uvL = uv - float2(o.x, 0);
                float2 uvR = uv + float2(o.x, 0);
                float2 uvD = uv - float2(0, o.y);
                float2 uvU = uv + float2(0, o.y);

                // Depth: second derivative is ~0 on planes, large across silhouettes.
                float dC = EyeDepth(uv);
                float dL = EyeDepth(uvL);
                float dR = EyeDepth(uvR);
                float dD = EyeDepth(uvD);
                float dU = EyeDepth(uvU);
                float curvature = max(abs(dL + dR - 2.0 * dC), abs(dD + dU - 2.0 * dC));
                // Scale by distance so perspective cameras behave like the ortho one.
                float depthScale = (unity_OrthoParams.w > 0.5) ? 1.0 : max(dC * 0.1, 1.0);
                float depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 2.0, curvature / depthScale);

                // Normals: creases and inner edges.
                float3 nC = SampleSceneNormals(uv);
                float nDiff = 0;
                nDiff = max(nDiff, 1.0 - dot(nC, SampleSceneNormals(uvL)));
                nDiff = max(nDiff, 1.0 - dot(nC, SampleSceneNormals(uvR)));
                nDiff = max(nDiff, 1.0 - dot(nC, SampleSceneNormals(uvD)));
                nDiff = max(nDiff, 1.0 - dot(nC, SampleSceneNormals(uvU)));
                float normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 1.5, nDiff);

                // Keep the empty background clean except where it meets geometry.
                float far = _ProjectionParams.z * 0.999;
                float allSky = step(far, min(min(dL, dR), min(min(dD, dU), dC)));
                float edge = saturate(max(depthEdge, normalEdge)) * (1.0 - allSky);

                return half4(lerp(sceneColor.rgb, _OutlineColor.rgb, edge * _Opacity), sceneColor.a);
            }
            ENDHLSL
        }
    }
}
