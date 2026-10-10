Shader "Team06/DokkebibulFlame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FlameColor ("Flame Outer Color", Color) = (1, 1, 1, 1)
        _FlameInnerColor ("Flame Inner Color", Color) = (1, 1, 1, 1)
        _RampStart ("Ramp Start", Range(0.0, 0.6)) = 0.25
        _RampEnd ("Ramp End", Range(0.4, 0.9)) = 0.78
        _CoreThreshold ("Core Threshold", Range(0.5, 0.98)) = 0.85
        _CoreSoftness ("Core Softness", Range(0.01, 0.2)) = 0.06
        _FlameIntensity ("Flame Intensity", Range(0.5, 2.0)) = 1.25
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex FlameVertex
            #pragma fragment FlameFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FlameColor;
                half4 _FlameInnerColor;
                half _RampStart;
                half _RampEnd;
                half _CoreThreshold;
                half _CoreSoftness;
                half _FlameIntensity;
            CBUFFER_END

            Varyings FlameVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * unity_SpriteColor;
                return output;
            }

            half4 FlameFragment(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                const half3 luminanceWeights = half3(0.2126, 0.7152, 0.0722);
                const half minimumLuminance = 0.001;
                half luminance = dot(sprite.rgb, luminanceWeights);

                // 한국 전통 단청 첩훈/운간채색 기법: 외곽 주색에서 내부 온기색으로 2단 램프 전이
                half ramp = smoothstep(_RampStart, _RampEnd, luminance);
                half3 flameRampColor = lerp(_FlameColor.rgb, _FlameInnerColor.rgb, ramp);
                half3 tintedFlame = flameRampColor * saturate(luminance * _FlameIntensity);

                half maxChannel = max(_FlameColor.r, max(_FlameColor.g, _FlameColor.b));
                half minChannel = min(_FlameColor.r, min(_FlameColor.g, _FlameColor.b));
                half saturation = saturate((maxChannel - minChannel) / max(maxChannel, minimumLuminance));
                half tintWeight = smoothstep(0.05, 0.35, saturation);
                half3 flameTarget = lerp(sprite.rgb, tintedFlame, tintWeight);

                // 고명도 얼굴/중심핵 보존 마스크 (얼굴 중심부만 순백색 유지)
                half minEdge = saturate(_CoreThreshold - _CoreSoftness);
                half maxEdge = saturate(_CoreThreshold + _CoreSoftness);
                half coreMask = smoothstep(minEdge, maxEdge, luminance);
                half3 baseColor = lerp(flameTarget, sprite.rgb, coreMask);

                // SpriteRenderer 색상(자폭 붉은 플래시 등) 변조 반영
                half3 finalRgb = baseColor * input.color.rgb;
                half finalAlpha = sprite.a * input.color.a * _FlameColor.a;

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
