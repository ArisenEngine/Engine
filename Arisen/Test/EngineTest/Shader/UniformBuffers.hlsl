struct Attribute
{
    float3 positionOS : POSITION0;
    float3 color : COLOR0;
};

struct Varying
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 color : TEXCOORD1;
};

cbuffer UboView : register(b0, space0) // 绑定到 set 0, binding 0
{
    float4x4 model;
    float4x4 projection;
    float4x4 view;
};

Varying Vert(Attribute input, uint vertexId : SV_VertexID)
{
    Varying output = (Varying)0;
    // 00, 01, 10
    output.positionCS = mul(projection, mul(view, mul(model, float4(input.positionOS, 1.0))));
    output.color = input.color;
    return output;
}

float4 Frag(Varying input) : SV_Target
{
    return float4(input.color, 1.0);
}