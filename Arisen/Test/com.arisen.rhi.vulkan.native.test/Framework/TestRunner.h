#pragma once

#include <vector>
#include <functional>
#include <memory>
#include "Base/FoundationMinimal.h"
#include "Logger/Logger.h"

namespace ArisenEngine::Testing
{
    /**
     * @brief Test categories for organization and filtering.
     */
    enum class TestCategory
    {
        Unit, // Logic and resource creation (no rendering/swapchain)
        Rendering, // Full rendering flow including window and swapchain
        Performance, // Benchmarking specific operations
        Misc // Other tests
    };

    /**
     * @brief Base interface for all test cases.
     */
    class ITest
    {
    public:
        virtual ~ITest() = default;

        /**
         * @brief Get the name of this test.
         */
        virtual const char* GetName() const = 0;

        /**
         * @brief Get the category of this test.
         */
        virtual TestCategory GetCategory() const { return TestCategory::Misc; }

        /**
         * @brief Setup test resources before running.
         * @return true if setup succeeded, false otherwise.
         */
        virtual bool Setup() = 0;

        /**
         * @brief Run the actual test logic.
         * @return true if test passed, false if test failed.
         */
        virtual bool Run() = 0;

        /**
         * @brief Cleanup test resources after running.
         */
        virtual void Teardown() = 0;
    };

    /**
     * @brief Test registration and execution system.
     * 
     * Usage:
     *   TestRunner::RegisterTest<MyTest>();
     *   TestRunner::RunAllTests();
     *   TestRunner::RunByCategory(TestCategory::Unit);
     */
    class TestRunner
    {
    public:
        struct TestResult
        {
            String testName;
            bool passed;
            String errorMessage;
        };

        /**
         * @brief Register a test for execution.
         */
        template <typename T>
        static void RegisterTest()
        {
            static_assert(std::is_base_of<ITest, T>::value, "T must inherit from ITest");

            GetRegistry().push_back([]() -> std::unique_ptr<ITest>
            {
                return std::make_unique<T>();
            });
        }

        /**
         * @brief Run all registered tests.
         * @return Vector of test results.
         */
        static std::vector<TestResult> RunAllTests()
        {
            return RunWithFilter([](const ITest&) { return true; });
        }

        /**
         * @brief Run tests in a specific category.
         */
        static std::vector<TestResult> RunByCategory(TestCategory category)
        {
            return RunWithFilter([category](const ITest& test)
            {
                return test.GetCategory() == category;
            });
        }

        /**
         * @brief Run a specific test by name.
         */
        static std::vector<TestResult> RunTestByName(const String& name)
        {
            return RunWithFilter([&name](const ITest& test)
            {
                return name == test.GetName();
            });
        }

        /**
         * @brief Internal implementation of filtered test execution.
         */
        static std::vector<TestResult> RunWithFilter(std::function<bool(const ITest&)> filter)
        {
            std::vector<TestResult> results;
            auto& registry = GetRegistry();

            LOG_INFO(String::Format("=== Starting Test Batch (Total registered: %zu) ===", registry.size()).c_str());

            for (auto& factory : registry)
            {
                auto test = factory();
                if (!filter(*test)) continue;

                TestResult result{test->GetName(), false, ""};
                try
                {
                    LOG_INFO((String("[TEST] Starting: ") + test->GetName()).c_str());

                    if (!test->Setup())
                    {
                        result.errorMessage = "Setup failed";
                        LOG_ERROR((String("[FAILED] ") + test->GetName() + " - Setup failed").c_str());
                    }
                    else
                    {
                        result.passed = test->Run();
                        test->Teardown();

                        if (result.passed)
                        {
                            LOG_INFO((String("[PASSED] ") + test->GetName()).c_str());
                        }
                        else
                        {
                            LOG_ERROR((String("[FAILED] ") + test->GetName() + " - Test logic failed").c_str());
                            result.errorMessage = "Test logic failed";
                        }
                    }
                }
                catch (const std::exception& ex)
                {
                    result.passed = false;
                    result.errorMessage = ex.what();
                    LOG_ERROR((String("[FAILED] ") + test->GetName() + " - Exception: " + ex.what()).c_str());
                }
                catch (...)
                {
                    result.passed = false;
                    result.errorMessage = "Unknown exception";
                    LOG_ERROR((String("[FAILED] ") + test->GetName() + " - Unknown exception").c_str());
                }

                results.push_back(result);
            }

            // Print summary
            if (results.empty())
            {
                LOG_INFO("No tests matched the filter.");
            }
            else
            {
                size_t passed = 0;
                for (const auto& r : results) { if (r.passed) ++passed; }
                LOG_INFO("=== Test Summary ===");
                LOG_INFO(
                    String::Format("Total: %zu | Passed: %zu | Failed: %zu", results.size(), passed, results.size() -
                        passed).c_str());
            }

            return results;
        }

    private:
        using TestFactory = std::function<std::unique_ptr<ITest>()>;

        static std::vector<TestFactory>& GetRegistry()
        {
            static std::vector<TestFactory> registry;
            return registry;
        }
    };
}
