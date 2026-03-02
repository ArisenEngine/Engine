using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Packages;
using CSharpEngineTest.Framework;
using System.Linq;

namespace CSharpEngineTest.Core.Packages;

public class PackageSystemTests : ITest
{
    public string GetName() => "Package System Tests";
    public TestCategory GetCategory() => TestCategory.Framework;

    public bool Setup()
    {
        var kernel = EngineKernel.Instance;
        kernel.Reset();
        kernel.RegisterSubsystem(new PackageSubsystem());
        kernel.Initialize(new EngineConfig { AppName = "PackageSystemTest" });
        return true;
    }

    public void Teardown()
    {
        EngineKernel.Instance.Shutdown();
    }

    public bool Run()
    {
        return TestForwardRPLoading();
    }

    private bool TestForwardRPLoading()
    {
        Logger.Log("Testing ForwardRP Package Loading...");
        var packageSubsystem = EngineKernel.Instance.GetSubsystem<PackageSubsystem>();
        
        if (packageSubsystem == null)
        {
            Logger.Error("PackageSubsystem not found in Kernel!");
            return false;
        }

        const string forwardRPId = "com.arisen.builtin.forward-rp";
        var forwardRP = packageSubsystem.GetAllPackages().FirstOrDefault(p => p.Id == forwardRPId);

        if (forwardRP == null)
        {
            Logger.Error($"Package '{forwardRPId}' not found!");
            return false;
        }

        Logger.Log($"Package '{forwardRPId}' is loaded successfully.");
        Logger.Log($"  Name: {forwardRP.Name}");
        Logger.Log($"  Version: {forwardRP.Version}");
        Logger.Log($"  Assembly: {forwardRP.Assembly.FullName}");
        Logger.Log($"  RootPath: {forwardRP.RootPath}");

        if (forwardRP.EntryInstance == null)
        {
            Logger.Error("ForwardRP EntryInstance is null!");
            return false;
        }

        Logger.Log($"ForwardRP EntryInstance: {forwardRP.EntryInstance.GetType().FullName}");

        // Check if it's the expected class
        const string expectedClass = "ArisenEngine.Rendering.ForwardRenderPipelineAsset";
        if (forwardRP.EntryInstance.GetType().FullName != expectedClass)
        {
            Logger.Error($"ForwardRP EntryInstance type mismatch. Expected {expectedClass}, got {forwardRP.EntryInstance.GetType().FullName}");
            return false;
        }

        return true;
    }
}
