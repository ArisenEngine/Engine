struct Particle {
    float4 position; // xyz, w = life
    float4 velocity; // xyz, w = maxLife
};

// Compute Shader
RWStructuredBuffer<Particle> Particles : register(u0, space0);

cbuffer UBO : register(b1, space0) {
    float4x4 model;
    float4x4 view;
    float4x4 projection;
    float mipmapBias;
    float time;
    float deltaTime;
};

// Pseudorandom function
float rand(float2 co) {
    return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
}

[numthreads(256, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {
    uint index = id.x;
    if (index >= 1000000) return;

    Particle p = Particles[index];

    // Update life
    p.position.w -= deltaTime;

    if (p.position.w <= 0) {
        // Respawn
        float r1 = rand(float2(index * 0.1, time));
        float r2 = rand(float2(time, index * 0.1));
        float r3 = rand(float2(index * 0.5, time * 0.3));
        
        p.position.xyz = float3((r1 - 0.5) * 2.0, -2.0, (r2 - 0.5) * 2.0);
        p.position.w = r3 * 3.0 + 1.0; // Life 1-4 seconds
        
        p.velocity.xyz = float3((r1 - 0.5) * 0.5, 1.0 + r2, (r2 - 0.5) * 0.5);
        p.velocity.w = p.position.w; // Store maxLife
    } else {
        // Apply velocity with some turbulence
        float r = rand(float2(index, time * 0.01));
        p.velocity.x += (r - 0.5) * 0.1;
        p.velocity.z += (rand(float2(time * 0.01, index)) - 0.5) * 0.1;
        
        p.position.xyz += p.velocity.xyz * deltaTime;
        
        // Buoyancy/Upward acceleration
        p.velocity.y += 0.5 * deltaTime;
    }

    Particles[index] = p;
}

// Graphics Shaders
struct VSInput {
    uint vertexID : SV_VertexID;
};

struct VSOutput {
    float4 position : SV_Position;
    float4 color : COLOR;
    [[vk::builtin("PointSize")]] float pointSize : PSIZE;
};

struct PSInput {
    float4 position : SV_Position;
    float4 color : COLOR;
};

StructuredBuffer<Particle> ParticlesRead : register(t0, space0);

VSOutput VSMain(VSInput input) {
    VSOutput output;
    Particle p = ParticlesRead[input.vertexID];
    
    float4 worldPos = mul(model, float4(p.position.xyz, 1.0));
    float4 viewPos = mul(view, worldPos);
    output.position = mul(projection, viewPos);
    
    float lifeRatio = saturate(p.position.w / p.velocity.w);
    
    // Fire color: White -> Yellow -> Orange -> Red -> Black/Smoke
    float3 color;
    if (lifeRatio > 0.8) {
        color = lerp(float3(1.0, 1.0, 0.5), float3(1.0, 1.0, 1.0), (lifeRatio - 0.8) * 5.0);
    } else if (lifeRatio > 0.5) {
        color = lerp(float3(1.0, 0.6, 0.0), float3(1.0, 1.0, 0.5), (lifeRatio - 0.5) * 3.33);
    } else if (lifeRatio > 0.2) {
        color = lerp(float3(1.0, 0.1, 0.0), float3(1.0, 0.6, 0.0), (lifeRatio - 0.2) * 3.33);
    } else {
        color = lerp(float3(0.1, 0.02, 0.0), float3(1.0, 0.1, 0.0), lifeRatio * 5.0);
    }
    
    output.color = float4(color, lifeRatio);
    output.pointSize = 2.0 * lifeRatio + 1.0;
    
    return output;
}

float4 PSMain(PSInput input) : SV_Target {
    return input.color;
}
