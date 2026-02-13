
struct RayPayloadFixed
{
    float3 radiance;
    float3 throughput;
    float3 nextOrigin;
    float3 nextDir;
    uint seed;
    bool stop;
};

struct ShadowPayload
{
    bool hit;
};

struct GLTFVertex
{
    float4 pos;
    float4 normal;
    float2 uv;
    float2 padding;
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
    column_major float4x4 viewInverse;
    column_major float4x4 projInverse;
    float4 cameraPos;
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

const static float PI = 3.1415926535f;

// --- PBR Helpers ---
float D_GGX(float3 N, float3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    return num / max(denom, 0.0001);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return num / denom;
}

float G_Smith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

float3 F_Schlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

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

// --- Sampling Helpers ---
float3 SampleHemisphere(float3 N, inout uint seed)
{
    float r1 = RandomFloat(seed);
    float r2 = RandomFloat(seed);
    
    float phi = 2.0 * PI * r1;
    float cosTheta = sqrt(1.0 - r2);
    float sinTheta = sqrt(r2);
    
    float3 localDir;
    localDir.x = cos(phi) * sinTheta;
    localDir.y = sin(phi) * sinTheta;
    localDir.z = cosTheta;
    
    // TBN basis
    float3 up = abs(N.z) < 0.999 ? float3(0, 0, 1) : float3(1, 0, 0);
    float3 tangent = normalize(cross(up, N));
    float3 bitangent = cross(N, tangent);
    
    return tangent * localDir.x + bitangent * localDir.y + N * localDir.z;
}

// --- Tonemapping Helpers ---
float3 ACESFilm(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
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
    float3 rayOrigin = cameraPos.xyz;

    float3 totalRadiance = 0;
    float3 currentThroughput = 1;
    
    RayDesc ray;
    ray.Origin = rayOrigin;
    ray.Direction = rayDir;
    ray.TMin = 0.001;
    ray.TMax = 10000.0;

    for (int bounce = 0; bounce < 4; ++bounce)
    {
        RayPayloadFixed payload;
        payload.radiance = 0;
        payload.throughput = 1;
        payload.stop = true;
        payload.seed = seed;

        TraceRay(Scene, RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 0, 0, ray, payload);
        
        totalRadiance += payload.radiance * currentThroughput;
        currentThroughput *= payload.throughput;
        seed = payload.seed;

        if (payload.stop)
            break;

        ray.Origin = payload.nextOrigin;
        ray.Direction = payload.nextDir;
        ray.TMin = 0.001;
    }

    float3 currentRadiance = totalRadiance;
    
    float3 accumulatedColor = currentRadiance;
    if (frameCount > 0)
    {
        float3 prevColor = AccumulationTarget[launchID.xy].rgb;
        accumulatedColor = lerp(prevColor, currentRadiance, 1.0 / (float(frameCount) + 1.0));
    }
    AccumulationTarget[launchID.xy] = float4(accumulatedColor, 1.0);
    
    // Tonemapping and SRGB
    float3 finalColor = ACESFilm(accumulatedColor);
    finalColor = pow(finalColor, 1.0 / 2.2);

    RenderTarget[launchID.xy] = float4(finalColor, 1.0);
}

[shader("miss")]
void Miss(inout RayPayloadFixed payload)
{
    float3 rayDir = WorldRayDirection();
    float t = 0.5 * (rayDir.y + 1.0);
    float3 skyColor = lerp(float3(1.0, 1.0, 1.0), float3(0.5, 0.7, 1.0), t);
    payload.radiance = skyColor * 0.5; // Scale down for a more natural look
    payload.stop = true;
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
    
    MaterialData mat = Materials[matIndex];
    
    uint i0 = Indices[baseIndex + triIndex * 3 + 0];
    uint i1 = Indices[baseIndex + triIndex * 3 + 1];
    uint i2 = Indices[baseIndex + triIndex * 3 + 2];
    
    GLTFVertex v0 = Vertices[i0];
    GLTFVertex v1 = Vertices[i1];
    GLTFVertex v2 = Vertices[i2];
    
    float3 bary = float3(1.0 - attr.barycentrics.x - attr.barycentrics.y, attr.barycentrics.x, attr.barycentrics.y);
    float2 uv = v0.uv * bary.x + v1.uv * bary.y + v2.uv * bary.z;
    float3 normalOS = normalize(v0.normal.xyz * bary.x + v1.normal.xyz * bary.y + v2.normal.xyz * bary.z);
    float3 normalWS = normalize(mul((float3x3)ObjectToWorld3x4(), normalOS));

    float4 baseColor = mat.baseColorFactor;
    if (mat.baseColorTextureIndex >= 0)
    {
        baseColor *= ModelTextures[mat.baseColorTextureIndex].SampleLevel(DefaultSampler, uv, 0);
    }

    float3 worldPos = WorldRayOrigin() + RayTCurrent() * WorldRayDirection();
    float3 V = normalize(WorldRayOrigin() - worldPos);
    float3 N = normalWS;

    float roughness = mat.roughnessFactor;
    float metallic = mat.metallicFactor;

    float3 F0 = float3(0.04, 0.04, 0.04);
    F0 = lerp(F0, baseColor.rgb, metallic);

    // --- Direct Lighting (Next Event Estimation) ---
    float3 Lo = float3(0, 0, 0);
    for (int i = 0; i < numPointLights; ++i)
    {
        float3 lightPos = pointLights[i].posRange.xyz;
        float range = pointLights[i].posRange.w;
        float3 lightColor = pointLights[i].colorInt.xyz;
        float intensity = pointLights[i].colorInt.w;

        float3 L = normalize(lightPos - worldPos);
        float3 H = normalize(V + L);

        float distance = length(lightPos - worldPos);
        float attenuation = 1.0 / (distance * distance);
        attenuation *= pow(max(0.0, 1.0 - pow(distance / range, 4.0)), 2.0);
        float3 lightRadiance = lightColor * intensity * attenuation;

        // Shadow ray
        float3 shadowRayOrigin = worldPos + N * 0.001;
        float3 shadowRayDir = L;
        
        RayDesc shadowRay;
        shadowRay.Origin = shadowRayOrigin;
        shadowRay.Direction = shadowRayDir;
        shadowRay.TMin = 0.001;
        shadowRay.TMax = distance - 0.001;

        ShadowPayload shadowPayload;
        shadowPayload.hit = true; 

        TraceRay(Scene, 
                 RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH, 
                 0xFF, 0, 0, 1, shadowRay, shadowPayload);

        if (!shadowPayload.hit)
        {
            float NDF = D_GGX(N, H, roughness);
            float G = G_Smith(N, V, L, roughness);
            float3 F = F_Schlick(max(dot(H, V), 0.0), F0);

            float3 kS = F;
            float3 kD = float3(1.0, 1.0, 1.0) - kS;
            kD *= 1.0 - metallic;

            float3 numerator = NDF * G * F;
            float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
            float3 specular = numerator / denominator;

            float NdotL = max(dot(N, L), 0.0);
            Lo += (kD * baseColor.rgb / PI + specular) * lightRadiance * NdotL;
        }
    }

    payload.radiance = Lo;

    // --- Indirect Lighting (Path Continuation) ---
    float r = RandomFloat(payload.seed);
    float3 kS = F_Schlick(max(dot(N, V), 0.0), F0);
    float specProbability = (metallic + (1.0 - metallic) * dot(kS, float3(0.33, 0.33, 0.33)));
    
    if (r < specProbability)
    {
        // Specular reflection (pure reflection for now, can add roughness later)
        payload.nextDir = reflect(-V, N);
        payload.throughput = F_Schlick(max(dot(N, payload.nextDir), 0.0), F0);
        // Divide by probability
        payload.throughput /= specProbability;
    }
    else
    {
        // Diffuse bounce
        payload.nextDir = SampleHemisphere(N, payload.seed);
        float3 kD = (float3(1.0, 1.0, 1.0) - kS) * (1.0 - metallic);
        payload.throughput = kD * baseColor.rgb; // (kD * alb / PI) * cosL / (cosL / PI) = kD * alb
        // Divide by probability
        payload.throughput /= (1.0 - specProbability);
    }
    
    payload.nextOrigin = worldPos + N * 0.001;
    payload.stop = false;
}
