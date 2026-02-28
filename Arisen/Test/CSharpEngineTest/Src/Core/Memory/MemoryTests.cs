using ArisenEngine.Core.Memory;
using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.Core.Memory;

public class MemoryTests : ITest
{
    public string GetName() => "Memory Infrastructure Tests";
    public TestCategory GetCategory() => TestCategory.Framework;

    public bool Setup()
    {
        MemoryManager.Initialize(16); // 16MB for test
        return true;
    }

    public void Teardown()
    {
        MemoryManager.Shutdown();
    }

    public bool Run()
    {
        return TestNativeArray() && TestFrameArena();
    }

    private bool TestNativeArray()
    {
        Logger.Log("Testing NativeArray...");
        using var array = new NativeArray<int>(100);
        
        if (array.Length != 100) return false;

        for (int i = 0; i < 100; i++) array[i] = i * 2;
        for (int i = 0; i < 100; i++)
        {
            if (array[i] != i * 2) return false;
        }

        var span = array.AsSpan();
        if (span.Length != 100) return false;
        if (span[50] != 100) return false;

        return true;
    }

    private bool TestFrameArena()
    {
        Logger.Log("Testing FrameArena...");
        var arena = MemoryManager.FrameArena;
        
        // 1. Basic allocation
        var span1 = arena.Alloc<int>(10);
        if (span1.Length != 10) return false;
        for (int i = 0; i < 10; i++) span1[i] = i;

        // 2. Sequential allocation
        var span2 = arena.Alloc<float>(5);
        if (span2.Length != 5) return false;
        for (int i = 0; i < 5; i++) span2[i] = i * 1.5f;

        // 3. Reset and reuse
        arena.Reset();
        var span3 = arena.Alloc<int>(10);
        // span3 should overlap with span1's memory address
        unsafe
        {
            fixed (int* p1 = span1, p3 = span3)
            {
                if (p1 != p3)
                {
                    Logger.Error("Arena reset did not return to start of buffer");
                    return false;
                }
            }
        }

        return true;
    }
}
