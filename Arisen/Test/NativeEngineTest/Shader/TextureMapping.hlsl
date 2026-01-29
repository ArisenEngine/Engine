struct Attribute
{
    float3 positionOS : POSITION0;
    float2 uv : TEXCOORD0;
};

struct Varying
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

cbuffer UboView : register(b0, space0)
{
    float4x4 model;
    float4x4 view;
    float4x4 projection;
};

Texture2D tex : register(t1, space0);
SamplerState sam : register(s1, space0);

Varying Vert(Attribute input)
{
    Varying output = (Varying)0;
    output.positionCS = mul(projection, mul(view, mul(model, float4(input.positionOS, 1.0))));
    output.uv = input.uv;
    return output;
}

float4 Frag(Varying input) : SV_Target
{
    return tex.Sample(sam, input.uv);
}
