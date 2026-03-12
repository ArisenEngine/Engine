# RenderGraph Spec

The RenderGraph is the high-level declarative API used to define the rendering pipeline in C#. It abstracts away Vulkan/DX12 complexities like image barriers and resource aliasing.

## 1. Declarative Passes
Instead of calling RHI commands directly, you define "Passes".

```csharp
renderGraph.AddPass("ShadowPass", (ctx) => {
    ctx.WriteTexture(shadowMap);
    ctx.Execute((cmd) => {
        // Traditional RHI draw calls here
    });
});
```

## 2. Automatic Synchronization
The RenderGraph analyzes the dependencies (Who writes to what? Who reads it later?) and automatically:
- Inserts **Memory Barriers** and Image Layout Transitions.
- Manages **Resource Lifetime** (Aliasing buffers that aren't used at the same time).
- Dispatches work to the appropriate Hardware Queues (Graphics/Compute/Transfer).

## 3. Constraints
- **State Leakage**: Passes must be independent. Do not rely on RHI state (like Bound Pipelines) persisting from a previous pass.
- **Resource Management**: Only use resources registered with the graph to ensure proper dependency tracking.
