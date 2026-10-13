Shader "Team06/PoisonBeam"
{
    Properties
    {
        _MainTex ("Large Brush", 2D) = "white" {}
        _DetailTex ("Flow Brush", 2D) = "white" {}
        _BodyColor ("Body", Color) = (0.31, 0.39, 0.14, 1)
        _LightColor ("Light Pigment", Color) = (0.60, 0.64, 0.32, 1)
        _EdgeColor ("Dark Pigment", Color) = (0.16, 0.19, 0.085, 1)
        _StreamLength ("Stream Length", Float) = 1
        _EndFrayLength ("End Fray Length", Float) = 0.55
        _EndImpact ("Terrain Contact", Float) = 0
        _MouthBlendLength ("Mouth Connection Length", Float) = 0.22
        _HasMouthConnection ("Attached To Mouth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex BeamVertex
            #pragma fragment BeamFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PoisonPaintSurface.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DetailTex);
            SAMPLER(sampler_DetailTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DetailTex_ST;
                float4 _BodyColor;
                float4 _LightColor;
                float4 _EdgeColor;
                float _StreamLength;
                float _EndFrayLength;
                float _EndImpact;
                float _MouthBlendLength;
                float _HasMouthConnection;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings BeamVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 BeamFragment(Varyings input) : SV_Target
            {
                float2 flowUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float2 brushUV = PoisonBrushUV(flowUV * float2(0.45, 1.4));
                float2 detailUV = PoisonBrushUV(flowUV * float2(0.9, 2.3) + float2(0.37, 0.21));
                float brush = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, brushUV).a;
                float detail = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, detailUV).a;
                float across = abs(input.uv.y * 2.0 - 1.0);
                // 외곽에만 거친 붓 경계를 적용한다. 물줄기의 중앙은 끊지 않는다.
                float rim = PoisonNoise(float2(flowUV.x * 7.0, input.uv.y * 3.0));
                float streakRim = PoisonNoise(float2(flowUV.x * 13.0, input.uv.y * 6.0));
                float edge = 0.86 + 0.09 * rim + 0.045 * streakRim - 0.035 * (1.0 - detail);
                float feather = max(fwidth(across), 0.008);
                float alpha = 1.0 - smoothstep(edge - feather, edge + feather, across);
                float startDistance = saturate(input.uv.x) * _StreamLength;
                float rootBlend = smoothstep(0.0, max(_MouthBlendLength, 0.001), startDistance);
                // 입 좌표는 유지한다. 바깥 모서리부터 늦게 나타나게 하여 평평한 시작 단면을 없앤다.
                float rootAlpha = smoothstep(-0.025, 0.035 + across * across * 0.09, startDistance);
                alpha *= lerp(1.0, rootAlpha, _HasMouthConnection);
                // 분사가 끝난 뒤 입에서 떨어져 나간 뒤쪽 단면도 갈라지며 가늘어진다.
                float tailLength = min(0.65, _StreamLength * 0.4);
                float tailNoise = PoisonNoise(float2(input.uv.y * 14.0, flowUV.x * 2.0));
                float tailCut = tailLength * (0.08 + 0.3 * tailNoise + 0.6 * across * across);
                float tailAlpha = smoothstep(tailCut, tailCut + max(fwidth(startDistance), 0.02), startDistance);
                alpha *= lerp(tailAlpha, 1.0, _HasMouthConnection);
                // 폭마다 다른 길이로 갈라지게 하여 끝이 일직선으로 잘리지 않게 한다.
                float endDistance = (1.0 - saturate(input.uv.x)) * _StreamLength;
                float frayLength = min(_EndFrayLength * lerp(1.0, 0.45, _EndImpact), _StreamLength * 0.35);
                float finger = PoisonNoise(float2(input.uv.y * 17.0, flowUV.x * 3.0));
                float cutBack = frayLength * (0.12 + 0.7 * finger + 0.18 * across);
                float tipFeather = max(fwidth(endDistance), 0.012);
                alpha *= smoothstep(cutBack, cutBack + tipFeather, endDistance);
                float3 color = EvaluatePoisonPaint(flowUV, brush, detail, across,
                    _BodyColor.rgb, _LightColor.rgb, _EdgeColor.rgb);
                color = lerp(_EdgeColor.rgb * 0.65, color, lerp(1.0, rootBlend, _HasMouthConnection));
                return half4(color * input.color.rgb, alpha * _BodyColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
