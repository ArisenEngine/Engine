
struct VSInput {
    float3 pos : POSITION;
    float2 uv : TEXCOORD0;
};

struct VSOutput {
    float3 pos : POSITION;
    float2 uv : TEXCOORD0;
};

struct HSConstantOutput {
    float edges[4] : SV_TessFactor;
    float inside[2] : SV_InsideTessFactor;
};

struct HSOutput {
    float3 pos : POSITION;
    float2 uv : TEXCOORD0;
};

struct DSOutput {
    float4 pos : SV_Position;
    float2 uv : TEXCOORD0;
    float height : TEXCOORD1;
    float3 worldPos : POSITION;
    float3 normal : NORMAL;
};

cbuffer UBO : register(b0, space0) {
    float4x4 model;
    float4x4 view;
    float4x4 projection;
    float time;
    float tessLevel;
    float waveAmplitude;
    float waveFrequency;
};

// --- Vertex Shader ---
VSOutput vs_main(VSInput input) {
    VSOutput output;
    output.pos = input.pos;
    output.uv = input.uv;
    return output;
}

// --- Hull Shader ---
HSConstantOutput hs_constant_main(InputPatch<VSOutput, 4> patch, uint patchID : SV_PrimitiveID) {
    HSConstantOutput output;
    
    // Distance-based tessellation could be added here
    // For now, use the UBO tessLevel
    float factor = tessLevel > 0 ? tessLevel : 1.0;
    
    output.edges[0] = factor;
    output.edges[1] = factor;
    output.edges[2] = factor;
    output.edges[3] = factor;
    output.inside[0] = factor;
    output.inside[1] = factor;
    
    return output;
}

[domain("quad")]
[partitioning("integer")]
[outputtopology("triangle_ccw")]
[outputcontrolpoints(4)]
[patchconstantfunc("hs_constant_main")]
HSOutput hs_main(InputPatch<VSOutput, 4> patch, uint i : SV_OutputControlPointID, uint patchID : SV_PrimitiveID) {
    HSOutput output;
    output.pos = patch[i].pos;
    output.uv = patch[i].uv;
    return output;
}

// --- Domain Shader ---
float calculate_wave(float2 p, float t) {
    float h = 0;
    h += sin(p.x * waveFrequency + t) * waveAmplitude;
    h += cos(p.y * waveFrequency * 0.8 + t * 1.2) * waveAmplitude * 0.5;
    h += sin((p.x + p.y) * waveFrequency * 0.5 + t * 0.7) * waveAmplitude * 0.3;
    return h;
}

float3 calculate_normal(float2 p, float t) {
    float eps = 0.01;
    float h = calculate_wave(p, t);
    float hx = calculate_wave(p + float2(eps, 0), t);
    float hy = calculate_wave(p + float2(0, eps), t);
    
    float3 dx = float3(eps, hx - h, 0);
    float3 dy = float3(0, hy - h, eps);
    return normalize(cross(dy, dx));
}

[domain("quad")]
DSOutput ds_main(HSConstantOutput input, float2 uv : SV_DomainLocation, const OutputPatch<HSOutput, 4> patch) {
    DSOutput output;
    
    // Bilinear interpolation
    float3 pos = lerp(
        lerp(patch[0].pos, patch[1].pos, uv.x),
        lerp(patch[3].pos, patch[2].pos, uv.x),
        uv.y
    );
    
    float2 tex = lerp(
        lerp(patch[0].uv, patch[1].uv, uv.x),
        lerp(patch[3].uv, patch[2].uv, uv.x),
        uv.y
    );
    
    // Apply wave displacement
    float h = calculate_wave(pos.xz, time);
    pos.y += h;
    
    float4 worldPos = mul(model, float4(pos, 1.0));
    output.worldPos = worldPos.xyz;
    output.pos = mul(projection, mul(view, worldPos));
    output.uv = tex;
    output.height = h;
    output.normal = mul((float3x3)model, calculate_normal(pos.xz, time));
    
    return output;
}

// --- Pixel Shader ---
float4 ps_main(DSOutput input) : SV_Target {
    float3 lightDir = normalize(float3(1.0, 1.0, 1.0));
    float diff = max(dot(input.normal, lightDir), 0.2);
    
    // Color based on height
    float3 deepColor = float3(0.0, 0.1, 0.4);
    float3 shallowColor = float3(0.0, 0.5, 0.8);
    float3 foamColor = float3(0.8, 0.9, 1.0);
    
    float h = (input.height / waveAmplitude + 1.0) * 0.5;
    float3 color = lerp(deepColor, shallowColor, h);
    if (h > 0.85) color = lerp(color, foamColor, (h - 0.85) * 6.6);
    
    return float4(color * diff, 1.0);
}
