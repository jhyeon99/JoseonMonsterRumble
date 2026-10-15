Shader "Team06/PoisonStream"
{
    Properties
    {
        _BodyColor ("Liquid", Color) = (0.29, 0.48, 0.10, 1)
        _LightColor ("Liquid Highlight", Color) = (0.68, 0.78, 0.23, 1)
        _EdgeColor ("Ink Edge", Color) = (0.07, 0.13, 0.045, 1)
        _BrushTex ("Brush Texture", 2D) = "white" {}
        _BrushStrength ("Brush Strength", Range(0, 1)) = 0.35
        _EdgeStrength ("Ink Edge Strength", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex StreamVertex
            #pragma fragment StreamFragment
            #include "UnityCG.cginc"

            float4 _BodyColor;
            float4 _LightColor;
            float4 _EdgeColor;
            sampler2D _BrushTex;
            float _BrushStrength;
            float _EdgeStrength;
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 grid = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(grid), Hash(grid + float2(1, 0)), f.x),
                    lerp(Hash(grid + float2(0, 1)), Hash(grid + float2(1, 1)), f.x), f.y);
            }
            Varyings StreamVertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }
            float4 StreamFragment(Varyings input) : SV_Target
            {
                float across = abs(input.uv.y * 2.0 - 1.0);
                float grain = Noise(input.uv * float2(9.0, 21.0));
                // Mirrored sampling keeps existing clamped Art textures unchanged.
                float2 brushUV = 1.0 - abs(frac(input.uv * float2(0.32, 0.7)) * 2.0 - 1.0);
                float brush = tex2D(_BrushTex, brushUV).a;
                float edge = 0.97 - 0.06 * grain - 0.03 * (1.0 - brush);
                float feather = max(fwidth(across), 0.008);
                float alpha = 1.0 - smoothstep(edge - feather, edge + feather, across);
                float flow = Noise(input.uv * float2(1.6, 5.0));
                float highlight = smoothstep(0.45, 0.8, flow)
                    * (1.0 - smoothstep(0.15, 0.85, across));
                float3 body = lerp(_BodyColor.rgb, _LightColor.rgb, highlight * 0.45);
                body *= lerp(1.0, 0.65 + 0.35 * brush, _BrushStrength);
                body *= 0.94 + 0.08 * grain;
                float ink = smoothstep(edge - 0.09, edge - 0.025, across) * _EdgeStrength;
                return float4(lerp(body, _EdgeColor.rgb, ink), alpha * _BodyColor.a);
            }
            ENDHLSL
        }
    }
}
