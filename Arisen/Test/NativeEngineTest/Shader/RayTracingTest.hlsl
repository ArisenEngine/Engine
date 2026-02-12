
struct RayPayload
{
    float radiance; // We can pack more if needed, but for now radiance and throughput are enough
    float3 throughput;
    float3 radiance_packed; // Using float3 for color
    float3 nextOrigin;
    float3 nextDirection;
    uint seed;
    int depth;
};

// Re-structured payload for better throughput
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

// Random Number Generation (PCG)
uint NextRandom(inout uint seed)
{
    seed = seed * 747796405u + 2891336453u;
    uint word = ((seed >> ((seed >> 28u) + 4u)) ^ seed) * 277803737u;
    return (word >> 22u) ^ word;
}

float RandomFloat(inout uint seed)
{
    return (NextRandom(seed) & 0xFFFFFFu) / 16777216.0f;
}

float3 RandomCosineDirection(inout uint seed)
{
    float r1 = RandomFloat(seed);
    float r2 = RandomFloat(seed);
    float z = sqrt(1.0 - r2);
    float phi = 2.0 * 3.14159265f * r1;
    float x = cos(phi) * sqrt(r2);
    float y = sin(phi) * sqrt(r2);
    return float3(x, y, z);
}

void Onb(float3 n, out float3 b1, out float3 b2)
{
    float sign = n.z >= 0.0 ? 1.0 : -1.0;
    float a = -1.0 / (sign + n.z);
    float b = n.x * n.y * a;
    b1 = float3(1.0 + sign * n.x * n.x * a, sign * b, -sign * n.x);
    b2 = float3(b, sign + n.y * n.y * a, -n.y);
}

[shader("raygeneration")]
void RayGen()
{
    uint3 launchID = DispatchRaysIndex();
    uint3 launchSize = DispatchRaysDimensions();
    
    int frameCount = (int)lightPosAndFrameCount.w;
    uint seed = launchID.y * launchSize.x + launchID.x + (uint)frameCount * 719391u;

    float3 accumulationRadiance = float3(0, 0, 0);
    int samplesPerFrame = 1;

    for (int s = 0; s < samplesPerFrame; ++s)
    {
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

        for (int bounce = 0; bounce < 4; ++bounce)
        {
            RayDesc ray;
            ray.Origin = payload.nextOrigin;
            ray.Direction = payload.nextDirection;
            ray.TMin = 0.001;
            ray.TMax = 10000.0;

            TraceRay(Scene, RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 0, ray, payload);

            if (payload.depth == -1) break;
            payload.depth++;
            
            if (bounce > 2)
            {
                float p = max(payload.throughput.r, max(payload.throughput.g, payload.throughput.b));
                if (RandomFloat(payload.seed) > p) break;
                payload.throughput /= p;
            }
        }
        accumulationRadiance += payload.radiance;
        seed = payload.seed;
    }
    accumulationRadiance /= (float)samplesPerFrame;

    // Accumulation logic
    float3 finalColor = accumulationRadiance;
    if (frameCount > 0)
    {
        float3 prevColor = AccumulationTarget[launchID.xy].rgb;
        float weight = 1.0f / (float)(frameCount + 1);
        finalColor = lerp(prevColor, accumulationRadiance, weight);
    }
    
    AccumulationTarget[launchID.xy] = float4(finalColor, 1.0);
    RenderTarget[launchID.xy] = float4(finalColor, 1.0);
}

[shader("miss")]
void Miss(inout RayPayloadFixed payload)
{
    float3 rayDir = WorldRayDirection();
    float t = 0.5 * (rayDir.y + 1.0);
    float3 skyColor = t * float3(0.5, 0.7, 1.0) + (1.0 - t) * float3(1.0, 1.0, 1.0);
    
    float3 sunDir = normalize(float3(1, 3, 1));
    float sun = pow(max(dot(rayDir, sunDir), 0.0), 128.0);
    skyColor += sun * float3(40.0, 35.0, 30.0);

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
    float3 normal = normalize(v0.normal * (1.0 - attr.barycentrics.x - attr.barycentrics.y) + v1.normal * attr.barycentrics.x + v2.normal * attr.barycentrics.y);
    float3 worldPos = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
    
    float4 baseColor = mat.baseColorFactor;
    if (mat.baseColorTextureIndex >= 0)
    {
        baseColor *= ModelTextures[mat.baseColorTextureIndex].SampleLevel(DefaultSampler, uv, 0);
    }
    
    float3 albedo = baseColor.rgb;
    
    // NEE
    float3 sunDir = normalize(float3(1, 3, 1));
    float3 sunColor = float3(40.0, 35.0, 30.0);
    
    RayDesc shadowRay;
    shadowRay.Origin = worldPos + normal * 0.02f;
    shadowRay.Direction = sunDir;
    shadowRay.TMin = 0.001;
    shadowRay.TMax = 10000.0;
    
    ShadowPayload sPayload;
    sPayload.hit = true;
    TraceRay(Scene, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER | RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 1, shadowRay, sPayload);
    
    if (!sPayload.hit)
    {
        float nDotL = max(dot(normal, sunDir), 0.0);
        payload.radiance += payload.throughput * albedo * nDotL * sunColor * 0.5f; 
    }
    
    payload.radiance += payload.throughput * albedo * 0.02; // Ambient
    payload.throughput *= albedo;
    
    float3 b1, b2;
    Onb(normal, b1, b2);
    float3 localDir = RandomCosineDirection(payload.seed);
    float3 nextDir = localDir.x * b1 + localDir.y * b2 + localDir.z * normal;
    
    payload.nextOrigin = worldPos + normal * 0.02f;
    payload.nextDirection = normalize(nextDir);
}
