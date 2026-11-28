sampler2D implicitInput : register(s0);
float amount : register(c0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 center = tex2D(implicitInput, uv);
    float shift = max(0.0, amount) * 0.0008;
    float red = tex2D(implicitInput, uv + float2(shift, 0.0)).r;
    float blue = tex2D(implicitInput, uv - float2(shift, 0.0)).b;
    return float4(red, center.g, blue, center.a);
}
