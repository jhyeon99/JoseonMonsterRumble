Shader "Team06/ProjectileGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 0.85, 0.4, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.45
        _Brightness ("Brightness", Range(1, 3)) = 1.5
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
            #pragma vertex GlowVertex
            #pragma fragment GlowFragment
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
                half4 _TintColor;
                half _TintStrength;
                half _Brightness;
            CBUFFER_END

            Varyings GlowVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * unity_SpriteColor;
                return output;
            }

            half4 GlowFragment(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Preserve the texture's shading when shifting its hue.
                const half3 luminanceWeights = half3(0.2126, 0.7152, 0.0722);
                const half minimumLuminance = 0.001;
                half luminance = dot(sprite.rgb, luminanceWeights);
                half tintLuminance = max(dot(_TintColor.rgb, luminanceWeights), minimumLuminance);
                half3 tintedColor = _TintColor.rgb * (luminance / tintLuminance);
                half3 color = lerp(sprite.rgb, tintedColor, _TintStrength) * _Brightness;
                return half4(color * input.color.rgb, sprite.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
