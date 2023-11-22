using System.Diagnostics;
using GameTest;
using NebulaEngine;
using Debugger = NebulaEngine.API.Debugger;

const string RenderPipelineAssetPath = "./CustomRenderPipelineAsset";

var customPipelineAsset =
    Serialization.SerializationUtil.Deserialize<CustomRenderPipelineAsset>(RenderPipelineAssetPath);

Debug.Assert(customPipelineAsset != null);

Debugger.Debugger_Log("Start");

NebulaEngine.Rendering.Graphics.SetCurrentRenderPipeline(customPipelineAsset);
int code = NebulaApplication.Run(1920, 1080, "Game Test");

Serialization.SerializationUtil.Serialize(customPipelineAsset, RenderPipelineAssetPath);

Environment.Exit(code);