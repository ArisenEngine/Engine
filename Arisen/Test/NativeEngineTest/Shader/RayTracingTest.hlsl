
struct RayPayloadFixed
{
    float3 radiance;
    float3 throughput;
    float3 nextOrigin;
    float3 nextDirection;
    float3 albedoDebug;  // Debug field for albedo visualization
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
};

RaytracingAccelerationStructure Scene : register(t0, space0);
RWTexture2D<float4> RenderTarget : register(u1, space0);

struct PointLight
{
    float4 posRange;   // xyz: pos, w: range
    float4 colorInt;   // xyz: color, w: intensity
};

cbuffer CameraBuffer : register(b2, space0)
{
    float4x4 viewInverse;
    float4x4 projInverse;
    float4 lightPosAndFrameCount; // xyz: sunPos, w: frameCount
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

// --- Utilities ---

float3 LessThan(float3 f, float value)
{
    return float3(
        (f.x < value) ? 1.0f : 0.0f,
        (f.y < value) ? 1.0f : 0.0f,
        (f.z < value) ? 1.0f : 0.0f);
}

float3 LinearToSRGB(float3 rgb)
{
    rgb = clamp(rgb, 0.0f, 1.0f);
    return lerp(
        pow(rgb, float3(1.0f / 2.4f, 1.0f / 2.4f, 1.0f / 2.4f)) * 1.055f - 0.055f,
        rgb * 12.92f,
        LessThan(rgb, 0.0031308f)
    );
}

float3 SRGBToLinear(float3 rgb)
{
    rgb = clamp(rgb, 0.0f, 1.0f);
    return lerp(
        pow((rgb + 0.055f) / 1.055f, float3(2.4f, 2.4f, 2.4f)),
        rgb / 12.92f,
        LessThan(rgb, 0.04045f)
    );
}

float3 ACESToneMapping(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0f, 1.0f);
}

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

float3 RandomInUnitSphere(inout uint seed)
{
    for (int i = 0; i < 16; ++i)
    {
        float3 p = float3(RandomFloat(seed), RandomFloat(seed), RandomFloat(seed)) * 2.0 - 1.0;
        if (dot(p, p) < 1.0) return p;
    }
    return float3(0, 1, 0); // Fallback
}

float3 RandomUnitVector(inout uint seed)
{
    float3 p = RandomInUnitSphere(seed);
    float d2 = dot(p, p);
    if (d2 < 0.0001f) return float3(0, 1, 0);
    return p / sqrt(d2);
}

bool IsNearZero(float3 v)
{
    const float s = 1e-8;
    return (abs(v.x) < s) && (abs(v.y) < s) && (abs(v.z) < s);
}

// --- Shaders ---

[shader("raygeneration")]
void RayGen()
{
    uint3 launchID = DispatchRaysIndex();
    uint3 launchSize = DispatchRaysDimensions();
    
    int frameCount = (int)lightPosAndFrameCount.w;
    uint seed = PCGHash(launchID.y * launchSize.x + launchID.x + PCGHash((uint)frameCount));

    // Anti-aliasing jitter
    float2 jitter = float2(RandomFloat(seed), RandomFloat(seed)) - 0.5f;
    float2 d = (((float2)launchID.xy + 0.5f + jitter) / (float2)launchSize.xy) * 2.f - 1.f;

    float4 target = mul(projInverse, float4(d.x, d.y, 1, 1));
    target.xyz /= target.w;
    float3 rayDir = mul(viewInverse, float4(normalize(target.xyz), 0)).xyz;
    float3 rayOrigin = viewInverse[3].xyz;

    RayPayloadFixed payload;
    payload.radiance = float3(0, 0, 0);
    payload.throughput = float3(1, 1, 1);
    payload.seed = seed;
    payload.depth = 0;
    payload.nextOrigin = rayOrigin;
    payload.nextDirection = rayDir;

    // Simple single ray (no bounces for debugging)
    RayDesc ray;
    ray.Origin = rayOrigin;
    ray.Direction = rayDir;
    ray.TMin = 0.001;
    ray.TMax = 10000.0;

    TraceRay(Scene, RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 0, ray, payload);

    float3 finalColor = payload.radiance;
    
    // Guard against NaNs
    if (any(isnan(finalColor)) || any(isinf(finalColor))) finalColor = float3(0, 0, 0);

    // Direct output (no accumulation, no post-processing)
    RenderTarget[launchID.xy] = float4(finalColor, 1.0);
}

[shader("miss")]
void Miss(inout RayPayloadFixed payload)
{
    // Simple blue sky
    float3 rayDir = WorldRayDirection();
    float t = 0.5 * (rayDir.y + 1.0);
    float3 skyColor = (1.0 - t) * float3(1.0, 1.0, 1.0) + t * float3(0.5, 0.7, 1.0);

    payload.radiance = skyColor;
    payload.depth = -1;
}

[shader("miss")]
void ShadowMiss(inout ShadowPayload payload)
{
    payload.hit = false;
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
    
    // Use baseIndex to access the correct part of the global index buffer
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
    payload.depth = -1;
}
