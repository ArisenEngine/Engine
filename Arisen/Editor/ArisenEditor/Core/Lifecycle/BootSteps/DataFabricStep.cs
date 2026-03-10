using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class DataFabricStep : IBootStep
{
    public string Name => "Data Fabric Initialization";
    public string Description => "Mounting virtual file system and asset databases...";

    public async Task ExecuteAsync(BootContext context)
    {
        await Task.Delay(1000);
    }
}
