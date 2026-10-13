Shader "Team06/BlueGuideFlame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _OuterColor ("Outer Blue", Color) = (0.05, 0.32, 1.4, 1)
        [HDR] _InnerColor ("Inner Cyan", Color) = (0.35, 1.4, 2.0, 1)
        [HDR] _CoreColor ("White Core", Color) = (1.6, 2.0, 2.4, 1)
        _Intensity ("Brightness", Range(0, 4)) = 1.2
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Glow ("Soft Halo", Range(0, 1)) = 0.3
        _Speed ("Flame Speed", Range(0, 5)) = 1.4
        _Sway ("Flame Sway", Range(0, 0.25)) = 0.08
        _WispCurl ("Ghost Tail Curl", Range(0, 0.4)) = 0.22
        _Swirl ("Inner Flame Swirl", Range(0, 1)) = 0.65
        _HoverAmplitude ("Hover Height", Range(0, 0.15)) = 0.04
        _HoverSpeed ("Hover Speed", Range(0, 5)) = 1.6
        _Phase ("Animation Phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "BlueGuideFlame"
            HLSLPROGRAM
            #pragma vertex FlameVertex
            #pragma fragment FlameFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float phase : TEXCOORD1;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OuterColor;
                half4 _InnerColor;
                half4 _CoreColor;
                float _Intensity, _Opacity, _Glow, _Speed, _Sway;
                float _HoverAmplitude, _HoverSpeed, _Phase;
                float _WispCurl, _Swirl;
            CBUFFER_END

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            float SpiritDistance(float2 p, float time, float noise)
            {
                float2 bulb = p - float2(sin(time * 0.8) * 0.025, 0.33);
                float bulbDistance = (length(bulb / float2(0.30, 0.17)) - 1.0) * 0.17;
                bulbDistance += sin(atan2(bulb.y, bulb.x) * 5.0 + time * 1.4) * 0.008;

                // 둥근 혼불에서 길게 휘어지는 중심 꼬리와 양쪽 작은 불길.
                float h = saturate((p.y - 0.32) / 0.58);
                float center = sin(h * 5.5 - time * 1.3) * _Sway * h;
                center += sin(time * 0.7 + h * 3.0) * _WispCurl * h * h;
                center += (noise - 0.5) * 0.055 * h;
                float width = lerp(0.15, 0.005, pow(h, 0.85));
                float mainTail = max(abs(p.x - center) - width, max(0.28 - p.y, p.y - 0.9));

                float sideHeight = saturate((p.y - 0.34) / 0.4);
                float sideWidth = 0.07 * pow(1.0 - sideHeight, 1.2) + 0.003;
                float sideCurve = 0.13 + sin(sideHeight * 3.14159) * 0.16;
                sideCurve += sin(time * 1.1 + sideHeight * 7.0) * 0.025 * sideHeight;
                float leftTail = max(abs(p.x + sideCurve) - sideWidth,
                    max(0.34 - p.y, p.y - (0.74 + sin(time) * 0.025)));
                float rightTail = max(abs(p.x - sideCurve * 0.9) - sideWidth,
                    max(0.34 - p.y, p.y - (0.68 + cos(time * 1.2) * 0.03)));

                float distance = min(bulbDistance, min(mainTail, min(leftTail, rightTail)));
                return distance + (noise - 0.5) * 0.03;
            }

            Varyings FlameVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                float2 anchor = TransformObjectToWorld(float3(0, 0, 0)).xy;
                float phase = dot(anchor, float2(2.31, 4.17)) + _Phase;
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                input.positionOS.y += sin(_Time.y * _HoverSpeed + phase) * _HoverAmplitude;
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * unity_SpriteColor;
                output.phase = phase;
                return output;
            }

            half4 FlameFragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _Speed + input.phase;
                float2 p = float2((uv.x - 0.5) * 2.0, uv.y);
                float flow = Noise(float2(p.x * 9.0, p.y * 10.0 - time * 1.8));
                float distance = SpiritDistance(p, time, flow);
                float edgeSoftness = max(0.012, fwidth(distance) * 1.5);
                float body = 1.0 - smoothstep(-edgeSoftness, edgeSoftness, distance);
                float rim = exp(-abs(distance + 0.01) * 45.0) * body;

                // 속이 한 덩어리로 빛나지 않도록 나선형 불결과 투명한 틈을 만든다.
                float2 coreOffset = p - float2(0, 0.33);
                float coreRadius = length(coreOffset / float2(0.30, 0.17));
                float angle = atan2(coreOffset.y, coreOffset.x);
                float spiral = sin(coreRadius * 15.0 - angle * 2.0 - time * 2.0 + (flow - 0.5) * 4.0);
                float filaments = pow(saturate(spiral * 0.5 + 0.5), 5.0);
                filaments *= 1.0 - smoothstep(0.65, 1.0, coreRadius);
                float inner = saturate(-distance * 9.0);
                float core = exp(-coreRadius * coreRadius * 7.0);
                float halo = exp(-max(0.0, distance) * 24.0) * _Glow;
                float pulse = 0.92 + 0.08 * sin(time * 2.4);
                half3 color = lerp(_OuterColor.rgb, _InnerColor.rgb, saturate(inner * 0.55 + rim));
                color = lerp(color, _CoreColor.rgb, saturate(core * 0.6 + filaments * _Swirl));
                color *= _Intensity * pulse * input.color.rgb;
                float frameMask = smoothstep(0.0, 0.04, uv.x) * (1.0 - smoothstep(0.96, 1.0, uv.x));
                frameMask *= smoothstep(0.02, 0.08, uv.y) * (1.0 - smoothstep(0.93, 0.99, uv.y));
                float density = lerp(1.0, 0.45 + rim * 0.35 + filaments * 0.45 + core * 0.2, _Swirl);
                float alpha = saturate(body * density + halo) * frameMask * _Opacity * input.color.a * _OuterColor.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
