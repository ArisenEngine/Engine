
struct RayPayload
{
    float3 color;
    float distance;
    bool hit;
};

RaytracingAccelerationStructure Scene : register(t0, space0);
RWTexture2D<float4> RenderTarget : register(u1, space0);

cbuffer CameraBuffer : register(b2, space0)
{
    float4x4 viewInverse;
    float4x4 projInverse;
    float3 lightPos;
    float padding;
};

// Simple PBR functions
float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

float DistributionGGX(float3 N, float3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = 3.14159 * denom * denom;

    return num / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return num / denom;
}

float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

[shader("raygen")]
void RayGen()
{
    uint3 launchID = DispatchRaysIndex();
    uint3 launchSize = DispatchRaysDimensions();

    float2 d = (((float2)launchID.xy + 0.5f) / (float2)launchSize.xy) * 2.f - 1.f;

    float4 target = mul(projInverse, float4(d.x, d.y, 1, 1));
    float3 rayDir = mul(viewInverse, float4(normalize(target.xyz), 0)).xyz;
    float3 rayOrigin = viewInverse[3].xyz;

    RayDesc ray;
    ray.Origin = rayOrigin;
    ray.Direction = rayDir;
    ray.TMin = 0.001;
    ray.TMax = 10000.0;

    RayPayload payload;
    payload.color = float3(0, 0, 0);
    payload.distance = -1.0;
    payload.hit = false;

    TraceRay(Scene, RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 0, ray, payload);

    RenderTarget[launchID.xy] = float4(payload.color, 1.0);
}

[shader("miss")]
void Miss(inout RayPayload payload)
{
    float3 rayDir = WorldRayDirection();
    float t = 0.5 * (rayDir.y + 1.0);
    payload.color = (1.0 - t) * float3(1.0, 1.0, 1.0) + t * float3(0.5, 0.7, 1.0);
    payload.hit = false;
}

[shader("closesthit")]
void ClosestHit(inout RayPayload payload, in BuiltInTriangleIntersectionAttributes attr)
{
    float3 worldNormal = WorldRayDirection(); // Placeholder, need actual normal
    // In a real hit shader, we would fetch vertex data here.
    // To keep it simple for the first test, use a procedural normal or constant for now,
    // or pass vertex buffers.
    
    // For now, let's just use the barycentrics to show something.
    float3 barycentrics = float3(1.0 - attr.barycentrics.x - attr.barycentrics.y, attr.barycentrics.x, attr.barycentrics.y);
    
    float3 N = normalize(cross(WorldRayDirection(), float3(0, 1, 0))); // Very fake normal
    // We will improve this by passing vertex buffers later.
    
    float3 V = -WorldRayDirection();
    float3 L = normalize(lightPos - WorldRayPosition());
    float3 H = normalize(V + L);

    float3 baseColor = float3(0.8, 0.8, 0.8);
    float metallic = 0.5;
    float roughness = 0.2;

    float3 F0 = float3(0.04, 0.04, 0.04);
    F0 = lerp(F0, baseColor, metallic);

    // BRDF
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    float3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    float3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
    float3 specular = numerator / denominator;

    float3 kS = F;
    float3 kD = float3(1.0, 1.0, 1.0) - kS;
    kD *= 1.0 - metallic;

    float nDotL = max(dot(N, L), 0.0);
    float3 ambient = float3(0.03, 0.03, 0.03) * baseColor;
    
    payload.color = (kD * baseColor / 3.14159 + specular) * nDotL + ambient;
    payload.hit = true;
}
