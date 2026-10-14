Shader "Team06/Environment/BossRoomMist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Mist Color", Color) = (0.7,0.76,0.77,0.26)
        _Speed ("Drift Speed", Range(0,0.1)) = 0.008
        _Density ("Mist Density", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Speed;
                float _Density;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 worldXY : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.worldXY = world.xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                            lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.worldXY * float2(0.16,0.42) - float2(_Time.y * _Speed,0);
                float mist = Noise(p) * 0.65 + Noise(p * 2.1 + 17) * 0.35;
                float2 edge = min(input.uv, 1.0 - input.uv);
                float feather = smoothstep(0,0.18,edge.x) * smoothstep(0,0.3,edge.y);
                float alpha = smoothstep(0.18,0.82,mist) * _Density * feather * input.color.a;
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
