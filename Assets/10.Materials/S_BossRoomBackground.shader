Shader "Team06/Environment/BossRoomBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Contrast ("Background Contrast", Range(0,2)) = 0.82
        _Saturation ("Background Saturation", Range(0,2)) = 0.9
        _MistColor ("Atmosphere Color", Color) = (0.66,0.71,0.72,1)
        _MistBlend ("Atmosphere Blend", Range(0,1)) = 0.14
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
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
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _MistColor;
                float _Contrast;
                float _Saturation;
                float _MistBlend;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(texel.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 color = lerp(luminance.xxx, texel.rgb, _Saturation);
                color = (color - 0.5h) * _Contrast + 0.5h;
                color = lerp(color, _MistColor.rgb, _MistBlend);
                return half4(saturate(color) * input.color.rgb, texel.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
