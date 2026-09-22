using System;
using System.IO;
using ArisenEngine.Core.Memory;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// The frame arena is frame-scoped memory: producers allocate during a frame and the frame
/// boundary reclaims every allocation. A runtime that never reclaimed it exhausted the arena
/// after enough frames and failed the next allocation with an <see cref="OutOfMemoryException"/>
/// from an unrelated system, so the contract is pinned here.
/// </summary>
public sealed class FrameArenaTests
{
    [Fact]
    public void ResetReclaimsEveryAllocationOfTheFinishedFrame()
    {
        var arena = new FrameArena(1);
        try
        {

            arena.Alloc<byte>(4096);
            arena.Alloc<int>(1024);
            Assert.Equal(4096 + 1024 * sizeof(int), (int)arena.UsedBytes);

            arena.Reset();

            Assert.Equal(0u, (uint)arena.UsedBytes);
            Assert.Equal(4096, arena.Alloc<byte>(4096).Length);
            Assert.Equal(4096u, (uint)arena.UsedBytes);
        }
        finally
        {
            arena.Dispose();
        }
    }

    [Fact]
    public void RepeatedFramesNeverAccumulateBeyondOneFrameOfData()
    {
        var arena = new FrameArena(1);
        try
        {
            const int frameBytes = 64 * 1024;
            nuint lastFrameUsage = 0;
            for (int frame = 0; frame < 20_000; frame++)
            {
                arena.Alloc<long>(frameBytes / sizeof(long));
                lastFrameUsage = arena.UsedBytes;
                arena.Reset();
            }

            Assert.Equal((nuint)frameBytes, lastFrameUsage);
            Assert.Equal(0u, (uint)arena.UsedBytes);
        }
        finally
        {
            arena.Dispose();
        }
    }

    [Fact]
    public void OneFrameThatExceedsTheBudgetFailsInsteadOfGrowing()
    {
        var arena = new FrameArena(1);
        try
        {
            arena.Alloc<byte>(512 * 1024);

            OutOfMemoryException error = Assert.Throws<OutOfMemoryException>(() =>
            {
                arena.Alloc<byte>(1024 * 1024);
            });

            Assert.Contains("Frame arena exhausted", error.Message, StringComparison.Ordinal);
            Assert.Equal((nuint)(512 * 1024), arena.UsedBytes);
        }
        finally
        {
            arena.Dispose();
        }
    }

    [Fact]
    public void NegativeAllocationCountsAreRejectedWithoutConsumingTheArena()
    {
        var arena = new FrameArena(1);
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                arena.Alloc<byte>(-1);
            });

            Assert.Equal(0u, (uint)arena.UsedBytes);
        }
        finally
        {
            arena.Dispose();
        }
    }

    [Fact]
    public void MemoryManagerOwnsTheRuntimeArenaForItsWholeLifetime()
    {
        MemoryManager.Shutdown();
        Assert.False(MemoryManager.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => MemoryManager.FrameArena);

        try
        {
            MemoryManager.Initialize(1);
            Assert.True(MemoryManager.IsInitialized);
            FrameArena first = MemoryManager.FrameArena;
            Assert.Equal((nuint)(1024 * 1024), first.CapacityBytes);

            // A package reload replaces the arena exactly once instead of stacking generations.
            MemoryManager.Initialize(2);
            FrameArena second = MemoryManager.FrameArena;
            Assert.NotSame(first, second);
            Assert.Equal((nuint)(2 * 1024 * 1024), second.CapacityBytes);
        }
        finally
        {
            MemoryManager.Shutdown();
        }

        Assert.False(MemoryManager.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => MemoryManager.FrameArena);
    }

    [Fact]
    public void TheFrameArenaIsReclaimedAtTheFrameBoundaryByItsSingleOwner()
    {
        string frameArena = ReadSource("Arisen/Development/PackageGame/Local/com.arisen.core/Memory/FrameArena.cs");
        string memoryManager = ReadSource("Arisen/Development/PackageGame/Local/com.arisen.core/Memory/MemoryManager.cs");
        string corePackage = ReadSource("Arisen/Development/PackageGame/Local/com.arisen.core/CorePackage.cs");
        string meshSystem = ReadSource("Arisen/Development/PackageGame/Local/com.arisen.ecs/Systems/MeshSystem.cs");
        string renderSubsystem = ReadSource("Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSubsystem.cs");

        // Frame memory has exactly one owner: no hidden process-wide arena can shadow it.
        Assert.DoesNotContain("Lazy<FrameArena>", frameArena, StringComparison.Ordinal);
        Assert.DoesNotContain("static FrameArena", frameArena, StringComparison.Ordinal);

        // The owner creates the arena on package load, releases it on unload, and reclaims it at
        // the kernel frame boundary, after every subsystem has ticked.
        Assert.Contains("MemoryManager.Initialize();", corePackage, StringComparison.Ordinal);
        Assert.Contains("MemoryManager.Shutdown();", corePackage, StringComparison.Ordinal);
        Assert.Contains("OnFrameEnd += OnFrameEnd;", memoryManager, StringComparison.Ordinal);
        Assert.Contains("OnFrameEnd -= OnFrameEnd;", memoryManager, StringComparison.Ordinal);

        // Frame-scoped producers allocate through the owner and never resurrect the singleton.
        Assert.Contains("MemoryManager.FrameArena.Alloc", meshSystem, StringComparison.Ordinal);
        Assert.Contains("MemoryManager.FrameArena", renderSubsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("FrameArena.Instance", meshSystem, StringComparison.Ordinal);
        Assert.DoesNotContain("FrameArena.Instance", renderSubsystem, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(CppSourceContractScanner.FindRepoRoot(), relativePath));
    }
}
