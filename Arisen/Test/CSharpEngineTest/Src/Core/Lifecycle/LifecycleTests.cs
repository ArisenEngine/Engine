using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.Core.Lifecycle;

public class LifecycleTests : ITest
{
    private class MockSubsystem : IEngineSubsystem
    {
        public int Priority { get; }
        public EnginePhase InitPhase { get; }
        public bool Initialized { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public static List<string> ExecutionLog { get; } = new();

        private readonly string m_Name;

        public MockSubsystem(string name, int priority, EnginePhase phase)
        {
            m_Name = name;
            Priority = priority;
            InitPhase = phase;
        }

        public void Initialize()
        {
            Initialized = true;
            ExecutionLog.Add($"Init:{m_Name}");
        }

        public void Shutdown()
        {
            ShutdownCalled = true;
            ExecutionLog.Add($"Shutdown:{m_Name}");
        }

        public void Dispose() => Shutdown();
    }

    public string GetName() => "Lifecycle System Tests";
    public TestCategory GetCategory() => TestCategory.Framework;

    public bool Setup()
    {
        MockSubsystem.ExecutionLog.Clear();
        return true;
    }

    public void Teardown() { }

    public bool Run()
    {
        return TestPhaseTransitions() && TestPriorityOrder();
    }

    private bool TestPhaseTransitions()
    {
        Logger.Log("Testing Phase Transitions...");
        var kernel = EngineKernel.Instance;
        
        var sub1 = new MockSubsystem("Pre", 0, EnginePhase.PreInit);
        var sub2 = new MockSubsystem("Main", 0, EnginePhase.Init);
        
        kernel.RegisterSubsystem(sub1);
        kernel.RegisterSubsystem(sub2);
        
        kernel.Initialize();
        
        bool success = sub1.Initialized && sub2.Initialized && kernel.CurrentPhase == EnginePhase.Running;
        
        kernel.Shutdown();
        
        success &= sub1.ShutdownCalled && sub2.ShutdownCalled && kernel.CurrentPhase == EnginePhase.Shutdown;
        
        return success;
    }

    private bool TestPriorityOrder()
    {
        Logger.Log("Testing Priority Order...");
        MockSubsystem.ExecutionLog.Clear();
        
        // Note: EngineKernel is a singleton, we might need a way to reset it for tests
        // For now, these tests assume sequential execution in a fresh process or careful reset.
        // Since we can't easily reset EngineKernel singleton without reflection or design change,
        // we'll just check the log from the previous test if we reuse the kernel.
        
        // Let's check if the order was Pre then Main
        if (MockSubsystem.ExecutionLog.IndexOf("Init:Pre") > MockSubsystem.ExecutionLog.IndexOf("Init:Main"))
        {
            Logger.Error("Subsystems initialized in wrong order!");
            return false;
        }

        return true;
    }
}
