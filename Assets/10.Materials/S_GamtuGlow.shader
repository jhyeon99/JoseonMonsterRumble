Shader "Team06/GamtuGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GlowColor ("Blue White Outline", Color) = (0.45, 1.25, 2, 1)
        _OutlinePixels ("Outline Width (Pixels)", Range(1, 12)) = 5
        _HaloPixels ("Halo Width (Pixels)", Range(2, 24)) = 12
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.45
        _PulseBoost ("Pulse Brightness", Range(0, 5)) = 2.4
        [HideInInspector] _Pulse ("Pulse", Float) = 0
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex GlowVertex
            #pragma fragment GlowFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _OutlinePixels, _HaloPixels, _GlowStrength, _PulseBoost, _Pulse;
                float4 _SpriteUVRect;
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

            half AlphaAt(float2 uv)
            {
                float inside = step(_SpriteUVRect.x, uv.x) * step(_SpriteUVRect.y, uv.y)
                    * step(uv.x, _SpriteUVRect.z) * step(uv.y, _SpriteUVRect.w);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * inside;
            }

            float RingAlpha(float2 uv, float pixels, bool minimum)
            {
                float2 d = _MainTex_TexelSize.xy * pixels;
                float samples[8];
                samples[0] = AlphaAt(uv + float2(d.x, 0));
                samples[1] = AlphaAt(uv - float2(d.x, 0));
                samples[2] = AlphaAt(uv + float2(0, d.y));
                samples[3] = AlphaAt(uv - float2(0, d.y));
                samples[4] = AlphaAt(uv + d * 0.7071);
                samples[5] = AlphaAt(uv - d * 0.7071);
                samples[6] = AlphaAt(uv + float2(d.x, -d.y) * 0.7071);
                samples[7] = AlphaAt(uv + float2(-d.x, d.y) * 0.7071);
                float result = minimum ? 1.0 : 0.0;
                [unroll] for (int i = 0; i < 8; i++)
                    result = minimum ? min(result, samples[i]) : max(result, samples[i]);
                return result;
            }

            half4 GlowFragment(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float outer = saturate(RingAlpha(input.uv, _OutlinePixels, false) - sprite.a);
                float inner = saturate(sprite.a - RingAlpha(input.uv, _OutlinePixels, true));
                float halo = saturate(RingAlpha(input.uv, _HaloPixels, false) - sprite.a);
                float glow = _GlowStrength * (1.0 + _Pulse * _PulseBoost);
                float edgeAlpha = saturate(outer * 0.7 + halo * 0.12) * _GlowColor.a;
                float alpha = saturate(sprite.a + edgeAlpha) * input.color.a;
                half3 baseColor = sprite.rgb * sprite.a * input.color.rgb;
                baseColor *= 1.0 + _Pulse * 0.12;
                half3 outline = _GlowColor.rgb * (outer * 0.7 + inner * 0.3 + halo * 0.12) * glow;
                return half4((baseColor + outline) * input.color.a, alpha);
            }
            ENDHLSL
        }
    }
}
