Shader "Team06/FinisherInkShockwave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BrushTex ("Grunge Mask", 2D) = "black" {}
        _CenterAspect ("Center and Aspect", Vector) = (0.5, 0.5, 1, 0)
        _Burst ("Expansion, Opening, Ink Opacity, Progress", Vector) = (0, 0, 0, 0)
        _ScreenFade ("Monochrome Strength", Range(0, 1)) = 0
        _MonochromeThreshold ("Black / White Threshold", Range(0, 1)) = 0.45
        _MonochromeSoftness ("Black / White Transition", Range(0.001, 0.2)) = 0.025
        _RestorationProgress ("Inside-out Restoration", Range(0, 1)) = 0
        [HideInInspector] _SubjectPass ("Subject Pass", Float) = 0
        [HideInInspector] _SubjectColor ("Subject Colour", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex InkVertex
            #pragma fragment InkFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                float2 sceneUV : TEXCOORD1;
                float2 viewportUV : TEXCOORD4;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            TEXTURE2D(_BrushTex);
            SAMPLER(sampler_BrushTex);
            TEXTURE2D_X(_CameraSortingLayerTexture);
            CBUFFER_START(UnityPerMaterial)
                float4 _BrushTex_TexelSize;
                float4 _CenterAspect;
                float4 _Burst;
                float _ScreenFade;
                float _MonochromeThreshold;
                float _MonochromeSoftness;
                float _RestorationProgress;
                float _SubjectPass;
                float4 _SubjectColor;
            CBUFFER_END
            static const float RestorationEdgeWidth = 0.025;
            static const float RestorationGrungeWidth = 0.08;
            Varyings InkVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                // Match URP's Lit2DCommon screen-texture coordinates. Unlike
                // GetNormalizedScreenSpaceUV, this does not require _ScaleBiasRt.
                output.sceneUV = ComputeScreenPos(output.positionCS / output.positionCS.w).xy;
                // This renderer is a unit quad covering the orthographic viewport.
                output.viewportUV = _SubjectPass > 0.5 ? output.sceneUV : input.positionOS.xy + 0.5;
                return output;
            }
            float BrushMask(float2 offset, float radius)
            {
                float aspect = _BrushTex_TexelSize.z / max(_BrushTex_TexelSize.w, 1.0);
                float2 uv = offset / (2.0 * max(radius, 0.0001));
                uv.x /= aspect;
                uv += 0.5;
                // Outside the texture must be empty even when the importer uses clamp.
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, saturate(uv)).a * inside;
            }
            half4 InkFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.viewportUV;
                float2 offset = screenUV - _CenterAspect.xy;
                offset.x *= _CenterAspect.z;
                float outer = BrushMask(offset, _Burst.x);
                // The brush cuts open the centre; no procedural circular rim.
                float opening = BrushMask(offset, _Burst.x * _Burst.y * 0.86) * step(0.001, _Burst.y);
                float ink = saturate(outer - opening) * _Burst.z;
                half3 scene = SAMPLE_TEXTURE2D_X(_CameraSortingLayerTexture, sampler_PointClamp, input.sceneUV).rgb;
                half luminance = dot(scene, half3(0.2126, 0.7152, 0.0722));
                // Separate black and white instead of leaving the scene mid-grey.
                half contrast = smoothstep(_MonochromeThreshold - _MonochromeSoftness,
                    _MonochromeThreshold + _MonochromeSoftness, luminance);
                half3 monochrome = lerp(scene, contrast.xxx, _ScreenFade);
                half3 result = monochrome * (1.0 - ink);
                // Restore colour from the same origin, not with a global fade.
                // Keep the approved reference-camera restoration speed in world units.
                // Zero preserves compatibility with the material's default vector.
                float referenceScale = _CenterAspect.w > 0.0 ? _CenterAspect.w : 1.0;
                float2 referenceCenter = 0.5 + (_CenterAspect.xy - 0.5) / referenceScale;
                float2 farthestCorner = max(referenceCenter, 1.0 - referenceCenter);
                farthestCorner.x *= _CenterAspect.z;
                float edgeWidth = RestorationEdgeWidth * referenceScale;
                float grungeWidth = RestorationGrungeWidth * referenceScale;
                float maximumRadius = length(farthestCorner) * referenceScale + grungeWidth + edgeWidth;
                float restorationRadius = smoothstep(0.0, 1.0, _RestorationProgress) * maximumRadius;
                float grungeDistance = length(offset) + (outer - 0.5) * grungeWidth;
                float restored = (1.0 - smoothstep(restorationRadius - edgeWidth,
                    restorationRadius + edgeWidth, grungeDistance)) * step(0.0001, _RestorationProgress);
                if (_SubjectPass > 0.5)
                {
                    half4 subject = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _SubjectColor;
                    half subjectLuminance = dot(subject.rgb, half3(0.2126, 0.7152, 0.0722));
                    // Lift dark body details without a white outline or a flat silhouette.
                    half subjectTone = saturate((sqrt(saturate(subjectLuminance)) - 0.5) * 1.4 + 0.5);
                    return half4(subjectTone.xxx, subject.a * outer * _ScreenFade * (1.0 - restored));
                }
                return half4(result, outer * max(_ScreenFade, _Burst.z) * (1.0 - restored));
            }
            ENDHLSL
        }
    }
}
