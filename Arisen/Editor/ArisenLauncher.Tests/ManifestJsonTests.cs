using System.Text.Json;
using ArisenLauncher.Models;
using ArisenLauncher.Services;
using Xunit;

namespace ArisenLauncher.Tests;

public sealed class ManifestJsonTests
{
    [Fact]
    public void LauncherManifestParserAllowsCommentsAndTrailingCommas()
    {
        var manifest = ManifestJson.Deserialize<ProjectManifest>(
            """
            {
              // Launcher-authored workspace manifests can contain comments.
              "Name": "JsoncLauncherFixture",
              "EngineVersion": "Current",
              "Packages": [
                {
                  "Id": "com.test.app",
                  "Url": "file://Local/com.test.app",
                  "Version": "1.0.0",
                },
              ],
            }
            """);

        Assert.NotNull(manifest);
        Assert.Equal("JsoncLauncherFixture", manifest!.Name);
        Assert.Equal("com.test.app", manifest.Packages!.Single().Id);
    }

    [Fact]
    public void LauncherManifestParserRejectsFullJson5UnquotedKeys()
    {
        Assert.Throws<JsonException>(() => ManifestJson.Deserialize<ProjectManifest>(
            """
            {
              Name: "Json5LauncherFixture",
              Packages: [],
            }
            """));
    }
}
