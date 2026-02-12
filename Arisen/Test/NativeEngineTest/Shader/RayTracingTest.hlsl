
struct RayPayloadFixed
{
    float3 radiance;
    uint seed;
};

struct GLTFVertex
{
    float3 pos;
    float3 normal;
    float2 uv;
    float4 color;
};

struct MaterialData
{
    float4 baseColorFactor;
    int baseColorTextureIndex;
    float metallicFactor;
    float roughnessFactor;
    int padding;
};

struct SubmeshData
{
    uint materialIndex;
    uint firstIndex;
    uint2 padding;
};

RaytracingAccelerationStructure Scene : register(t0, space0);
RWTexture2D<float4> RenderTarget : register(u1, space0);

struct PointLight
{
    float4 posRange;
    float4 colorInt;
};

cbuffer CameraBuffer : register(b2, space0)
{
    float4x4 viewInverse;
    float4x4 projInverse;
    float4 lightPosAndFrameCount; 
    PointLight pointLights[8];
    int numPointLights;
    int padding[3];
};

StructuredBuffer<GLTFVertex> Vertices : register(t3, space0);
StructuredBuffer<uint> Indices : register(t4, space0);
StructuredBuffer<MaterialData> Materials : register(t5, space0);
StructuredBuffer<SubmeshData> SubmeshInfo : register(t6, space0);
Texture2D ModelTextures[100] : register(t7, space0);
SamplerState DefaultSampler : register(s8, space0);
RWTexture2D<float4> AccumulationTarget : register(u9, space0);

// --- Random Helpers ---
uint PCGHash(uint seed)
{
    uint state = seed * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

float RandomFloat(inout uint seed)
{
    seed = PCGHash(seed);
    return (seed & 0xFFFFFFu) / 16777216.0f;
}

[shader("raygeneration")]
void RayGen()
{
    uint3 launchID = DispatchRaysIndex();
    uint3 launchSize = DispatchRaysDimensions();
    
    int frameCount = (int)lightPosAndFrameCount.w;
    uint seed = PCGHash(launchID.y * launchSize.x + launchID.x + PCGHash((uint)frameCount));

    float2 jitter = float2(RandomFloat(seed), RandomFloat(seed)) - 0.5f;
    float2 d = (((float2)launchID.xy + 0.5f + jitter) / (float2)launchSize.xy) * 2.f - 1.f;

    float4 target = mul(projInverse, float4(d.x, d.y, 1, 1));
    target.xyz /= target.w;
    float3 rayDir = mul(viewInverse, float4(normalize(target.xyz), 0)).xyz;
    float3 rayOrigin = viewInverse[3].xyz;

    RayPayloadFixed payload;
    payload.radiance = float3(0, 0, 0);
    payload.seed = seed;

    RayDesc ray;
    ray.Origin = rayOrigin;
    ray.Direction = rayDir;
    ray.TMin = 0.01; // Increased TMin to prevent precision noise/rings
    ray.TMax = 10000.0;

    TraceRay(Scene, RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 0, 0, ray, payload);

    float3 currentRadiance = payload.radiance;
    
    float3 accumulatedColor = currentRadiance;
    if (frameCount > 0)
    {
        float3 prevColor = AccumulationTarget[launchID.xy].rgb;
        accumulatedColor = lerp(prevColor, currentRadiance, 1.0 / (float(frameCount) + 1.0));
    }
    AccumulationTarget[launchID.xy] = float4(accumulatedColor, 1.0);
    
    // Tonemapping and SRGB
    float3 finalColor = pow(max(accumulatedColor, 0.0), 1.0 / 2.2);

    RenderTarget[launchID.xy] = float4(finalColor, 1.0);
}

[shader("miss")]
void Miss(inout RayPayloadFixed payload)
{
    payload.radiance = float3(0.1, 0.12, 0.15); 
}

[shader("closesthit")]
void ClosestHit(inout RayPayloadFixed payload, in BuiltInTriangleIntersectionAttributes attr)
{
    uint geomIndex = GeometryIndex();
    uint triIndex = PrimitiveIndex();
    
    SubmeshData sub = SubmeshInfo[geomIndex];
    uint matIndex = sub.materialIndex;
    uint baseIndex = sub.firstIndex;
    
    MaterialData mat = Materials[min(matIndex, 100)];
    
    uint i0 = Indices[baseIndex + triIndex * 3 + 0];
    uint i1 = Indices[baseIndex + triIndex * 3 + 1];
    uint i2 = Indices[baseIndex + triIndex * 3 + 2];
    
    GLTFVertex v0 = Vertices[i0];
    GLTFVertex v1 = Vertices[i1];
    GLTFVertex v2 = Vertices[i2];
    
    float3 bary = float3(1.0 - attr.barycentrics.x - attr.barycentrics.y, attr.barycentrics.x, attr.barycentrics.y);
    float2 uv = v0.uv * bary.x + v1.uv * bary.y + v2.uv * bary.z;
    
    // Display raw UVs for verification
    // Note: uv.y = 1.0 - uv.y; // Keep or remove based on previous test result
    payload.radiance = float3(uv.x, uv.y, 0.0);
}
