struct MeshUBO {
    float4x4 model;
    float4x4 view;
    float4x4 projection;
    float time;
    float3 padding;
};

ConstantBuffer<MeshUBO> ubo : register(b0);

struct VertexOut {
    float4 position : SV_Position;
    float3 color : COLOR;
};

struct MeshPayload {
    uint meshletIndex;
};

// --- Mesh Shader ---
#ifdef MESH_STAGE
[NumThreads(128, 1, 1)]
[OutputTopology("triangle")]
void MSMain(
    uint gtid : SV_GroupThreadID,
    uint gid : SV_GroupID,
    out vertices VertexOut verts[64],
    out indices uint3 tris[126]
) {
    const uint numVertices = 64;
    const uint numTriangles = 62; // Simple strip/fan for a circle/sphere slice

    SetMeshOutputCounts(numVertices, numTriangles);

    if (gtid < numVertices) {
        float angle = (float(gtid) / float(numVertices)) * 2.0 * 3.14159;
        float radius = 1.0 + 0.2 * sin(ubo.time * 5.0 + float(gid) * 0.5 + angle * 3.0);
        
        float3 pos;
        pos.x = cos(angle) * radius;
        pos.y = sin(angle) * radius;
        pos.z = float(gid) * 0.1 - 1.0;

        float4 worldPos = mul(ubo.model, float4(pos, 1.0));
        float4 viewPos = mul(ubo.view, worldPos);
        verts[gtid].position = mul(ubo.projection, viewPos);
        
        // Cool rainbow colors
        verts[gtid].color = float3(
            sin(ubo.time + angle) * 0.5 + 0.5,
            sin(ubo.time + angle + 2.0) * 0.5 + 0.5,
            sin(ubo.time + angle + 4.0) * 0.5 + 0.5
        );
    }

    if (gtid < numTriangles) {
        // Simple fan from vertex 0
        tris[gtid] = uint3(0, gtid + 1, gtid + 2);
    }
}
#endif

// --- Pixel Shader ---
#ifdef PIXEL_STAGE
float4 PSMain(VertexOut input) : SV_Target {
    return float4(input.color, 1.0);
}
#endif
