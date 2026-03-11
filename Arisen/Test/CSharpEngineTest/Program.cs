using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using CSharpEngineTest.Framework;
using CSharpEngineTest.RHI.Rendering;
using CSharpEngineTest.Core.Graph;
using CSharpEngineTest.Core.Memory;
using CSharpEngineTest.Core.Lifecycle;
using CSharpEngineTest.Core.Packages;
using CSharpEngineTest.RHI.Unit;

ArisenApplication.InitializeLogging();
Logger.Log("###### Start C# Engine RHI Test ######");

var config = new EngineConfig
{
    AppName = "CSharpEngineTest"
};
if (!ArisenApplication.InitializeEngine(config))
{
    Logger.Error("Failed to initialize engine.");
    ArisenApplication.ShutdownEngine();
    return;
}

TestRunner.RegisterTest<RHIBasicTriangleTest>();
TestRunner.RegisterTest<GraphTests>();
TestRunner.RegisterTest<MemoryTests>();
TestRunner.RegisterTest<LifecycleTests>();
TestRunner.RegisterTest<PackageSystemTests>();
TestRunner.RegisterTest<RHIAsyncComputeTest>();

try
{
    TestRunner.RunAllTests();
}
catch (Exception ex)
{
    Logger.Error($"Unhandled exception in TestRunner: {ex.Message}");
    Logger.Error(ex.StackTrace ?? "");
}

Logger.Log("###### End C# Engine RHI Test ######");
ArisenApplication.ShutdownEngine();