struct VSInput
{
    uint VertexID : SV_VertexID;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR;
};

VSOutput MainVS(VSInput input)
{
    VSOutput output;
    
    // Hardcoded positions for a triangle
    float2 positions[3] = {
        float2(0.0, -0.5),
        float2(0.5, 0.5),
        float2(-0.5, 0.5)
    };
    
    float4 colors[3] = {
        float4(1.0, 0.0, 0.0, 1.0),
        float4(0.0, 1.0, 0.0, 1.0),
        float4(0.0, 0.0, 1.0, 1.0)
    };
    
    output.Position = float4(positions[input.VertexID], 0.0, 1.0);
    output.Color = colors[input.VertexID];
    
    return output;
}

float4 MainPS(VSOutput input) : SV_Target
{
    return input.Color;
}
