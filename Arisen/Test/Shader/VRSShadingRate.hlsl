struct UniformBufferObject
{
    float4x4 model;
    float4x4 view;
    float4x4 proj;
};

[[vk::binding(0, 0)]]
ConstantBuffer<UniformBufferObject> ubo : register(b0);

struct VSInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
    float3 Normal : NORMAL;
};

VSOutput Vert(VSInput input)
{
    VSOutput output;
    float4 worldPos = mul(ubo.model, float4(input.Position, 1.0));
    output.Position = mul(ubo.proj, mul(ubo.view, worldPos));
    output.UV = input.UV;
    output.Normal = mul((float3x3)ubo.model, input.Normal);
    return output;
}

// Simple hash for noise
float hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
}

[[vk::binding(1, 0)]]
Texture2D texSampler : register(t1);
[[vk::binding(2, 0)]]
SamplerState samplers : register(s1);

float4 Frag(VSOutput input) : SV_Target
{
    // High frequency noise to emphasize VRS differences
    float n = 0.0;
    float2 uv = input.UV * 500.0;
    n += 0.5000 * noise(uv); uv *= 2.02;
    n += 0.2500 * noise(uv); uv *= 2.03;
    n += 0.1250 * noise(uv); uv *= 2.01;
    n += 0.0625 * noise(uv);
    
    float3 baseColor = texSampler.Sample(samplers, input.UV).rgb;
    float3 lightDir = normalize(float3(1, 1, 1));
    float diff = max(dot(normalize(input.Normal), lightDir), 0.2);
    
    float3 finalColor = baseColor * diff * (0.8 + 0.4 * n);
    
    return float4(finalColor, 1.0);
}

