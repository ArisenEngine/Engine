struct Attributes
{
    uint vertexID:SV_VertexID;
};

struct Varyings
{
    float4 positionCS:SV_POSITION;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = float4(input.vertexID >> 1, input.vertexID << 2, input.vertexID & 2, 1.0);
    return output;
}

struct RenderOutput
{
    float4 colorBuffer:SV_Target;
};

RenderOutput FragRender(Varyings input)
{
    RenderOutput output;
    output.colorBuffer = float4(input.positionCS.x, 1, 1, 1);
    return output;
}
