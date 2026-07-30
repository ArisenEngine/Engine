using ArisenEngine.Resources.Serialization;
using ArisenKernel.Lifecycle;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeSmokeScenarioRegistryTests
{
    [Fact]
    public void RegistryRoutesNamedPackageScenariosAndOrdersDiagnostics()
    {
        var registry = new RuntimeSmokeScenarioRegistry();
        var terrain = new StubProvider("terrain-streaming");
        var world = new StubProvider("world-streaming");
        registry.Register("world-streaming", world);
        registry.Register("terrain-streaming", terrain);

        bool created = registry.TryCreateScenario(
            new RuntimeSmokeScenarioContext(
                "terrain-streaming",
                "workspace",
                "Development",
                null,
                null),
            out IRuntimeSmokeScenario scenario,
            out string diagnostic);

        Assert.True(created, diagnostic);
        Assert.Equal("terrain-streaming", scenario.Name);
        Assert.Equal(["terrain-streaming", "world-streaming"], registry.GetRegisteredModes());
    }

    [Fact]
    public void RegistryRejectsDuplicateModesAndRequiresMatchingOwnerToUnregister()
    {
        var registry = new RuntimeSmokeScenarioRegistry();
        var owner = new StubProvider("terrain-streaming");
        var other = new StubProvider("other");
        registry.Register("terrain-streaming", owner);

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("terrain-streaming", other));
        Assert.False(registry.Unregister("terrain-streaming", other));
        Assert.True(registry.Unregister("terrain-streaming", owner));
        Assert.Empty(registry.GetRegisteredModes());
    }

    private sealed class StubProvider : IRuntimeSmokeScenarioProvider
    {
        private readonly string m_Name;

        public StubProvider(string name)
        {
            m_Name = name;
        }

        public bool TryCreateScenario(
            RuntimeSmokeScenarioContext context,
            out IRuntimeSmokeScenario scenario,
            out string diagnostic)
        {
            scenario = new StubScenario(m_Name);
            diagnostic = string.Empty;
            return true;
        }
    }

    private sealed class StubScenario : IRuntimeSmokeScenario
    {
        public StubScenario(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string OutputPath => string.Empty;
        public bool IsReadyForShutdown => false;
        public bool IsComplete => false;
        public bool Succeeded => false;
        public string? FailureMessage => null;
        public void Start(uint initialFrameIndex) { }
        public void BeforeFrame(uint frameIndex) { }
        public void AfterFrame(uint frameIndex) { }
        public void ReportFailure(string message) { }
        public void AfterShutdown() { }
    }
}
