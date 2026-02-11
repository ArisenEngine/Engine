
struct RayPayload
{
    float3 color;
    float distance;
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

struct PrimitiveData
{
    int materialIndex;
    int indexOffset;
    int indexCount;
    int vertexOffset;
};

RaytracingAccelerationStructure Scene : register(t0, space0);
RWTexture2D<float4> RenderTarget : register(u1, space0);

cbuffer CameraBuffer : register(b2, space0)
{
    float4x4 viewInverse;
    float4x4 projInverse;
    float3 lightPos;
    int frameCount;
};

StructuredBuffer<GLTFVertex> Vertices : register(t3, space0);
StructuredBuffer<uint> Indices : register(t4, space0);
StructuredBuffer<MaterialData> Materials : register(t5, space0);
StructuredBuffer<PrimitiveData> Primitives : register(t6, space0);
Texture2D ModelTextures[100] : register(t7, space0);
SamplerState DefaultSampler : register(s8, space0);

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

[shader("raygeneration")]
void RayGen()
{
    uint3 launchID = DispatchRaysIndex();
    uint3 launchSize = DispatchRaysDimensions();

    float2 d = (((float2)launchID.xy + 0.5f) / (float2)launchSize.xy) * 2.f - 1.f;
    // d.y = -d.y; // Removed: Fixed vertical flip by removing double flip

    float4 target = mul(projInverse, float4(d.x, d.y, 1, 1));
    target.xyz /= target.w;
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
    payload.color = t * float3(0.5, 0.7, 1.0) + (1.0 - t) * float3(1.0, 1.0, 1.0);
    payload.hit = false;
}

[shader("closesthit")]
void ClosestHit(inout RayPayload payload, in BuiltInTriangleIntersectionAttributes attr)
{
    uint primIndex = PrimitiveIndex();
    uint instIndex = InstanceIndex();
    
    PrimitiveData prim = Primitives[primIndex];
    MaterialData mat = Materials[prim.materialIndex];
    
    // Fetch indices
    uint i0 = Indices[prim.indexOffset + PrimitiveIndex() * 3 + 0];
    uint i1 = Indices[prim.indexOffset + PrimitiveIndex() * 3 + 1];
    uint i2 = Indices[prim.indexOffset + PrimitiveIndex() * 3 + 2];
    
    GLTFVertex v0 = Vertices[i0];
    GLTFVertex v1 = Vertices[i1];
    GLTFVertex v2 = Vertices[i2];
    
    float3 barycentrics = float3(1.0 - attr.barycentrics.x - attr.barycentrics.y, attr.barycentrics.x, attr.barycentrics.y);
    
    float2 uv = v0.uv * barycentrics.x + v1.uv * barycentrics.y + v2.uv * barycentrics.z;
    float3 normal = normalize(v0.normal * barycentrics.x + v1.normal * barycentrics.y + v2.normal * barycentrics.z);
    float3 worldPos = v0.pos * barycentrics.x + v1.pos * barycentrics.y + v2.pos * barycentrics.z;
    
    float4 baseColor = mat.baseColorFactor;
    if (mat.baseColorTextureIndex >= 0)
    {
        baseColor *= ModelTextures[mat.baseColorTextureIndex].SampleLevel(DefaultSampler, uv, 0);
    }
    
    // Simple Lighting
    float3 L = normalize(lightPos - worldPos);
    float3 V = normalize(WorldRayOrigin() - worldPos);
    float3 H = normalize(V + L);
    
    float nDotL = max(dot(normal, L), 0.0);
    float3 diffuse = baseColor.rgb * nDotL;
    
    // Ambient
    float3 ambient = baseColor.rgb * 0.1;
    
    payload.color = ambient + diffuse;
    payload.hit = true;
}
