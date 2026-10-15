Shader "Team06/UI/DeathBackgroundInk"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BrushTex ("Grunge Brush", 2D) = "white" {}
        _GrungeTex ("Paper Grunge", 2D) = "black" {}
        _GrungeStrength ("Paper Grunge Strength", Range(0,0.3)) = 0.12
        _BrushScale ("Fixed Brush Size", Range(1,2)) = 1.35
        _SourcePaperColor ("Original Paper Colour", Color) = (0.941176,0.909804,0.870588,1)
        _PaperColor ("Paper Base Colour", Color) = (0.721569,0.733333,0.745098,1)
        _TitlePaperColor ("Title Paper Colour", Color) = (0.85098,0.858824,0.866667,1)
        _TitleArea ("Title Centre And Half Size", Vector) = (0.5,0.76,0.23,0.2)
        _InkContrast ("Ink Contrast", Range(1,2)) = 1.3
        _AccentStrength ("Original Colour Accents", Range(0,1)) = 1
        _AccentSaturation ("Accent Saturation", Range(1,2)) = 1.6
        _AccentBrightness ("Accent Brightness", Range(1,1.5)) = 1.2
        _VignetteStrength ("Background Vignette Strength", Range(0,0.8)) = 0.42
        _VignetteInnerRadius ("Vignette Clear Radius", Range(0,1)) = 0.45
        _VignetteOuterRadius ("Vignette Outer Radius", Range(1,2)) = 1.35
        _VignetteCenter ("Vignette Centre", Vector) = (0.5,0.56,0,0)
        _InitialVisibility ("Initial Illustration Strength", Range(0,1)) = 0.55
        _EdgeSoftness ("Ink Soak Softness", Range(0.1,0.5)) = 0.3
        _RevealProgress ("Spread Progress", Range(0,1)) = 1
        [HideInInspector] _BackgroundRect ("Local Rect", Vector) = (-960,-540,1920,1080)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
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
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex, _BrushTex, _GrungeTex;
            fixed4 _Color, _TextureSampleAdd, _SourcePaperColor, _PaperColor, _TitlePaperColor;
            float4 _ClipRect, _BackgroundRect, _TitleArea, _VignetteCenter;
            float _BrushScale, _InitialVisibility, _EdgeSoftness, _RevealProgress, _AccentStrength;
            float _GrungeStrength, _InkContrast, _AccentSaturation, _AccentBrightness;
            float _VignetteStrength, _VignetteInnerRadius, _VignetteOuterRadius;
            static const float MinimumSize = 0.0001;
            static const float SoakSampleOffset = 0.18;
            static const float BrushDetailWeight = 0.15;
            static const float AccentChromaLow = 0.12;
            static const float AccentChromaHigh = 0.25;
            static const float GrungePaperLow = 0.25;
            static const float GrungePaperHigh = 0.65;
            static const float TitleGrungeSuppression = 0.8;
            static const float GrungeSecondScale = 1.37;
            static const float2 GrungeSecondOffset = float2(0.23, 0.41);
            static const float PaperArrivalProgress = 0.45;
            static const float3 LuminanceWeights = float3(0.2126, 0.7152, 0.0722);
            float SampleBrushDensity(float2 uv)
            {
                // A fixed, broad average gives the dense brush a graded soak map,
                // instead of revealing its opaque centre in a single step.
                float density = 0.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        density += tex2D(_BrushTex, saturate(uv + float2(x, y) * SoakSampleOffset)).a;
                    }
                }
                return density / 9.0;
            }
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = (input.localPosition.xy - _BackgroundRect.xy) / max(_BackgroundRect.zw, MinimumSize);
                // The fixed brush changes illustration density, never draws a rim.
                float2 brushUV = (uv - 0.5) / _BrushScale + 0.5;
                float brush = tex2D(_BrushTex, brushUV).a;
                float density = SampleBrushDensity(brushUV);
                float soakTime = 1.0 - lerp(density, brush, BrushDetailWeight);
                float threshold = lerp(-_EdgeSoftness, 1.0 + _EdgeSoftness, _RevealProgress);
                float soaked = smoothstep(soakTime - _EdgeSoftness, soakTime + _EdgeSoftness, threshold);
                fixed4 color = (tex2D(_MainTex, input.uv) + _TextureSampleAdd) * input.color;
                // Neutralise the paper and scenery, retaining only chromatic accents.
                // Use source luminance so the original grain and ink details remain.
                float luminance = dot(color.rgb, LuminanceWeights);
                float sourcePaperLuminance = max(dot(_SourcePaperColor.rgb, LuminanceWeights), MinimumSize);
                float2 titleDistance = abs(uv - _TitleArea.xy) / max(_TitleArea.zw, MinimumSize);
                float2 titleFalloff = 1.0 - smoothstep(0.0, 1.0, titleDistance);
                float titleWeight = titleFalloff.x * titleFalloff.y;
                float3 paper = lerp(_PaperColor.rgb, _TitlePaperColor.rgb, titleWeight);
                float normalizedLuminance = saturate(luminance / sourcePaperLuminance);
                float3 neutralIllustration = paper * pow(normalizedLuminance, _InkContrast);
                float grunge = tex2D(_GrungeTex, uv).a;
                float secondGrunge = tex2D(_GrungeTex, frac(uv * GrungeSecondScale + GrungeSecondOffset)).a;
                float grungeWeight = (grunge + secondGrunge) * 0.5 * _GrungeStrength
                    * smoothstep(GrungePaperLow, GrungePaperHigh, normalizedLuminance)
                    * (1.0 - titleWeight * TitleGrungeSuppression);
                neutralIllustration *= 1.0 - grungeWeight;
                float chroma = max(color.r, max(color.g, color.b)) - min(color.r, min(color.g, color.b));
                float accentWeight = smoothstep(AccentChromaLow, AccentChromaHigh, chroma) * _AccentStrength;
                float3 accent = saturate(lerp(float3(luminance, luminance, luminance),
                    color.rgb, _AccentSaturation) * _AccentBrightness);
                float3 illustration = lerp(neutralIllustration, accent, accentWeight);
                float visibility = lerp(_InitialVisibility, 1.0, soaked);
                color.rgb = lerp(paper, illustration, visibility);
                // Elliptical screen-relative shading belongs only to this background.
                // The scroll and button use their own materials and stay unchanged.
                float vignetteRadius = length((uv - _VignetteCenter.xy) * 2.0);
                float vignette = smoothstep(_VignetteInnerRadius,
                    max(_VignetteOuterRadius, _VignetteInnerRadius + MinimumSize), vignetteRadius);
                color.rgb *= 1.0 - vignette * _VignetteStrength;
                color.a *= smoothstep(0.0, PaperArrivalProgress, _RevealProgress);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - MinimumSize);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
