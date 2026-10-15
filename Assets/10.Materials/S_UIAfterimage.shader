Shader "Team06/UIAfterimage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.5, 0.8, 1, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.2
        _Brightness ("Brightness", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 position : TEXCOORD1;
            };
            sampler2D _MainTex;
            float4 _ClipRect;
            half4 _TintColor;
            half _TintStrength;
            half _Brightness;
            v2f vert(appdata input)
            {
                v2f output;
                output.position = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            half4 frag(v2f input) : SV_Target
            {
                half4 sprite = tex2D(_MainTex, input.uv);
                const half3 luminanceWeights = half3(0.2126, 0.7152, 0.0722);
                const half minimumLuminance = 0.001;
                half luminance = dot(sprite.rgb, luminanceWeights);
                half tintLuminance = max(dot(_TintColor.rgb, luminanceWeights), minimumLuminance);
                half3 tintedColor = _TintColor.rgb * (luminance / tintLuminance);
                half3 color = lerp(sprite.rgb, tintedColor, _TintStrength) * _Brightness;
                half alpha = sprite.a * input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.position.xy, _ClipRect);
                #endif
                return half4(color * input.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
