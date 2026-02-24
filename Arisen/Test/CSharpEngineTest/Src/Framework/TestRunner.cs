using System;
using System.Collections.Generic;
using ArisenEngine.Core.Diagnostics;

namespace CSharpEngineTest.Framework
{
    public static class TestRunner
    {
        public struct TestResult
        {
            public string TestName;
            public bool Passed;
            public string ErrorMessage;
        }

        private static readonly List<Func<ITest>> _registry = new List<Func<ITest>>();

        public static void RegisterTest<T>() where T : ITest, new()
        {
            _registry.Add(() => new T());
        }

        public static List<TestResult> RunAllTests()
        {
            return RunWithFilter(t => true);
        }

        public static List<TestResult> RunByCategory(TestCategory category)
        {
            return RunWithFilter(t => t.GetCategory() == category);
        }

        public static List<TestResult> RunWithFilter(Func<ITest, bool> filter)
        {
            var results = new List<TestResult>();

            Logger.Log($"=== Starting C# Test Batch (Total registered: {_registry.Count}) ===");

            foreach (var factory in _registry)
            {
                var test = factory();
                if (!filter(test)) continue;

                var result = new TestResult { TestName = test.GetName(), Passed = false, ErrorMessage = "" };
                try
                {
                    Logger.Log($"[TEST] Starting: {test.GetName()}");

                    if (!test.Setup())
                    {
                        result.ErrorMessage = "Setup failed";
                        Logger.Error($"[FAILED] {test.GetName()} - Setup failed");
                    }
                    else
                    {
                        result.Passed = test.Run();
                        test.Teardown();

                        if (result.Passed)
                        {
                            Logger.Log($"[PASSED] {test.GetName()}");
                        }
                        else
                        {
                            Logger.Error($"[FAILED] {test.GetName()} - Test logic failed");
                            result.ErrorMessage = "Test logic failed";
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Passed = false;
                    result.ErrorMessage = ex.Message;
                    Logger.Error($"[FAILED] {test.GetName()} - Exception: {ex.Message}");
                    Logger.Error(ex.StackTrace ?? "");
                }

                results.push_back(result);
            }

            if (results.Count == 0)
            {
                Logger.Log("No tests matched the filter.");
            }
            else
            {
                int passed = 0;
                foreach (var r in results) if (r.Passed) passed++;
                Logger.Log("=== Test Summary ===");
                Logger.Log($"Total: {results.Count} | Passed: {passed} | Failed: {results.Count - passed}");
            }

            return results;
        }
    }

    // Helper extension until we have List.Add that looks like C++ push_back (mental context)
    internal static class ListExtensions
    {
        public static void push_back<T>(this List<T> list, T item) => list.Add(item);
    }
}
