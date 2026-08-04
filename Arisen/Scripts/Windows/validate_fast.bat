@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "ARISEN_NO_PAUSE="
if /i "%~1"=="--no-pause" set "ARISEN_NO_PAUSE=1"
if defined CI set "ARISEN_NO_PAUSE=1"

set "SCRIPT_ROOT=%~dp0"
for %%I in ("%SCRIPT_ROOT%..\..") do set "ENGINE_ROOT=%%~fI"
for %%I in ("%ENGINE_ROOT%\..") do set "REPO_ROOT=%%~fI"

pushd "%REPO_ROOT%" >nul
if errorlevel 1 (
    echo [ERROR] Failed to enter repository root: %REPO_ROOT%
    set "EXIT_CODE=1"
    goto :finish_no_pop
)

echo [Arisen] Fast validation started.
echo [Arisen] Repository root: %REPO_ROOT%

call :run dotnet test "Arisen\External\ArisenBuildTool.Tests\ArisenBuildTool.Tests.csproj"
if errorlevel 1 goto :fail

call :run dotnet test "Arisen\ArisenKernel.Tests\ArisenKernel.Tests.csproj"
if errorlevel 1 goto :fail

call :run dotnet test "Arisen\Editor\ArisenLauncher.Tests\ArisenLauncher.Tests.csproj"
if errorlevel 1 goto :fail

call :run dotnet test "Arisen\Com.Arisen.Rendering.Tests\Com.Arisen.Rendering.Tests.csproj" --filter "Category!=AllocationSensitive"
if errorlevel 1 goto :fail

call :run_rendering_allocation_tests
if errorlevel 1 goto :fail

call :run dotnet run --project "Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj" -- validate --workspace "Arisen\Development\PackageGame" --profile Editor
if errorlevel 1 goto :fail

call :run dotnet run --project "Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj" -- validate --workspace "Arisen\Development\PackageGame" --profile Development
if errorlevel 1 goto :fail

call :run dotnet run --project "Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj" -- validate --workspace "Arisen\Development\PackageGame" --profile Production
if errorlevel 1 goto :fail

call :run dotnet run --project "Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj" -- validate --workspace "Arisen\Development\PackageGame" --profile RHIVulkanTesting
if errorlevel 1 goto :fail

echo [Arisen] Fast validation succeeded.
set "EXIT_CODE=0"
goto :finish

:run_rendering_allocation_tests
setlocal
set "DOTNET_TieredCompilation=0"
echo.
echo [Arisen] Allocation-sensitive rendering tests use a fresh Release host with tiered compilation disabled.
call :run dotnet test "Arisen\Com.Arisen.Rendering.Tests\Com.Arisen.Rendering.Tests.csproj" --configuration Release --filter "Category=AllocationSensitive"
set "ALLOCATION_TEST_EXIT_CODE=%ERRORLEVEL%"
endlocal & exit /b %ALLOCATION_TEST_EXIT_CODE%

:run
echo.
echo [Arisen] Running: %*
%*
if errorlevel 1 (
    echo [ERROR] Command failed: %*
    exit /b 1
)
echo [Arisen] Passed: %*
exit /b 0

:fail
set "EXIT_CODE=1"
echo.
echo [Arisen] Fast validation failed.

:finish
popd >nul

:finish_no_pop
echo.
if "%EXIT_CODE%"=="0" (
    echo [Arisen] RESULT: SUCCESS
) else (
    echo [Arisen] RESULT: FAILED
)

if not defined ARISEN_NO_PAUSE (
    echo.
    echo Press any key to close this validation window...
    pause >nul
)

exit /b %EXIT_CODE%
