Shader "UI/Ep1TutorialSoul"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _FlameColor ("Flame Color", Color) = (1,1,1,1)
        _FlameInnerColor ("Inner Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            fixed4 _FlameColor;
            fixed4 _FlameInnerColor;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.uv);
                float luminance = dot(sprite.rgb, float3(0.2126,0.7152,0.0722));
                float ramp = smoothstep(0.25,0.78,luminance);
                fixed3 tint = lerp(_FlameColor.rgb,_FlameInnerColor.rgb,ramp) * saturate(luminance);
                float hi = max(_FlameColor.r,max(_FlameColor.g,_FlameColor.b));
                float lo = min(_FlameColor.r,min(_FlameColor.g,_FlameColor.b));
                float weight = smoothstep(0.05,0.35,saturate((hi-lo)/max(hi,0.001)));
                fixed3 flame = lerp(sprite.rgb,tint,weight);
                float core = smoothstep(0.905,0.955,luminance);
                return fixed4(lerp(flame,sprite.rgb,core)*i.color.rgb,sprite.a*i.color.a*_FlameColor.a);
            }
            ENDCG
        }
    }
}
