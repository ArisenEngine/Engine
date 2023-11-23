using System.Diagnostics;
using GameTest;
using NebulaEngine;
using NebulaEngine.Debugger;
using Debugger = NebulaEngine.API.Debugger;

const string RenderPipelineAssetPath = "./CustomRenderPipelineAsset";

var customPipelineAsset =
    Serialization.SerializationUtil.Deserialize<CustomRenderPipelineAsset>(RenderPipelineAssetPath);

Debug.Assert(customPipelineAsset != null);

Thread.CurrentThread.Name = "MainThread";


//Debugger.Debugger_Log("Start", Thread.CurrentThread.Name);
//Debugger.Debugger_Info("Start", Thread.CurrentThread.Name);
//Debugger.Debugger_Trace("Start", Thread.CurrentThread.Name);
//Debugger.Debugger_Warning("Start", Thread.CurrentThread.Name);
//Debugger.Debugger_Error("Start", Thread.CurrentThread.Name);
//Debugger.Debugger_Fatal("Start", Thread.CurrentThread.Name);

//Task.Run(() =>
//{

//    Debugger.Debugger_Log("Task", Thread.CurrentThread.Name);
//    Debugger.Debugger_Info("Task", Thread.CurrentThread.Name);
//    Debugger.Debugger_Trace("Task", Thread.CurrentThread.Name);
//    Debugger.Debugger_Warning("Task", Thread.CurrentThread.Name);
//    Debugger.Debugger_Error("Task", Thread.CurrentThread.Name);
//    Debugger.Debugger_Fatal("Task", Thread.CurrentThread.Name);

//});


Logger.Log("Start", Thread.CurrentThread.Name);
Logger.Info("Start", Thread.CurrentThread.Name);
Logger.Trace("Start", Thread.CurrentThread.Name);
Logger.Warning("Start", Thread.CurrentThread.Name);
Logger.Error("Start", Thread.CurrentThread.Name);
Logger.Fatal("Start", Thread.CurrentThread.Name);

Task.Run(() =>
{

    Logger.Log("Task", Thread.CurrentThread.Name);
    Logger.Info("Task", Thread.CurrentThread.Name);
    Logger.Trace("Task", Thread.CurrentThread.Name);
    Logger.Warning("Task", Thread.CurrentThread.Name);
    Logger.Error("Task", Thread.CurrentThread.Name);
    Logger.Fatal("Task", Thread.CurrentThread.Name);

});

NebulaEngine.Rendering.Graphics.SetCurrentRenderPipeline(customPipelineAsset);
int code = NebulaApplication.Run(1920, 1080, "Game Test");

Serialization.SerializationUtil.Serialize(customPipelineAsset, RenderPipelineAssetPath);

Environment.Exit(code);