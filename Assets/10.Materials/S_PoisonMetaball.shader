Shader "Team06/PoisonMetaball"
{
    Properties
    {
        _BodyColor ("Liquid", Color) = (0.29,0.48,0.10,1)
        _LightColor ("Highlight", Color) = (0.68,0.78,0.23,1)
        _EdgeColor ("Edge", Color) = (0.07,0.13,0.045,1)
        _BrushTex ("Brush", 2D) = "white" {}
        _BrushStrength ("Brush Strength", Range(0,1)) = 0
        _EdgeStrength ("Edge Strength", Range(0,1)) = 0.3
        _DensityThreshold ("Surface Threshold", Range(0.1,2)) = 0.8
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
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #include "UnityCG.cginc"
            float4 _Metaballs[128];
            int _PointCount;
            float4 _FlowDirection, _BodyColor, _LightColor, _EdgeColor;
            float _BrushStrength, _EdgeStrength, _DensityThreshold;
            sampler2D _BrushTex;
            struct Attributes { float4 position : POSITION; };
            struct Varyings { float4 position : SV_POSITION; float2 worldPosition : TEXCOORD0; };
            Varyings SurfaceVertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.worldPosition = mul(unity_ObjectToWorld,input.position).xy;
                return output;
            }
            float4 SurfaceFragment(Varyings input) : SV_Target
            {
                float density = 0;
                [loop] for (int i=0;i<_PointCount;i++)
                {
                    float2 delta = input.worldPosition-_Metaballs[i].xy;
                    float radius = _Metaballs[i].z;
                    if (abs(delta.x)>radius || abs(delta.y)>radius) continue;
                    float field = saturate(1-dot(delta,delta)/(radius*radius));
                    density += field*field;
                }
                float feather = max(fwidth(density),0.025);
                float alpha = smoothstep(_DensityThreshold-feather,_DensityThreshold+feather,density);
                clip(alpha-0.001);
                float2 perpendicular = float2(-_FlowDirection.y,_FlowDirection.x);
                float2 flowUV = float2(dot(input.worldPosition,_FlowDirection.xy)*0.13-_Time.y*0.35,
                    dot(input.worldPosition,perpendicular)*0.6);
                float2 brushUV = 1-abs(frac(flowUV)*2-1);
                float brush = tex2D(_BrushTex,brushUV).a;
                float3 body = lerp(_BodyColor.rgb,_LightColor.rgb,0.12*saturate(density-1));
                body *= lerp(1,0.8+0.2*brush,_BrushStrength);
                float ink = (1-smoothstep(_DensityThreshold,_DensityThreshold+0.35,density))*_EdgeStrength;
                return float4(lerp(body,_EdgeColor.rgb,ink),alpha*_BodyColor.a);
            }
            ENDHLSL
        }
    }
}
