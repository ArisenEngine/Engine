struct Attribute
{
    float3 positionOS : POSITION0;
    float3 color : COLOR0;
};

struct Varying
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

Varying Vert(Attribute input, uint vertexId : SV_VertexID)
{
    Varying output = (Varying)0;
    // 00, 01, 10
    output.positionCS = float4((vertexId >> 1) << 1, (vertexId << 1) & 2, 0, 1.0);

    // 计算 UV 坐标
    output.uv = float2(vertexId / 3.0, vertexId / 3.0);
    return output;
}

float4 Frag(Varying input) : SV_Target
{
    return float4(1.0, 1.0, 0, 1.0);
}