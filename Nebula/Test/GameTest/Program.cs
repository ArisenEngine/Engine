using System.Diagnostics;
using GameTest;
using NebulaEngine;
using NebulaEngine.Rendering;

const string RenderPipelineAssetPath = "./CustomRenderPipelineAsset";

var customPipelineAsset =
    Serialization.SerializationUtil.Deserialize<CustomRenderPipelineAsset>(RenderPipelineAssetPath);

Debug.Assert(customPipelineAsset != null);


NebulaEngine.Rendering.Graphics.SetCurrentRenderPipeline(customPipelineAsset);
int code = NebulaApplication.Run(1280, 1080, "Game Test");

Serialization.SerializationUtil.Serialize(customPipelineAsset, RenderPipelineAssetPath);

Environment.Exit(code);