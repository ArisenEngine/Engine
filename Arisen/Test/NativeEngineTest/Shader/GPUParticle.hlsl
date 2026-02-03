struct Particle {
    float4 position;
    float4 velocity;
};

// Compute Shader
RWStructuredBuffer<Particle> Particles : register(u0, space0);

[numthreads(256, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {
    uint index = id.x;
    if (index >= 10000) return; // Assume 10000 particles

    Particles[index].position.xyz += Particles[index].velocity.xyz * 0.016; // 60 FPS approx

    // Basic wrap around
    if (Particles[index].position.x > 5.0) Particles[index].position.x = -5.0;
    if (Particles[index].position.x < -5.0) Particles[index].position.x = 5.0;
    if (Particles[index].position.y > 5.0) Particles[index].position.y = -5.0;
    if (Particles[index].position.y < -5.0) Particles[index].position.y = 5.0;
    if (Particles[index].position.z > 5.0) Particles[index].position.z = -5.0;
    if (Particles[index].position.z < -5.0) Particles[index].position.z = 5.0;
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

float4x4 model;
float4x4 view;
float4x4 projection;

VSOutput VSMain(VSInput input) {
    VSOutput output;
    float3 pos = ParticlesRead[input.vertexID].position.xyz;
    
    float4 worldPos = mul(model, float4(pos, 1.0));
    float4 viewPos = mul(view, worldPos);
    output.position = mul(projection, viewPos);
    
    output.color = float4(0.5, 0.8, 1.0, 1.0);
    output.pointSize = 4.0f;
    return output;
}

float4 PSMain(PSInput input) : SV_Target {
    return input.color;
}
