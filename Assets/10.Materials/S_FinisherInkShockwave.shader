Shader "Team06/FinisherInkShockwave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BrushTex ("Grunge Mask", 2D) = "black" {}
        _CenterAspect ("Center and Aspect", Vector) = (0.5, 0.5, 1, 0)
        _Burst ("Expansion, Opening, Ink Opacity, Progress", Vector) = (0, 0, 0, 0)
        _ScreenFade ("Monochrome Strength", Range(0, 1)) = 0
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
            CBUFFER_END
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
                output.viewportUV = input.positionOS.xy + 0.5;
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
                // Preserve luminance. No white replacement, inversion or blurred samples.
                half3 monochrome = lerp(scene, luminance.xxx, _ScreenFade);
                half3 result = monochrome * (1.0 - ink);
                return half4(result, outer * max(_ScreenFade, _Burst.z));
            }
            ENDHLSL
        }
    }
}
