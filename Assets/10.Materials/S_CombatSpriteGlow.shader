Shader "Team06/Combat/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Light Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tone", Color) = (0.84,0.94,1.03,1)
        _HighlightTint ("Highlight Tone", Color) = (1.03,1.0,0.93,1)
        _ToneBlend ("Tone Blend", Range(0,1)) = 0.25
        [Toggle] _RemoveBlueBackdrop ("Remove Blue Backdrop", Float) = 0
        _KeyStart ("Blue Key Start", Range(0,1)) = 0.05
        _KeyEnd ("Blue Key End", Range(0,1)) = 0.15
        _EmissionStrength ("Emission Strength", Range(0,8)) = 0
        _EmissionMode ("Emission Mode: Bright / RedBlue / Flame / Poison", Float) = 0
        _EmissionThreshold ("Emission Threshold", Range(0,1)) = 0.35
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0
    }
    HLSLINCLUDE
    half GetBackdropCoverage(half3 rgb, float removeBackdrop, float keyStart, float keyEnd)
    {
        half blueExcess = rgb.b - max(rgb.r, rgb.g);
        return lerp(1.0h, 1.0h - smoothstep(keyStart, max(keyEnd, keyStart + 0.001), blueExcess), removeBackdrop);
    }
    half3 ApplyAtmosphereTone(half3 rgb, half3 shadowTint, half3 highlightTint, float blend)
    {
        half luminance = dot(rgb, half3(0.2126,0.7152,0.0722));
        half3 tone = lerp(shadowTint, highlightTint, smoothstep(0.12h,0.85h,luminance));
        return rgb * lerp(half3(1,1,1), tone, blend);
    }
    half3 GetCombatEmission(half3 rgb, float mode, float threshold, float strength)
    {
        [branch] if (strength <= 0.0) return half3(0, 0, 0);
        half peak = max(rgb.r, max(rgb.g, rgb.b));
        half selected = smoothstep(min(threshold, 0.999), min(threshold + 0.2, 1.0), peak);
        if (mode > 0.5 && mode < 1.5)
        {
            half red = smoothstep(0.25h, 0.5h, rgb.r - max(rgb.g, rgb.b));
            half blue = smoothstep(0.18h, 0.4h, rgb.b - max(rgb.r, rgb.g));
            selected *= max(red, blue);
        }
        else if (mode > 1.5 && mode < 2.5)
            selected *= smoothstep(0.5h, 0.7h, rgb.r - rgb.g);
        else if (mode > 2.5)
            selected *= smoothstep(0.03h, 0.15h, rgb.g - rgb.b);
        return rgb * selected * strength;
    }
    ENDHLSL
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadowTint;
                half4 _HighlightTint;
                float _ToneBlend;
                float _RemoveBlueBackdrop;
                float _KeyStart;
                float _KeyEnd;
                float _EmissionStrength;
                float _EmissionMode;
                float _EmissionThreshold;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonLitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half coverage = GetBackdropCoverage(texel.rgb, _RemoveBlueBackdrop, _KeyStart, _KeyEnd);
                half4 main = texel * input.color;
                main.rgb = ApplyAtmosphereTone(main.rgb, _ShadowTint.rgb, _HighlightTint.rgb, _ToneBlend);
                main.a *= coverage;
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                SurfaceData2D surface;
                InputData2D data;
                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surface);
                InitializeInputData(input.uv, input.lightingUV, data);
                #if defined(DEBUG_DISPLAY)
                    SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(data, input.positionWS, input.positionCS, _MainTex);
                    surface.normalWS = input.normalWS;
                #endif
                half4 output = CombinedShapeLightShared(surface, data);
                output.rgb += GetCombatEmission(texel.rgb, _EmissionMode, _EmissionThreshold, _EmissionStrength) * input.color.rgb;
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            struct Attributes { COMMON_2D_NORMALS_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadowTint;
                half4 _HighlightTint;
                float _ToneBlend;
                float _RemoveBlueBackdrop;
                float _KeyStart;
                float _KeyEnd;
                float _EmissionStrength;
                float _EmissionMode;
                float _EmissionThreshold;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonNormalsVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 main = texel * input.color;
                main.a *= GetBackdropCoverage(texel.rgb, _RemoveBlueBackdrop, _KeyStart, _KeyEnd);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                return NormalsRenderingShared(main, normalTS, input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
            }
            ENDHLSL
        }
    }
}
