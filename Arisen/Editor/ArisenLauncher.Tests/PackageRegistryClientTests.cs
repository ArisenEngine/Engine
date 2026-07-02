using ArisenLauncher.Services;
using Xunit;

namespace ArisenLauncher.Tests;

public sealed class PackageRegistryClientTests
{
    [Fact]
    public void ParseIndexReturnsSortedPackageVersionsAndResolvesRelativeArchiveUrls()
    {
        string json = """
{
  "schemaVersion": 1,
  "packages": [
    {
      "id": "com.test.zeta",
      "version": "2.0.0",
      "archive": {
        "url": "packages/com.test.zeta-2.0.0.zip",
        "sha256": "abc",
        "sizeBytes": 123
      }
    },
    {
      "id": "com.test.alpha",
      "version": "1.0.0",
      "name": "Alpha",
      "description": "Alpha package",
      "type": "managed",
      "archive": {
        "url": "https://cdn.example.test/com.test.alpha-1.0.0.zip",
        "sha256": "def",
        "sizeBytes": 456
      }
    }
  ]
}
""";

        var packages = PackageRegistryClient.ParseIndex(json, "https://registry.example.test/arisen/registry.json");

        Assert.Equal(2, packages.Count);
        Assert.Equal("com.test.alpha", packages[0].Id);
        Assert.Equal("Alpha", packages[0].Name);
        Assert.Equal("https://cdn.example.test/com.test.alpha-1.0.0.zip", packages[0].ArchiveUrl);
        Assert.Equal("com.test.zeta", packages[1].Id);
        Assert.Equal("https://registry.example.test/arisen/packages/com.test.zeta-2.0.0.zip", packages[1].ArchiveUrl);
    }

    [Fact]
    public void ParseIndexRejectsUnsupportedSchemaVersion()
    {
        string json = """
{
  "schemaVersion": 2,
  "packages": []
}
""";

        var ex = Assert.Throws<InvalidOperationException>(() => PackageRegistryClient.ParseIndex(json, "https://registry.example.test/registry.json"));
        Assert.Contains("unsupported schemaVersion", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("^1.2.0", "1.4.0")]
    [InlineData("~1.2.0", "1.2.5")]
    [InlineData(">=1.2.0 <2.0.0", "1.4.0")]
    [InlineData("*", "2.0.0")]
    public void SelectPackageVersionChoosesHighestMatchingSemanticVersion(string range, string expectedVersion)
    {
        var packages = new[]
        {
            new PackageRegistryPackageVersion { Id = "com.test.range", Version = "1.2.0" },
            new PackageRegistryPackageVersion { Id = "com.test.range", Version = "1.2.5" },
            new PackageRegistryPackageVersion { Id = "com.test.range", Version = "1.4.0" },
            new PackageRegistryPackageVersion { Id = "com.test.range", Version = "2.0.0" }
        };

        var selected = PackageRegistryClient.SelectPackageVersion(packages, "com.test.range", range, out string error);

        Assert.Equal(string.Empty, error);
        Assert.NotNull(selected);
        Assert.Equal(expectedVersion, selected.Version);
    }

    [Fact]
    public void SelectPackageVersionReportsInvalidRange()
    {
        var packages = new[]
        {
            new PackageRegistryPackageVersion { Id = "com.test.range", Version = "1.0.0" }
        };

        var selected = PackageRegistryClient.SelectPackageVersion(packages, "com.test.range", ">=bad", out string error);

        Assert.Null(selected);
        Assert.Contains("Invalid", error, StringComparison.OrdinalIgnoreCase);
    }
}
