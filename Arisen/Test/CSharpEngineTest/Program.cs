using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;
using CSharpEngineTest.RHI.Rendering;

Logger.Initialize();
Logger.Log("###### Start C# Engine RHI Test ######");

TestRunner.RegisterTest<RHIBasicTriangleTest>();

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
Logger.Dispose();