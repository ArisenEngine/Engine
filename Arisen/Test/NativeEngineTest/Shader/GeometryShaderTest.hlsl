
struct VSInput
{
    float3 Pos : POSITION;
    float3 Normal : NORMAL;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
};

struct VSOutput
{
    float3 PosW : POSITION;
    float3 NormalW : NORMAL;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
};

struct GSOutput
{
    float4 PosH : SV_POSITION;
    float4 Color : COLOR;
    float2 UV : TEXCOORD0;
    float3 NormalW : NORMAL;
};

struct SceneData
{
    float4x4 Model;
    float4x4 View;
    float4x4 Proj;
    float MipmapBias;
};

ConstantBuffer<SceneData> cbScene : register(b0, space0);

VSOutput vs_main(VSInput input)
{
    VSOutput output;
    float4 posW = mul(cbScene.Model, float4(input.Pos, 1.0f));
    output.PosW = posW.xyz;
    output.NormalW = mul((float3x3)cbScene.Model, input.Normal);
    output.UV = input.UV;
    output.Color = input.Color;
    return output;
}

[maxvertexcount(12)]
void gs_main(triangle VSOutput input[3], inout TriangleStream<GSOutput> outStream)
{
    float4x4 viewProj = mul(cbScene.Proj, cbScene.View);

    // 1. Emit the original triangle
    GSOutput output;
    [unroll]
    for (int i = 0; i < 3; ++i)
    {
        output.PosH = mul(viewProj, float4(input[i].PosW, 1.0f));
        output.Color = input[i].Color;
        output.UV = input[i].UV;
        output.NormalW = input[i].NormalW;
        outStream.Append(output);
    }
    outStream.RestartStrip();

    // 2. Emit fur spikes for each vertex
    float furLength = 0.05f;
    float furWidth = 0.01f;

    for (int j = 0; j < 3; ++j)
    {
        float3 basePos = input[j].PosW;
        float3 normal = normalize(input[j].NormalW);
        float3 tipPos = basePos + normal * furLength;

        // Create a small spike triangle at the vertex
        // Use a simple right vector for width
        float3 up = abs(normal.y) > 0.999f ? float3(0, 0, 1) : float3(0, 1, 0);
        float3 right = normalize(cross(up, normal)) * furWidth;

        // Vertex 1: Base Left
        output.PosH = mul(viewProj, float4(basePos - right, 1.0f));
        output.Color = float4(0.4, 0.2, 0.1, 1.0); // Fur base color
        output.UV = input[j].UV;
        output.NormalW = normal;
        outStream.Append(output);

        // Vertex 2: Base Right
        output.PosH = mul(viewProj, float4(basePos + right, 1.0f));
        output.Color = float4(0.4, 0.2, 0.1, 1.0);
        output.UV = input[j].UV;
        output.NormalW = normal;
        outStream.Append(output);

        // Vertex 3: Tip
        output.PosH = mul(viewProj, float4(tipPos, 1.0f));
        output.Color = float4(0.1, 0.1, 0.05, 1.0); // Fur tip color
        output.UV = input[j].UV;
        output.NormalW = normal;
        outStream.Append(output);

        outStream.RestartStrip();
    }
}

float4 ps_main(GSOutput input) : SV_TARGET
{
    float3 lightDir = normalize(float3(1.0, 1.0, -1.0));
    float diff = max(dot(normalize(input.NormalW), lightDir), 0.2);
    return float4(input.Color.rgb * diff, input.Color.a);
}
