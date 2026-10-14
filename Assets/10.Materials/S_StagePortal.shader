Shader "Team06/StagePortal"
{
    Properties
    {
        [HDR] _Tint ("Silver Blue Light", Color) = (0.68, 0.8, 1, 1)
        [HDR] _CoreColor ("White Core", Color) = (1.3, 1.4, 1.6, 1)
        _Intensity ("Brightness", Range(0, 4)) = 1.2
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Speed ("Flow Speed", Range(0, 3)) = 0.65
        _Width ("Column Width", Range(0.12, 0.6)) = 0.34
        _BeamDensity ("Vertical Beam Density", Range(15, 100)) = 65
        _Sparkle ("Sparkle Brightness", Range(0, 2)) = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            Name "StagePortal"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _CoreColor;
                float _Intensity, _Opacity, _Speed, _Width, _BeamDensity, _Sparkle;
            CBUFFER_END

            float Hash(float value) { return frac(sin(value * 127.1) * 43758.5453); }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float x = (uv.x - 0.5) * 2.0;
                float y = uv.y;
                float time = _Time.y * _Speed;
                float aa = max(fwidth(x), 0.001);

                // A translucent column with a rounded, softly dissolving crown.
                float cap = 0.77 + 0.13 * sqrt(saturate(1.0 - x * x / (_Width * _Width)));
                float column = (1.0 - smoothstep(_Width * 0.7, _Width + 0.09, abs(x)));
                column *= smoothstep(0.09, 0.16, y) * (1.0 - smoothstep(cap - 0.16, cap + 0.025, y));
                float haze = exp(-x * x / (_Width * _Width) * 3.0) * column;

                // Independently flowing thin strands; the gaps retain the background.
                float lane = (x + 1.0) * _BeamDensity;
                float id = floor(lane);
                float seed = Hash(id);
                float strandDistance = abs(frac(lane) - 0.5);
                float strandAA = max(fwidth(lane), 0.015);
                float strands = 1.0 - smoothstep(0.035, 0.035 + strandAA, strandDistance);
                float flowing = pow(saturate(0.5 + 0.5 * sin(y * (14.0 + seed * 25.0) - time * (2.0 + seed * 3.0) + seed * 40.0)), 3.0);
                float beams = strands * (0.12 + flowing * 0.6) * column;

                // Flattened ellipse at ground level, with a broad surrounding glow.
                float2 ground = float2(x / 0.69, (y - 0.105) / 0.036);
                float radius = length(ground);
                float ringAA = max(fwidth(radius), 0.02);
                float ring = 1.0 - smoothstep(0.045, 0.045 + ringAA, abs(radius - 0.82));
                float baseGlow = exp(-x * x * 7.0 - pow((y - 0.105) / 0.045, 2.0));
                float baseCore = exp(-x * x * 11.0 - pow((y - 0.105) / 0.012, 2.0));

                // Curved rays opening out from the base like the reference's light fan.
                float h = max(y - 0.105, 0.0);
                float fanWidth = 0.63 + h * 1.8;
                float fanLane = x / fanWidth * 48.0;
                float fanLine = pow(saturate(0.5 + 0.5 * cos(fanLane * 6.283185)), 14.0);
                float fan = fanLine * exp(-h * 22.0) * smoothstep(0.095, 0.115, y);
                fan *= 1.0 - smoothstep(0.48, 0.85, abs(x));
                fan *= 0.6 + 0.4 * sin(time * 2.0 + floor(fanLane) * 2.7);

                // Sparse, rising four-point glints, all generated without textures.
                float sparkles = 0.0;
                [unroll] for (int i = 0; i < 10; i++)
                {
                    float seedX = Hash(i + 71.0);
                    float phase = frac(time * (0.045 + Hash(i + 22.0) * 0.05) + Hash(i + 9.0));
                    float2 center = float2((seedX - 0.5) * 1.05, 0.17 + phase * 0.68);
                    float2 delta = abs(float2(x, y) - center);
                    float size = lerp(0.012, 0.028, Hash(i + 39.0));
                    float vertical = (1.0 - smoothstep(aa, aa * 2.0, delta.x)) * saturate(1.0 - delta.y / size);
                    float horizontal = (1.0 - smoothstep(aa * 0.5, aa, delta.y)) * saturate(1.0 - delta.x / (size * 1.4));
                    float blink = pow(saturate(sin(time * 2.0 + i * 2.3)), 6.0);
                    sparkles += max(vertical, horizontal) * blink * sin(phase * 3.141593);
                }

                float pulse = 0.94 + 0.06 * sin(time * 2.0);
                float frame = smoothstep(0.0, 0.025, y) * (1.0 - smoothstep(0.93, 1.0, y));
                frame *= 1.0 - smoothstep(0.90, 0.99, abs(x));
                half3 light = _Tint.rgb * (haze * 0.16 + beams * 0.55 + baseGlow * 0.5 + fan * 0.65);
                light += _CoreColor.rgb * (ring * 0.55 + baseCore * 0.9 + sparkles * _Sparkle);
                light *= _Intensity * _Opacity * _Tint.a * pulse * frame;
                return half4(light, 0);
            }
            ENDHLSL
        }
    }
}
