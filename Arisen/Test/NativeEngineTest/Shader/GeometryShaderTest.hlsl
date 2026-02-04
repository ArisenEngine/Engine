
struct VSInput
{
    float3 Pos : POSITION;
    float4 Color : COLOR;
    float2 Size : TEXCOORD; // Quad size (width, height)
};

struct VSOutput
{
    float3 PosW : POSITION;
    float4 Color : COLOR;
    float2 Size : TEXCOORD;
};

struct GSOutput
{
    float4 PosH : SV_POSITION;
    float4 Color : COLOR;
    float2 UV : TEXCOORD;
};

struct SceneData
{
    float4x4 View;
    float4x4 Proj;
    float3 CameraPos;
    float Padding;
};

ConstantBuffer<SceneData> cbScene : register(b0, space0);

VSOutput vs_main(VSInput input)
{
    VSOutput output;
    output.PosW = input.Pos;
    output.Color = input.Color;
    output.Size = input.Size;
    return output;
}

[maxvertexcount(4)]
void gs_main(point VSOutput input[1], inout TriangleStream<GSOutput> outStream)
{
    float3 vPos = input[0].PosW;
    float2 size = input[0].Size * 0.5f;

    float3 look = normalize(cbScene.CameraPos - vPos);
    float3 right = normalize(cross(float3(0, 1, 0), look));
    float3 up = cross(look, right);

    float4x4 viewProj = mul(cbScene.View, cbScene.Proj);

    float3 corners[4];
    corners[0] = vPos + (-right * size.x) + (up * size.y); // Top-left
    corners[1] = vPos + (right * size.x) + (up * size.y);  // Top-right
    corners[2] = vPos + (-right * size.x) - (up * size.y); // Bottom-left
    corners[3] = vPos + (right * size.x) - (up * size.y);  // Bottom-right

    float2 uvs[4] = {
        float2(0, 0),
        float2(1, 0),
        float2(0, 1),
        float2(1, 1)
    };

    GSOutput output;
    output.Color = input[0].Color;

    [unroll]
    for (int i = 0; i < 4; ++i)
    {
        output.PosH = mul(float4(corners[i], 1.0f), viewProj);
        output.UV = uvs[i];
        outStream.Append(output);
    }
}

float4 ps_main(GSOutput input) : SV_TARGET
{
    // Simply output color, maybe a radial gradient for "particle" look
    float dist = length(input.UV - 0.5f);
    if (dist > 0.5f) discard;
    
    float alpha = 1.0f - smoothstep(0.4f, 0.5f, dist);
    return float4(input.Color.rgb, input.Color.a * alpha);
}
