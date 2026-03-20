using ArisenKernel.Packages;
using ArisenKernel.Services;
using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;

namespace ArisenEngine.Core.ECS;

public class EcsPackage : IPackageEntry
{
    private IEntityManager? m_EntityManager;

    public void OnLoad(IServiceRegistry registry)
    {
        m_EntityManager = new EntityManager();
        registry.RegisterService<IEntityManager>(m_EntityManager);
        System.Console.WriteLine("[EcsPackage] Loaded and registered IEntityManager");
    }

    public void OnUnload(IServiceRegistry registry)
    {
        if (m_EntityManager != null)
        {
            // Unregister functionality is not yet defined in the registry contract
            m_EntityManager = null;
        }
        System.Console.WriteLine("[EcsPackage] Unloaded");
    }
}

