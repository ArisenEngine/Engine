using Xunit;

namespace Com.Arisen.Rendering.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SceneComponentExtensionRegistryCollection
{
    public const string Name = "Scene component extension registry";
}
