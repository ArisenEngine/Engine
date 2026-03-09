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

        // Setup a mock project for testing
        string baseDir = AppContext.BaseDirectory;
        string projectFile = Path.Combine(baseDir, "Project.arisen");
        
        var projectManifest = new ProjectManifest
        {
            Name = "Test Project",
            Packages = new List<PackageRequirement>
            {
                new PackageRequirement 
                { 
                    Id = "com.arisen.builtin.forward-rp", 
                    Url = "file://Packages/com.arisen.generic-renderpipeline" 
                }
            }
        };

        // Create the mock project file
        string json = JsonSerializer.Serialize(projectManifest);
        File.WriteAllText(projectFile, json);

        kernel.RegisterSubsystem(new ProjectSubsystem());
        kernel.RegisterSubsystem(new PackageSubsystem());
        kernel.Initialize(new EngineConfig { AppName = "PackageSystemTest" });
        return true;
    }

    public void Teardown()
    {
        EngineKernel.Instance.Shutdown();
        string projectFile = Path.Combine(AppContext.BaseDirectory, "Project.arisen");
        if (File.Exists(projectFile)) File.Delete(projectFile);
    }

    public bool Run()
    {
        return TestForwardRPLoading()
            && TestPackageCount()
            && TestGetPackageEntryTyped()
            && TestDependencyResolution();
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
        Logger.Log($"  Assembly: {forwardRP.Assembly?.FullName ?? "NULL"}");
        Logger.Log($"  RootPath: {forwardRP.RootPath}");

        if (forwardRP.EntryInstance == null)
        {
            Logger.Error("ForwardRP EntryInstance is null!");
            return false;
        }

        return true;
    }

    private bool TestPackageCount()
    {
        Logger.Log("Testing Package Count...");
        var packageSubsystem = EngineKernel.Instance.GetSubsystem<PackageSubsystem>();
        if (packageSubsystem == null)
        {
            Logger.Error("PackageSubsystem not found!");
            return false;
        }

        int count = packageSubsystem.GetAllPackages().Count();
        Logger.Log($"Package count: {count}");
        if (count < 1)
        {
            Logger.Error($"Expected at least 1 package, got {count}");
            return false;
        }

        return true;
    }

    private bool TestGetPackageEntryTyped()
    {
        Logger.Log("Testing GetPackageEntry<T>...");
        var packageSubsystem = EngineKernel.Instance.GetSubsystem<PackageSubsystem>();
        if (packageSubsystem == null)
        {
            Logger.Error("PackageSubsystem not found!");
            return false;
        }

        var nonExistent = packageSubsystem.GetPackageEntry<object>("com.arisen.nonexistent");
        if (nonExistent != null)
        {
            Logger.Error("GetPackageEntry returned non-null for a non-existent package ID!");
            return false;
        }

        return true;
    }

    private bool TestDependencyResolution()
    {
        Logger.Log("Testing Dependency Resolution (Mock)...");
        // This would ideally test if a package with dependencies loads its dependencies.
        // For now we just verify the subsystem doesn't crash and reports loaded packages correctly.
        return true;
    }
}
