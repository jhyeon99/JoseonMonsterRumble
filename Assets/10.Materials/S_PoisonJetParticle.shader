Shader "Team06/PoisonJetParticle"
{
    Properties
    {
        _BrushTex ("Large Brush", 2D) = "white" {}
        _DetailTex ("Flow Brush", 2D) = "white" {}
        _BodyColor ("Body", Color) = (0.31, 0.39, 0.14, 1)
        _LightColor ("Light Pigment", Color) = (0.60, 0.64, 0.32, 1)
        _EdgeColor ("Dark Pigment", Color) = (0.16, 0.19, 0.085, 1)
        _EmissionStrength ("Emission Strength", Range(0,8)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex JetVertex
            #pragma fragment JetFragment
            #include "UnityCG.cginc"
            #include "PoisonPaintSurface.hlsl"
            sampler2D _BrushTex;
            sampler2D _DetailTex;
            float4 _BodyColor, _LightColor, _EdgeColor;
            float _EmissionStrength;
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings JetVertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            float4 JetFragment(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float2 paintUV = input.uv * float2(1.2, 1.7);
                float brush = tex2D(_BrushTex, PoisonBrushUV(paintUV)).a;
                float detail = tex2D(_DetailTex, PoisonBrushUV(paintUV + float2(0.37, 0.21))).a;
                // 불규칙한 외곽을 사용하고 광택 있는 둥근 알갱이는 만들지 않는다.
                float radius = length(centered * float2(1.0, 0.86));
                float edge = 0.78 + 0.1 * detail + 0.07 * PoisonNoise(input.uv * 5.0);
                float feather = max(fwidth(radius), 0.025);
                float alpha = 1.0 - smoothstep(edge - feather, edge + feather, radius);
                float3 color = EvaluatePoisonPaint(paintUV, brush, detail, radius,
                    _BodyColor.rgb, _LightColor.rgb, _EdgeColor.rgb);
                color += EvaluatePoisonEmission(paintUV, brush, detail, radius, _LightColor.rgb, _EmissionStrength);
                return float4(color * input.color.rgb, alpha * _BodyColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
