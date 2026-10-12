Shader "Team06/BackgroundSeamBlend"
{
    Properties
    {
        _LeftTex ("Left", 2D) = "white" {}
        _RightTex ("Right", 2D) = "white" {}
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };
            sampler2D _LeftTex, _RightTex;
            float4 _LeftBounds, _RightBounds, _LeftUV, _RightUV;
            float4 _LeftFlip, _RightFlip, _LeftColor, _RightColor;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.uv = v.uv;
                return o;
            }
            float2 SpriteUV(float2 world, float4 bounds, float4 uvRect, float4 flip)
            {
                float2 uv = saturate((world - bounds.xy) / bounds.zw);
                uv = lerp(uv, 1.0 - uv, flip.xy);
                return uvRect.xy + uv * uvRect.zw;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 left = tex2D(_LeftTex, SpriteUV(i.world, _LeftBounds, _LeftUV, _LeftFlip)) * _LeftColor;
                fixed4 right = tex2D(_RightTex, SpriteUV(i.world, _RightBounds, _RightUV, _RightFlip)) * _RightColor;
                float t = smoothstep(0.0, 1.0, i.uv.x);
                return lerp(left, right, t);
            }
            ENDCG
        }
    }
}
