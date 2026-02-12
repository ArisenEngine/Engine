
struct RayPayloadFixed
{
    float3 radiance;
    float3 throughput;
    float3 nextOrigin;
    float3 nextDirection;
    float3 albedoDebug;
    uint seed;
    int depth;
};

struct ShadowPayload
{
    bool hit;
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

[shader("raygeneration")]
void RayGen()
{
    uint3 launchID = DispatchRaysIndex();
    uint3 launchSize = DispatchRaysDimensions();
    
    float2 d = (((float2)launchID.xy + 0.5f) / (float2)launchSize.xy) * 2.f - 1.f;

    float4 target = mul(projInverse, float4(d.x, d.y, 1, 1));
    target.xyz /= target.w;
    float3 rayDir = mul(viewInverse, float4(normalize(target.xyz), 0)).xyz;
    float3 rayOrigin = viewInverse[3].xyz;

    RayPayloadFixed payload;
    payload.radiance = float3(0, 0, 0);

    RayDesc ray;
    ray.Origin = rayOrigin;
    ray.Direction = rayDir;
    ray.TMin = 0.001;
    ray.TMax = 10000.0;

    TraceRay(Scene, RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 0, 0, ray, payload);

    // Simple raw output for verification
    RenderTarget[launchID.xy] = float4(payload.radiance, 1.0);
}

[shader("miss")]
void Miss(inout RayPayloadFixed payload)
{
    payload.radiance = float3(0.1, 0.1, 0.1); // Dark gray background
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
    
    float2 uv = v0.uv * (1.0 - attr.barycentrics.x - attr.barycentrics.y) + v1.uv * attr.barycentrics.x + v2.uv * attr.barycentrics.y;
    
    float4 baseColor = mat.baseColorFactor;
    if (mat.baseColorTextureIndex >= 0 && mat.baseColorTextureIndex < 100)
    {
        baseColor *= ModelTextures[mat.baseColorTextureIndex].SampleLevel(DefaultSampler, uv, 0);
    }
    
    payload.radiance = baseColor.rgb;
}
