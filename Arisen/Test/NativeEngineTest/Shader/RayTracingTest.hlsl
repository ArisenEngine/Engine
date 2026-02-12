
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

float Halton(uint index, uint base)
{
    float result = 0;
    float f = 1.0f / (float)base;
    uint i = index;
    while (i > 0)
    {
        result += f * (float)(i % base);
        i /= base;
        f /= (float)base;
    }
    return result;
}

float2 GetHaltonJitter(uint index)
{
    return float2(Halton(index, 2), Halton(index, 3));
}

void Onb(float3 n, out float3 b1, out float3 b2)
{
    float sign = n.z >= 0.0 ? 1.0 : -1.0;
    float a = -1.0 / (sign + n.z);
    float b = n.x * n.y * a;
    b1 = float3(1.0 + sign * n.x * n.x * a, sign * b, -sign * n.x);
    b2 = float3(b, sign + n.y * n.y * a, -n.y);
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

// --- BSDF Functions ---

float D_GGX(float NdotH, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float denom = (NdotH * NdotH * (a2 - 1.0) + 1.0);
    return a2 / (3.14159265f * denom * denom);
}

float G_SchlickGGX(float NdotV, float roughness)
{
    float k = (roughness * roughness) / 2.0f;
    return NdotV / (NdotV * (1.0 - k) + k);
}

float G_Smith(float NdotV, float NdotL, float roughness)
{
    return G_SchlickGGX(NdotV, roughness) * G_SchlickGGX(NdotL, roughness);
}

float3 F_Schlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

// --- Shaders ---

[shader("raygeneration")]
void RayGen()
{
    uint3 launchID = DispatchRaysIndex();
    uint3 launchSize = DispatchRaysDimensions();
    
    int frameCount = (int)lightPosAndFrameCount.w;
    uint seed = launchID.y * launchSize.x + launchID.x + (uint)frameCount * 719391u;

    float2 jitter = GetHaltonJitter((uint)frameCount) - 0.5f;
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

    for (int bounce = 0; bounce < 5; ++bounce)
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
        if (bounce > 2)
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
    else
    {
        // First frame: just use current radiance
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
    
    float3 sunDir = normalize(float3(1, 4, 1));
    float sun = pow(max(dot(rayDir, sunDir), 0.0), 32.0); // Softer sun
    skyColor += sun * float3(10.0, 8.0, 5.0);

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
    float roughness = max(mat.roughnessFactor, 0.01f);
    float metallic = mat.metallicFactor;
    float3 V = -WorldRayDirection();
    
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);

    // --- Direct Lighting (NEE) ---
    {
        float3 sunDir = normalize(float3(1, 4, 1));
        float3 sunColor = float3(10.0, 8.0, 5.0);
        
        RayDesc shadowRay;
        shadowRay.Origin = worldPos + N * 0.001f;
        shadowRay.Direction = sunDir;
        shadowRay.TMin = 0.001;
        shadowRay.TMax = 10000.0;
        
        ShadowPayload sPayload;
        sPayload.hit = true;
        TraceRay(Scene, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER | RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 1, shadowRay, sPayload);
        
        if (!sPayload.hit)
        {
            float3 L = sunDir;
            float3 H = normalize(V + L);
            float NdotL = max(dot(N, L), 0.0);
            float NdotV = max(dot(N, V), 0.0);
            float NdotH = max(dot(N, H), 0.0);
            float VdotH = max(dot(V, H), 0.0);

            if (NdotL > 0 && NdotV > 0)
            {
                float D = D_GGX(NdotH, roughness);
                float G = G_Smith(NdotV, NdotL, roughness);
                float3 F = F_Schlick(VdotH, F0);
                
                float3 spec = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);
                float3 kS = F;
                float3 kD = (1.0 - kS) * (1.0 - metallic);
                float3 diff = kD * albedo / 3.14159265f;

                payload.radiance += payload.throughput * (diff + spec) * sunColor * NdotL;
            }
        }
    }

    // --- Indirect Lighting (Sampling) ---
    float3 b1, b2;
    Onb(N, b1, b2);

    float3 nextDir;
    float specProb = 0.5f; // Simplified
    if (RandomFloat(payload.seed) < specProb)
    {
        // Sample GGX
        float2 r = float2(RandomFloat(payload.seed), RandomFloat(payload.seed));
        float a = roughness * roughness;
        float phi = 2.0 * 3.14159265 * r.x;
        float cosTheta = sqrt((1.0 - r.y) / (1.0 + (a * a - 1.0) * r.y));
        float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
        
        float3 H = float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
        H = H.x * b1 + H.y * b2 + H.z * N;
        nextDir = reflect(-V, H);
        
        float NdotL = max(dot(N, nextDir), 0.0);
        float NdotV = max(dot(N, V), 0.0);
        if (NdotL > 0 && NdotV > 0)
        {
            float3 H_ = normalize(V + nextDir);
            float VdotH = max(dot(V, H_), 0.0);
            float3 F = F_Schlick(VdotH, F0);
            float G = G_Smith(NdotV, NdotL, roughness);
            // Weight = (F*G*D)/(4*NoV*NoL) * (4*VoH)/(D*NoH) / specProb
            payload.throughput *= (F * G * VdotH) / (NdotV * max(dot(N, H_), 0.001) * specProb + 0.001);
        }
        else payload.depth = -1;
    }
    else
    {
        // Sample Diffuse
        float3 localDir = RandomCosineDirection(payload.seed);
        nextDir = localDir.x * b1 + localDir.y * b2 + localDir.z * N;
        payload.throughput *= albedo / (1.0 - specProb);
    }
    
    payload.nextOrigin = worldPos + N * 0.001f;
    payload.nextDirection = normalize(nextDir);
}
