
struct RayPayloadFixed
{
    float3 radiance;
    float3 throughput;
    float3 nextOrigin;
    float3 nextDirection;
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

RaytracingAccelerationStructure Scene : register(t0, space0);
RWTexture2D<float4> RenderTarget : register(u1, space0);

cbuffer CameraBuffer : register(b2, space0)
{
    float4x4 viewInverse;
    float4x4 projInverse;
    float4 lightPosAndFrameCount; // xyz: lightPos, w: frameCount
};

StructuredBuffer<GLTFVertex> Vertices : register(t3, space0);
StructuredBuffer<uint> Indices : register(t4, space0);
StructuredBuffer<MaterialData> Materials : register(t5, space0);
StructuredBuffer<uint> TriangleMaterialIndices : register(t6, space0);
Texture2D ModelTextures[100] : register(t7, space0);
SamplerState DefaultSampler : register(s8, space0);
RWTexture2D<float4> AccumulationTarget : register(u9, space0);

// --- Utilities ---

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
    while (true)
    {
        float3 p = float3(RandomFloat(seed), RandomFloat(seed), RandomFloat(seed)) * 2.0 - 1.0;
        if (dot(p, p) < 1.0) return p;
    }
}

float3 RandomUnitVector(inout uint seed)
{
    return normalize(RandomInUnitSphere(seed));
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

    const int maxBounces = 8;
    for (int bounce = 0; bounce < maxBounces; ++bounce)
    {
        RayDesc ray;
        ray.Origin = payload.nextOrigin;
        ray.Direction = payload.nextDirection;
        ray.TMin = 0.001;
        ray.TMax = 10000.0;

        TraceRay(Scene, RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 0, ray, payload);

        if (payload.depth == -1) break;
        payload.depth++;
        
        // Russian Roulette
        if (bounce > 3)
        {
            float p = max(payload.throughput.r, max(payload.throughput.g, payload.throughput.b));
            if (RandomFloat(payload.seed) > p) break;
            payload.throughput /= p;
        }
    }

    float3 finalColor = payload.radiance;
    if (frameCount > 0)
    {
        float3 prevColor = AccumulationTarget[launchID.xy].rgb;
        finalColor = lerp(prevColor, finalColor, 1.0f / (float)(frameCount + 1));
    }
    
    AccumulationTarget[launchID.xy] = float4(finalColor, 1.0);
    RenderTarget[launchID.xy] = float4(finalColor, 1.0);
}

[shader("miss")]
void Miss(inout RayPayloadFixed payload)
{
    // Classic gradient sky from "One Weekend"
    float3 rayDir = WorldRayDirection();
    float t = 0.5 * (rayDir.y + 1.0);
    float3 skyColor = (1.0 - t) * float3(1.0, 1.0, 1.0) + t * float3(0.5, 0.7, 1.0);
    
    // Add a sun
    float3 sunDir = normalize(float3(1, 4, 1));
    float sun = pow(max(dot(rayDir, sunDir), 0.0), 64.0);
    skyColor += sun * float3(15.0, 10.0, 5.0);

    payload.radiance += payload.throughput * skyColor;
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
    uint triIndex = PrimitiveIndex();
    uint matIndex = TriangleMaterialIndices[triIndex];
    MaterialData mat = Materials[matIndex];
    
    uint i0 = Indices[triIndex * 3 + 0];
    uint i1 = Indices[triIndex * 3 + 1];
    uint i2 = Indices[triIndex * 3 + 2];
    
    GLTFVertex v0 = Vertices[i0];
    GLTFVertex v1 = Vertices[i1];
    GLTFVertex v2 = Vertices[i2];
    
    float2 uv = v0.uv * (1.0 - attr.barycentrics.x - attr.barycentrics.y) + v1.uv * attr.barycentrics.x + v2.uv * attr.barycentrics.y;
    float3 N = normalize(v0.normal * (1.0 - attr.barycentrics.x - attr.barycentrics.y) + v1.normal * attr.barycentrics.x + v2.normal * attr.barycentrics.y);
    float3 worldPos = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
    
    float4 baseColor = mat.baseColorFactor;
    if (mat.baseColorTextureIndex >= 0)
    {
        baseColor *= ModelTextures[mat.baseColorTextureIndex].SampleLevel(DefaultSampler, uv, 0);
    }
    
    float3 albedo = baseColor.rgb;
    float metallic = mat.metallicFactor;
    float roughness = mat.roughnessFactor;
    float3 V = -WorldRayDirection();

    // Mapping to "One Weekend" materials
    bool isMetal = metallic > 0.5;

    if (isMetal)
    {
        // Metal: reflection + fuzz
        float3 reflected = reflect(WorldRayDirection(), N);
        payload.nextDirection = normalize(reflected + roughness * RandomInUnitSphere(payload.seed));
        payload.throughput *= albedo;
        
        // Ensure the reflected ray is above the surface
        if (dot(payload.nextDirection, N) <= 0)
        {
            payload.depth = -1;
        }
    }
    else
    {
        // Lambertian: p + n + random_unit_vector
        payload.nextDirection = normalize(N + RandomUnitVector(payload.seed));
        payload.throughput *= albedo;
    }
    
    payload.nextOrigin = worldPos + N * 0.0001f;
}
