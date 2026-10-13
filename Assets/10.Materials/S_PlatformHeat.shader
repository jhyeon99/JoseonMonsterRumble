Shader "Team06/PlatformHeat"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex HeatVertex
            #pragma fragment HeatFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            Varyings HeatVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 HeatFragment(Varyings input) : SV_Target
            {
                float bend = sin(input.uv.y * 7.0 - _Time.y * 3.0) * 0.12;
                float2 offset = float2(input.uv.x - 0.5 + bend, input.uv.y - 0.5);
                float softness = 1.0 - smoothstep(0.08, 0.5, length(offset));
                return half4(input.color.rgb, input.color.a * softness);
            }
            ENDHLSL
        }
    }
}
