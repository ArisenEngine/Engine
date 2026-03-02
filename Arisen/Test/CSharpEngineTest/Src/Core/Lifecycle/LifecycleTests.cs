using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.Core.Lifecycle;

public class LifecycleTests : ITest
{
    private class MockSubsystem1 : IEngineSubsystem
    {
        public int Priority { get; }
        public EnginePhase InitPhase { get; }
        public bool Initialized { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public static List<string> ExecutionLog { get; } = new();

        private readonly string m_Name;

        public MockSubsystem1(string name, int priority, EnginePhase phase)
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

    private class MockSubsystem2 : IEngineSubsystem
    {
        public int Priority { get; }
        public EnginePhase InitPhase { get; }
        public bool Initialized { get; private set; }
        public bool ShutdownCalled { get; private set; }

        private readonly string m_Name;

        public MockSubsystem2(string name, int priority, EnginePhase phase)
        {
            m_Name = name;
            Priority = priority;
            InitPhase = phase;
        }

        public void Initialize()
        {
            Initialized = true;
            MockSubsystem1.ExecutionLog.Add($"Init:{m_Name}");
        }

        public void Shutdown()
        {
            ShutdownCalled = true;
            MockSubsystem1.ExecutionLog.Add($"Shutdown:{m_Name}");
        }

        public void Dispose() => Shutdown();
    }

    public string GetName() => "Lifecycle System Tests";
    public TestCategory GetCategory() => TestCategory.Framework;

    public bool Setup()
    {
        EngineKernel.Instance.Reset();
        MockSubsystem1.ExecutionLog.Clear();
        return true;
    }

    public void Teardown()
    {
    }

    public bool Run()
    {
        return TestPhaseTransitions()
            && TestPriorityOrder()
            && TestRegisterAfterInit()
            && TestShutdownReverseOrder();
    }

    private bool TestPhaseTransitions()
    {
        Logger.Log("Testing Phase Transitions...");
        var kernel = EngineKernel.Instance;

        var sub1 = new MockSubsystem1("Pre", 0, EnginePhase.PreInit);
        var sub2 = new MockSubsystem2("Main", 0, EnginePhase.Init);

        kernel.RegisterSubsystem(sub1);
        kernel.RegisterSubsystem(sub2);

        kernel.Initialize(new EngineConfig { AppName = "Test" });

        bool success = sub1.Initialized && sub2.Initialized && kernel.CurrentPhase == EnginePhase.Running;

        kernel.Shutdown();

        success &= sub1.ShutdownCalled && sub2.ShutdownCalled && kernel.CurrentPhase == EnginePhase.Shutdown;

        return success;
    }

    private bool TestPriorityOrder()
    {
        Logger.Log("Testing Priority Order...");
        EngineKernel.Instance.Reset();
        MockSubsystem1.ExecutionLog.Clear();

        var kernel = EngineKernel.Instance;

        // Register with intentionally out-of-order priorities
        var high = new MockSubsystem1("HighPri", 100, EnginePhase.Init);
        var low = new MockSubsystem1("LowPri", 0, EnginePhase.Init);
        var mid = new MockSubsystem1("MidPri", 50, EnginePhase.Init);

        kernel.RegisterSubsystem(high);
        kernel.RegisterSubsystem(low);
        kernel.RegisterSubsystem(mid);

        kernel.Initialize(new EngineConfig { AppName = "PriorityTest" });

        // Priority sort: 0 (LowPri) < 50 (MidPri) < 100 (HighPri)
        int idxLow = MockSubsystem1.ExecutionLog.IndexOf("Init:LowPri");
        int idxMid = MockSubsystem1.ExecutionLog.IndexOf("Init:MidPri");
        int idxHigh = MockSubsystem1.ExecutionLog.IndexOf("Init:HighPri");

        if (idxLow < 0 || idxMid < 0 || idxHigh < 0)
        {
            Logger.Error("Not all subsystems were initialized!");
            return false;
        }

        if (!(idxLow < idxMid && idxMid < idxHigh))
        {
            Logger.Error($"Priority order wrong! LowPri={idxLow}, MidPri={idxMid}, HighPri={idxHigh}");
            return false;
        }

        Logger.Log("Priority order verified: LowPri → MidPri → HighPri");
        return true;
    }

    private bool TestRegisterAfterInit()
    {
        Logger.Log("Testing Register After Init Throws...");
        EngineKernel.Instance.Reset();

        var kernel = EngineKernel.Instance;
        kernel.RegisterSubsystem(new MockSubsystem1("Dummy", 0, EnginePhase.Init));
        kernel.Initialize(new EngineConfig { AppName = "NegativeTest" });

        try
        {
            kernel.RegisterSubsystem(new MockSubsystem2("Late", 0, EnginePhase.Init));
            Logger.Error("Expected InvalidOperationException was not thrown!");
            return false;
        }
        catch (InvalidOperationException)
        {
            Logger.Log("Correctly threw InvalidOperationException for late registration.");
            return true;
        }
    }

    private bool TestShutdownReverseOrder()
    {
        Logger.Log("Testing Shutdown Reverse Order...");
        EngineKernel.Instance.Reset();
        MockSubsystem1.ExecutionLog.Clear();

        var kernel = EngineKernel.Instance;

        var low = new MockSubsystem1("Low", 0, EnginePhase.Init);
        var mid = new MockSubsystem1("Mid", 50, EnginePhase.Init);
        var high = new MockSubsystem1("High", 100, EnginePhase.Init);

        kernel.RegisterSubsystem(low);
        kernel.RegisterSubsystem(mid);
        kernel.RegisterSubsystem(high);

        kernel.Initialize(new EngineConfig { AppName = "ShutdownTest" });
        MockSubsystem1.ExecutionLog.Clear(); // Clear init logs, only track shutdown

        kernel.Shutdown();

        // Shutdown should be in reverse priority order: High(100) → Mid(50) → Low(0)
        int idxHigh = MockSubsystem1.ExecutionLog.IndexOf("Shutdown:High");
        int idxMid = MockSubsystem1.ExecutionLog.IndexOf("Shutdown:Mid");
        int idxLow = MockSubsystem1.ExecutionLog.IndexOf("Shutdown:Low");

        if (idxHigh < 0 || idxMid < 0 || idxLow < 0)
        {
            Logger.Error("Not all subsystems were shut down!");
            return false;
        }

        if (!(idxHigh < idxMid && idxMid < idxLow))
        {
            Logger.Error($"Shutdown order wrong! High={idxHigh}, Mid={idxMid}, Low={idxLow}");
            return false;
        }

        Logger.Log("Shutdown order verified: High → Mid → Low (reverse priority)");
        return true;
    }
}