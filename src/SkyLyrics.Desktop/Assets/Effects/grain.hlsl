sampler2D implicitInput : register(s0);
float intensity : register(c0);
float phase : register(c1);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 source = tex2D(implicitInput, uv);
    float2 pixel = uv * float2(1920.0, 1080.0);
    float noise = frac(sin(dot(pixel + phase, float2(12.9898, 78.233))) * 43758.5453);
    float grain = (noise - 0.5) * intensity;
    return float4(saturate(source.rgb + grain), source.a);
}
