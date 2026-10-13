#ifndef TEAM06_POISON_PAINT_SURFACE
#define TEAM06_POISON_PAINT_SURFACE
// 줄기와 방울이 같은 색면, 붓결, 먹색 경계를 사용한다.
float PoisonHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}
float PoisonNoise(float2 p)
{
    float2 grid = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(PoisonHash(grid), PoisonHash(grid + float2(1, 0)), f.x),
        lerp(PoisonHash(grid + float2(0, 1)), PoisonHash(grid + float2(1, 1)), f.x), f.y);
}
float2 PoisonBrushUV(float2 p)
{
    return 1.0 - abs(frac(p) * 2.0 - 1.0);
}
float3 EvaluatePoisonPaint(float2 uv, float brush, float detail, float across,
    float3 bodyColor, float3 lightColor, float3 edgeColor)
{
    float stain = 0.55 * PoisonNoise(uv * float2(0.65, 3.0)) + 0.45 * brush;
    float shadow = smoothstep(0.48, 0.62, 1.0 - stain);
    float3 color = lerp(bodyColor, edgeColor, shadow * 0.48);
    // 높은 광택 띠가 아니라 제한된 불규칙 색면을 만든다.
    float light = smoothstep(0.65, 0.81, stain) * smoothstep(0.24, 0.55, detail);
    color = lerp(color, lightColor, light * 0.65);
    color *= lerp(0.92, 1.02, detail);
    float ink = smoothstep(0.7, 0.94, across) * smoothstep(0.32, 0.64, detail);
    return lerp(color, edgeColor, ink * 0.65);
}
#endif
