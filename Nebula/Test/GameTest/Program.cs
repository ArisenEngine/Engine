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

var now = DateTime.Now;
Logger.Log("Start");
Logger.Info("Start");
Logger.Trace("Start");
Logger.Warning("Start");
Logger.Error("Start");
Logger.Fatal("Start");

var cost = DateTime.Now - now;

Task.Run(() =>
{

    Logger.Log("Task");
    Logger.Info("Task");
    Logger.Trace("Task");
    Logger.Warning("Task");
    Logger.Error("Task");
    Logger.Fatal("Task");

});

NebulaEngine.Rendering.Graphics.SetCurrentRenderPipeline(customPipelineAsset);
int code = NebulaApplication.Run(1920, 1080, "Game Test");

Serialization.SerializationUtil.Serialize(customPipelineAsset, RenderPipelineAssetPath);

Environment.Exit(code);