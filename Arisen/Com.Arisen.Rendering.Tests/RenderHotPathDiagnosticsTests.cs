using System.Text.RegularExpressions;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderHotPathDiagnosticsTests
{
    [Theory]
    [InlineData(null, RenderDiagnosticCategory.None)]
    [InlineData("", RenderDiagnosticCategory.None)]
    [InlineData("frame", RenderDiagnosticCategory.Frame)]
    [InlineData("submission, graph | passes", RenderDiagnosticCategory.Submission | RenderDiagnosticCategory.Graph | RenderDiagnosticCategory.Passes)]
    [InlineData("all", RenderDiagnosticCategory.All)]
    public void CategoryPolicyParsesExplicitProcessStartSelection(
        string? value,
        RenderDiagnosticCategory expected)
    {
        Assert.Equal(expected, RenderDiagnostics.ParseCategories(value));
    }

    [Fact]
    public void CategoryPolicyRejectsUnknownSelection()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => RenderDiagnostics.ParseCategories("frame,unknown"));

        Assert.Contains(RenderDiagnostics.EnvironmentVariableName, error.Message, StringComparison.Ordinal);
        Assert.Contains("unknown", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledWarmedCategoryCheckAllocatesNoManagedMemory()
    {
        _ = RenderDiagnostics.IsEnabled(
            RenderDiagnosticCategory.None,
            RenderDiagnosticCategory.All);

        long before = GC.GetAllocatedBytesForCurrentThread();
        int enabledCount = 0;
        for (int iteration = 0; iteration < 100_000; iteration++)
        {
            if (RenderDiagnostics.IsEnabled(
                    RenderDiagnosticCategory.None,
                    RenderDiagnosticCategory.All))
            {
                enabledCount++;
            }
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, enabledCount);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ManagedWarmedRenderTextIsExplicitlyCategoryOwned()
    {
        string subsystem = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSubsystem.cs");
        string submission = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderFrameSubmission.cs");
        string graph = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");
        string pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        string clearPass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/ClearPass.cs");
        string staticMeshPass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs");

        Assert.DoesNotContain("DiagnosticsFrameInterval", submission, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticsFrameInterval", graph, StringComparison.Ordinal);
        Assert.DoesNotContain("% 60", subsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("% 60", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("% 60", clearPass, StringComparison.Ordinal);
        Assert.DoesNotContain("% 60", staticMeshPass, StringComparison.Ordinal);
        Assert.DoesNotContain("|| ticket > 0", subsystem, StringComparison.Ordinal);

        AssertMarkerIsCategoryGuarded(
            subsystem,
            "[RenderSubsystem] Tick |",
            "RenderDiagnosticCategory.Frame");
        AssertMarkerIsCategoryGuarded(
            subsystem,
            "[RenderSubsystem] FrameSnapshot |",
            "RenderDiagnosticCategory.Frame");
        AssertMarkerIsCategoryGuarded(
            subsystem,
            "[RenderSubsystem] Shared output |",
            "RenderDiagnosticCategory.Frame");
        AssertMarkerIsCategoryGuarded(
            submission,
            "[RenderSubmission] BeginFrame |",
            "RenderDiagnosticCategory.Submission");
        AssertMarkerIsCategoryGuarded(
            submission,
            "[RenderSubmission] EndFrame |",
            "RenderDiagnosticCategory.Submission");
        AssertMarkerIsCategoryGuarded(
            pipeline,
            "[GenericRenderPipeline] SetupGraph |",
            "RenderDiagnosticCategory.Frame");
        AssertMarkerIsCategoryGuarded(
            clearPass,
            "[ClearPass] Record |",
            "RenderDiagnosticCategory.Passes");
        AssertMarkerIsCategoryGuarded(
            staticMeshPass,
            "[StaticMeshPass] RecordFallback |",
            "RenderDiagnosticCategory.Passes");

        Assert.Contains("RenderDiagnosticCategory.Graph", graph, StringComparison.Ordinal);
        Assert.Contains("(!m_DiagnosticsLoggedOnce || !compileCacheHit)", graph, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstPartyVulkanRenderPathHasNoDirectConsoleOutput()
    {
        string repoRoot = CppSourceContractScanner.FindRepoRoot();
        string vulkanRoot = Path.Combine(
            repoRoot,
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan");
        var violations = new List<string>();
        var directOutput = new Regex(
            @"(?:std::(?:cout|cerr)|\b(?:printf|fprintf|puts)\s*\()",
            RegexOptions.CultureInvariant);

        foreach (string sourcePath in Directory.EnumerateFiles(vulkanRoot, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(sourcePath);
            if (extension is not (".cpp" or ".c" or ".cc" or ".cxx" or ".h" or ".hpp"))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(vulkanRoot, sourcePath);
            if (relativePath.StartsWith($"3rdparty{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(sourcePath);
            string masked = CppSourceContractScanner.MaskCommentsAndLiterals(source);
            Match match = directOutput.Match(masked);
            if (match.Success)
            {
                violations.Add(
                    $"{relativePath}:{CppSourceContractScanner.GetLineNumber(source, match.Index)}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "First-party Vulkan source bypasses owned diagnostics: " + string.Join(", ", violations));

        string instance = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Core/RHIVkInstance.cpp");
        string swapchain = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");
        Assert.Contains("LOG_ERROR", instance, StringComparison.Ordinal);
        Assert.Contains("LOG_WARN", instance, StringComparison.Ordinal);
        Assert.DoesNotContain("Virtual Acquire - Frame", swapchain, StringComparison.Ordinal);
    }

    private static void AssertMarkerIsCategoryGuarded(
        string source,
        string marker,
        string category)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Expected diagnostic marker '{marker}'.");
        int guardSearchStart = Math.Max(0, markerIndex - 800);
        string prefix = source[guardSearchStart..markerIndex];
        Assert.Contains(
            $"RenderDiagnostics.IsEnabled({category})",
            prefix,
            StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(CppSourceContractScanner.FindRepoRoot(), relativePath));
    }
}
