struct VSInput
{
    float3 Position : POSITION;
    float2 UV : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

VSOutput Vert(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position, 1.0);
    output.UV = input.UV;
    return output;
}

float4 Frag(VSOutput input) : SV_Target
{
    // A complex procedural pattern to show visual quality differences
    float2 uv = input.UV * 100.0;
    float pattern = sin(uv.x) * cos(uv.y);
    float grid = (frac(uv.x) < 0.05 || frac(uv.y) < 0.05) ? 1.0 : 0.0;
    
    float3 color = float3(0.5 + 0.5 * pattern, 0.2, 0.8);
    color = lerp(color, float3(1, 1, 1), grid);
    
    return float4(color, 1.0);
}
