RWStructuredBuffer<uint> InputBuffer : register(u0, space0);
RWStructuredBuffer<uint> OutputBuffer : register(u1, space0);

[numthreads(256, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {
    uint index = id.x;
    // Simple copy to verify compute execution
    OutputBuffer[index] = InputBuffer[index] * 2;
}
