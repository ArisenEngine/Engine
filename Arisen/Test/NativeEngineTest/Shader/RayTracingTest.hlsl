
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
StructuredBuffer<uint> TriangleMaterialIndices : register(t6, space0);
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
        float4 prevSample = AccumulationTarget[launchID.xy];
        float3 prevColor = prevSample.rgb;
        
        // Stabilize: if prevColor is NaN or extreme, ignore it
        if (any(isnan(prevColor)) || any(isinf(prevColor))) prevColor = finalColor;

        float weight = 1.0f / (float)(frameCount + 1);
        finalColor = lerp(prevColor, finalColor, weight);
    }
    
    // Guard against NaNs in final result
    if (any(isnan(finalColor)) || any(isinf(finalColor))) finalColor = float3(0, 0, 0);

    AccumulationTarget[launchID.xy] = float4(finalColor, 1.0);

    // Final Post-processing
    float3 postProcessed = ACESToneMapping(finalColor);
    postProcessed = LinearToSRGB(postProcessed);
    
    RenderTarget[launchID.xy] = float4(postProcessed, 1.0);
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
    
    // --- Improved Interpolation & Offsetting ---
    uint i0 = Indices[triIndex * 3 + 0];
    uint i1 = Indices[triIndex * 3 + 1];
    uint i2 = Indices[triIndex * 3 + 2];
    GLTFVertex v0 = Vertices[i0];
    GLTFVertex v1 = Vertices[i1];
    GLTFVertex v2 = Vertices[i2];
    
    float3 bary = float3(1.0 - attr.barycentrics.x - attr.barycentrics.y, attr.barycentrics.x, attr.barycentrics.y);
    float2 uv = v0.uv * bary.x + v1.uv * bary.y + v2.uv * bary.z;
    float3 N = normalize(v0.normal * bary.x + v1.normal * bary.y + v2.normal * bary.z);
    float3 worldPos = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
    
    // Geometric normal for robust offsetting to fix antifacet (shadow acne)
    float3 geoN = normalize(cross(v1.pos - v0.pos, v2.pos - v0.pos));
    if (dot(geoN, -WorldRayDirection()) < 0) geoN = -geoN; // Ray side

    float4 baseColor = mat.baseColorFactor;
    if (mat.baseColorTextureIndex >= 0)
    {
        baseColor *= ModelTextures[mat.baseColorTextureIndex].SampleLevel(DefaultSampler, uv, 0);
    }
    
    // Consistent linearization of all albedo data
    float3 albedo = pow(max(baseColor.rgb, 0.0), 2.2f);
    
    float metallic = mat.metallicFactor;
    float roughness = mat.roughnessFactor;
    float3 V = -WorldRayDirection();

    const float PI = 3.14159265f;

    // --- Direct Lighting (NEE) for Sun ---
    {
        float3 sunDir = normalize(lightPosAndFrameCount.xyz);
        float3 sunColor = float3(15.0, 10.0, 5.0);
        float dotNL = max(dot(N, sunDir), 0.0);
        if (dotNL > 0)
        {
            RayDesc shadowRay;
            shadowRay.Origin = worldPos + geoN * 0.05f; // Stronger bias with geoN
            shadowRay.Direction = sunDir;
            shadowRay.TMin = 0.0f;
            shadowRay.TMax = 10000.0f;

            ShadowPayload sPayload;
            sPayload.hit = true;
            TraceRay(Scene, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER | RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 1, shadowRay, sPayload);

            if (!sPayload.hit)
            {
                // Lambertian BRDF is albedo/PI
                payload.radiance += payload.throughput * (albedo / PI) * sunColor * dotNL;
            }
        }
    }

    // --- Direct Lighting (NEE) for Point Lights ---
    for (int i = 0; i < numPointLights; ++i)
    {
        PointLight light = pointLights[i];
        float3 lightDir = light.posRange.xyz - worldPos;
        float d2 = dot(lightDir, lightDir);
        float dist = sqrt(d2);
        lightDir /= dist;

        float dotNL = max(dot(N, lightDir), 0.0);
        if (dotNL > 0)
        {
            RayDesc shadowRay;
            shadowRay.Origin = worldPos + geoN * 0.05f; 
            shadowRay.Direction = lightDir;
            shadowRay.TMin = 0.0f;
            shadowRay.TMax = dist - 0.05f;

            ShadowPayload sPayload;
            sPayload.hit = true;
            TraceRay(Scene, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER | RAY_FLAG_FORCE_OPAQUE, 0xFF, 0, 1, 1, shadowRay, sPayload);

            if (!sPayload.hit)
            {
                float attenuation = light.colorInt.w / (d2 + 1.0);
                payload.radiance += payload.throughput * (albedo / PI) * light.colorInt.xyz * dotNL * attenuation;
            }
        }
    }

    // --- Path Scattering ---
    bool isMetal = metallic > 0.5;
    if (isMetal)
    {
        float3 reflected = reflect(WorldRayDirection(), N);
        payload.nextDirection = normalize(reflected + roughness * RandomInUnitSphere(payload.seed));
        payload.throughput *= albedo;
    }
    else
    {
        // For Lambertian, PDF = cos(theta)/PI
        // BRDF = albedo/PI
        // weight = BRDF * cos(theta) / PDF = (albedo/PI) * cos(theta) / (cos(theta)/PI) = albedo
        float3 scatterDir = N + RandomUnitVector(payload.seed);
        if (IsNearZero(scatterDir)) scatterDir = N;
        payload.nextDirection = normalize(scatterDir);
        payload.throughput *= albedo; 
    }
    
    payload.nextOrigin = worldPos + geoN * 0.05f;
}
