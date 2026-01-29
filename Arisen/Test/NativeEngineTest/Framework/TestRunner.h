#pragma once

#include <string>
#include <vector>
#include <functional>
#include <memory>
#include "Logger/Logger.h"

namespace ArisenEngine::Testing
{
    /**
     * @brief Test categories for organization and filtering.
     */
    enum class TestCategory
    {
        Unit,        // Logic and resource creation (no rendering/swapchain)
        Rendering,   // Full rendering flow including window and swapchain
        Performance, // Benchmarking specific operations
        Misc         // Other tests
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
            std::string testName;
            bool passed;
            std::string errorMessage;
        };

        /**
         * @brief Register a test for execution.
         */
        template<typename T>
        static void RegisterTest()
        {
            static_assert(std::is_base_of<ITest, T>::value, "T must inherit from ITest");
            
            GetRegistry().push_back([]() -> std::unique_ptr<ITest> {
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
            return RunWithFilter([category](const ITest& test) { 
                return test.GetCategory() == category; 
            });
        }

        /**
         * @brief Run a specific test by name.
         */
        static std::vector<TestResult> RunTestByName(const std::string& name)
        {
            return RunWithFilter([&name](const ITest& test) {
                return std::string(test.GetName()) == name;
            });
        }

        /**
         * @brief Internal implementation of filtered test execution.
         */
        static std::vector<TestResult> RunWithFilter(std::function<bool(const ITest&)> filter)
        {
            std::vector<TestResult> results;
            auto& registry = GetRegistry();

            LOG_INFO("=== Running RHI Unit Tests ===");
            
            for (auto& factory : registry)
            {
                auto test = factory();
                if (!filter(*test)) continue;

                TestResult result{ test->GetName(), false, "" };
                try
                {
                    LOG_INFO((std::string("[TEST] Starting: ") + test->GetName()).c_str());

                    if (!test->Setup())
                    {
                        result.errorMessage = "Setup failed";
                        LOG_ERROR((std::string("[FAILED] ") + test->GetName() + " - Setup failed").c_str());
                    }
                    else
                    {
                        result.passed = test->Run();
                        test->Teardown();

                        if (result.passed)
                        {
                            LOG_INFO((std::string("[PASSED] ") + test->GetName()).c_str());
                        }
                        else
                        {
                            LOG_ERROR((std::string("[FAILED] ") + test->GetName() + " - Test logic failed").c_str());
                            result.errorMessage = "Test logic failed";
                        }
                    }
                }
                catch (const std::exception& ex)
                {
                    result.passed = false;
                    result.errorMessage = ex.what();
                    LOG_ERROR((std::string("[FAILED] ") + test->GetName() + " - Exception: " + ex.what()).c_str());
                }
                catch (...)
                {
                    result.passed = false;
                    result.errorMessage = "Unknown exception";
                    LOG_ERROR((std::string("[FAILED] ") + test->GetName() + " - Unknown exception").c_str());
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
                LOG_INFO("Total: " + std::to_string(results.size()) + " | Passed: " + std::to_string(passed) + " | Failed: " + std::to_string(results.size() - passed));
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
