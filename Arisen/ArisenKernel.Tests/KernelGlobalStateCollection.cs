using Xunit;

namespace ArisenKernel.Tests;

/// <summary>
/// Tests that drive the process-wide kernel singleton and the static animation clock. Both are
/// process-global, so a concurrent kernel reset would release a clock pin another test is holding.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class KernelGlobalStateCollection
{
    public const string Name = "Kernel global state";
}
