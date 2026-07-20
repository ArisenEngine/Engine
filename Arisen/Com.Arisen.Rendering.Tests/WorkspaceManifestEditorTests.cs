using System.Text;
using System.Text.Json;
using ArisenEditor.Core.Services;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class WorkspaceManifestEditorTests
{
    private static readonly JsonDocumentOptions s_DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    [Fact]
    public void SetStartupScene_ReplacesOnlyValueAndPreservesBomCommentsAndCrLf()
    {
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "manifest.json");
        var nextGuid = Guid.Parse("22222222-3333-4444-5555-666666666666");
        const string oldValue = "{\r\n" +
            "    // This comment belongs to the replaced setting.\r\n" +
            "    \"Guid\": \"11111111-2222-3333-4444-555555555555\",\r\n" +
            "    \"PackageId\": \"com.example.game\",\r\n" +
            "  }";
        const string source = "{\r\n" +
            "  // Workspace identity must survive edits.\r\n" +
            "  \"Name\": \"Commented Workspace\",\r\n" +
            "  \"EngineVersion\": \"Current\",\r\n" +
            "  \"StartupScene\": " + oldValue + ",\r\n" +
            "  \"CustomTooling\": { \"Keep\": true },\r\n" +
            "  \"Packages\": [\r\n" +
            "    { \"Id\": \"com.example.game\", \"Version\": \"1.0.0\" },\r\n" +
            "  ],\r\n" +
            "  \"Profiles\": {\r\n" +
            "    // Profile comments are unrelated.\r\n" +
            "    \"Editor\": { \"Packages\": [], },\r\n" +
            "  },\r\n" +
            "}\r\n";

        File.WriteAllText(path, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        int valueStart = source.IndexOf(oldValue, StringComparison.Ordinal);
        string expectedPrefix = source[..valueStart];
        string expectedSuffix = source[(valueStart + oldValue.Length)..];

        var result = WorkspaceManifestEditor.SetStartupScene(path, nextGuid, "com.example.game");

        Assert.True(result.Success, result.Diagnostic);
        byte[] updatedBytes = File.ReadAllBytes(path);
        Assert.True(updatedBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        string updated = File.ReadAllText(path);
        Assert.StartsWith(expectedPrefix, updated, StringComparison.Ordinal);
        Assert.EndsWith(expectedSuffix, updated, StringComparison.Ordinal);
        Assert.Contains("// Workspace identity must survive edits.", updated, StringComparison.Ordinal);
        Assert.Contains("// Profile comments are unrelated.", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", updated.Replace("\r\n", string.Empty), StringComparison.Ordinal);
        AssertStartupScene(path, nextGuid, "com.example.game");
    }

    [Fact]
    public void SetStartupScene_InsertsMissingPropertyBeforePackages()
    {
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "manifest.json");
        var sceneGuid = Guid.Parse("33333333-4444-5555-6666-777777777777");
        const string source = """
            {
              "Name": "Insertion Workspace",
              "EngineVersion": "Current",
              // Keep this package graph comment.
              "Packages": [
                { "Id": "com.example.game", "Version": "1.0.0" },
              ],
              "Unknown": { "Preserve": [1, 2, 3] },
            }
            """;
        File.WriteAllText(path, source);

        var result = WorkspaceManifestEditor.SetStartupScene(path, sceneGuid, "com.example.game");

        Assert.True(result.Success, result.Diagnostic);
        string updated = File.ReadAllText(path);
        Assert.Contains("// Keep this package graph comment.", updated, StringComparison.Ordinal);
        Assert.Contains("\"Unknown\": { \"Preserve\": [1, 2, 3] }", updated, StringComparison.Ordinal);
        Assert.True(
            updated.IndexOf("\"StartupScene\"", StringComparison.Ordinal) <
            updated.IndexOf("\"Packages\"", StringComparison.Ordinal));
        AssertStartupScene(path, sceneGuid, "com.example.game");
    }

    [Fact]
    public void SetStartupScene_RejectsProfileOnlyPackageWithoutChangingFile()
    {
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "manifest.json");
        const string source = """
            {
              "Name": "Profile Package Workspace",
              "EngineVersion": "Current",
              "Packages": [
                { "Id": "com.example.game", "Version": "1.0.0" }
              ],
              "Profiles": {
                "Editor": {
                  "Packages": [
                    { "Id": "com.example.editor-content", "Version": "1.0.0" }
                  ]
                }
              }
            }
            """;
        File.WriteAllText(path, source);
        byte[] before = File.ReadAllBytes(path);

        var result = WorkspaceManifestEditor.SetStartupScene(
            path,
            Guid.Parse("44444444-5555-6666-7777-888888888888"),
            "com.example.editor-content");

        Assert.False(result.Success);
        Assert.Contains("base Packages", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void SetStartupScene_DoesNotRewriteMatchingReference()
    {
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "manifest.json");
        var sceneGuid = Guid.Parse("55555555-6666-7777-8888-999999999999");
        string source = $$"""
            {
              "Name": "Matching Workspace",
              "EngineVersion": "Current",
              "StartupScene": {
                "Guid": "{{sceneGuid:D}}",
                "PackageId": "com.example.game"
              },
              "Packages": [
                { "Id": "com.example.game", "Version": "1.0.0" }
              ]
            }
            """;
        File.WriteAllText(path, source);
        byte[] before = File.ReadAllBytes(path);

        var result = WorkspaceManifestEditor.SetStartupScene(path, sceneGuid, "com.example.game");

        Assert.True(result.Success, result.Diagnostic);
        Assert.Contains("already matches", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void SetStartupScene_RejectsDuplicatePropertyEvenWhenFirstReferenceMatches()
    {
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "manifest.json");
        var sceneGuid = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
        string source = $$"""
            {
              "Name": "Duplicate Startup Scene Workspace",
              "EngineVersion": "Current",
              "StartupScene": {
                "Guid": "{{sceneGuid:D}}",
                "PackageId": "com.example.game"
              },
              "StartupScene": {
                "Guid": "77777777-8888-9999-aaaa-bbbbbbbbbbbb",
                "PackageId": "com.example.game"
              },
              "Packages": [
                { "Id": "com.example.game", "Version": "1.0.0" }
              ]
            }
            """;
        File.WriteAllText(path, source);
        byte[] before = File.ReadAllBytes(path);

        var result = WorkspaceManifestEditor.SetStartupScene(path, sceneGuid, "com.example.game");

        Assert.False(result.Success);
        Assert.Contains("more than one", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void SetProjectAssets_InsertsSceneAndPipelineInOnePreservingWrite()
    {
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "manifest.json");
        var sceneGuid = Guid.Parse("88888888-9999-aaaa-bbbb-cccccccccccc");
        var pipelineGuid = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        const string source = """
            {
              "Name": "Atomic Settings Workspace",
              "EngineVersion": "Current",
              // Keep package ownership comments and unknown fields.
              "Packages": [
                { "Id": "com.example.game", "Version": "1.0.0" },
                { "Id": "com.example.pipeline", "Version": "1.0.0" },
              ],
              "Unknown": { "Preserve": true },
            }
            """;
        File.WriteAllText(path, source);

        var result = WorkspaceManifestEditor.SetProjectAssets(
            path,
            new WorkspaceProjectAssetSelection(sceneGuid, "com.example.game"),
            new WorkspaceProjectAssetSelection(pipelineGuid, "com.example.pipeline"));

        Assert.True(result.Success, result.Diagnostic);
        string updated = File.ReadAllText(path);
        Assert.Contains("// Keep package ownership comments", updated, StringComparison.Ordinal);
        Assert.Contains("\"Unknown\": { \"Preserve\": true }", updated, StringComparison.Ordinal);
        Assert.True(
            updated.IndexOf("\"RenderPipeline\"", StringComparison.Ordinal) <
            updated.IndexOf("\"Packages\"", StringComparison.Ordinal));
        AssertAssetReference(path, "StartupScene", sceneGuid, "com.example.game");
        AssertAssetReference(path, "RenderPipeline", pipelineGuid, "com.example.pipeline");
    }

    [Fact]
    public void SetProjectAssets_InvalidPipelinePackageLeavesSceneAndManifestUnchanged()
    {
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "manifest.json");
        const string source = """
            {
              "Name": "Atomic Failure Workspace",
              "EngineVersion": "Current",
              "Packages": [
                { "Id": "com.example.game", "Version": "1.0.0" }
              ],
              "Profiles": {
                "Editor": {
                  "Packages": [
                    { "Id": "com.example.pipeline", "Version": "1.0.0" }
                  ]
                }
              }
            }
            """;
        File.WriteAllText(path, source);
        byte[] before = File.ReadAllBytes(path);

        var result = WorkspaceManifestEditor.SetProjectAssets(
            path,
            new WorkspaceProjectAssetSelection(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                "com.example.game"),
            new WorkspaceProjectAssetSelection(
                Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
                "com.example.pipeline"));

        Assert.False(result.Success);
        Assert.Contains("base Packages", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    private static void AssertStartupScene(string path, Guid expectedGuid, string expectedPackageId)
    {
        AssertAssetReference(path, "StartupScene", expectedGuid, expectedPackageId);
    }

    private static void AssertAssetReference(
        string path,
        string propertyName,
        Guid expectedGuid,
        string expectedPackageId)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), s_DocumentOptions);
        var assetReference = document.RootElement.GetProperty(propertyName);
        Assert.Equal(expectedGuid, assetReference.GetProperty("Guid").GetGuid());
        Assert.Equal(expectedPackageId, assetReference.GetProperty("PackageId").GetString());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ArisenWorkspaceManifestEditorTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
