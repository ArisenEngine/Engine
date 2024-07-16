struct VSInput
{
    float3 positionOS : POSITION0;
    float3 color : COLOR0;
};

struct VSOutput
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

VSOutput main(VSInput input, uint vertexId : SV_VertexID)
{
    VSOutput output = (VSOutput)0;
    // 00, 01, 10
    output.positionCS = float4((vertexId >> 1) << 1, (vertexId << 1) & 2, 0, 1.0);

    // 计算 UV 坐标
    output.uv = float2((vertexId << 1) & 2, vertexId & 2);
    return output;
}