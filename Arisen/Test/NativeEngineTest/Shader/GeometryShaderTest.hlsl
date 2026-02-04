
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
Texture2D baseColorTexture : register(t1, space0);
SamplerState baseColorSampler : register(s2, space0);

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
    float furLength = 0.12f; // Longer fur
    float furWidth = 0.008f;
    float3 gravity = float3(0.0f, -0.6f, 0.0f); // Gravity for drooping

    for (int j = 0; j < 3; ++j)
    {
        float3 basePos = input[j].PosW;
        float3 normal = normalize(input[j].NormalW);
        
        // Calculate tip position with drooping
        float3 tipPos = basePos + normal * furLength;
        tipPos += gravity * (furLength * furLength); // Droop factor tied to length

        // Create a small spike triangle at the vertex
        float3 up = abs(normal.y) > 0.999f ? float3(0, 0, 1) : float3(0, 1, 0);
        float3 right = normalize(cross(up, normal)) * furWidth;

        // Sample base color for the fur
        float4 baseColor = baseColorTexture.SampleLevel(baseColorSampler, input[j].UV, 0);

        // Vertex 1: Base Left
        output.PosH = mul(viewProj, float4(basePos - right, 1.0f));
        output.Color = baseColor; 
        output.UV = input[j].UV;
        output.NormalW = normal;
        outStream.Append(output);

        // Vertex 2: Base Right
        output.PosH = mul(viewProj, float4(basePos + right, 1.0f));
        output.Color = baseColor;
        output.UV = input[j].UV;
        output.NormalW = normal;
        outStream.Append(output);

        // Vertex 3: Tip (darker)
        output.PosH = mul(viewProj, float4(tipPos, 1.0f));
        output.Color = baseColor * 0.4f; // Darker tip
        output.UV = input[j].UV;
        output.NormalW = normalize(normal + gravity * 0.5f);
        outStream.Append(output);

        outStream.RestartStrip();
    }
}

float4 ps_main(GSOutput input) : SV_TARGET
{
    float4 texColor = baseColorTexture.Sample(baseColorSampler, input.UV);
    float3 finalColor = texColor.rgb;
    
    // If color in input is overridden (like for spikes), blend it
    // Note: for original triangle, input.Color is from VS; for spikes, it's the sampled baseColor
    finalColor = input.Color.rgb;

    float3 lightDir = normalize(float3(1.0, 1.0, -1.0));
    float diff = max(dot(normalize(input.NormalW), lightDir), 0.2);
    
    return float4(finalColor * diff, 1.0f);
}
