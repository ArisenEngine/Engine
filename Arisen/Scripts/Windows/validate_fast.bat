@echo off
setlocal

set "SCRIPT_ROOT=%~dp0"
for %%I in ("%SCRIPT_ROOT%..\..") do set "ENGINE_ROOT=%%~fI"
for %%I in ("%ENGINE_ROOT%\..") do set "REPO_ROOT=%%~fI"

pushd "%REPO_ROOT%" >nul
if errorlevel 1 (
    echo [ERROR] Failed to enter repository root: %REPO_ROOT%
    exit /b 1
)

echo [Arisen] Fast validation started.

call :run dotnet test "Arisen\External\ArisenBuildTool.Tests\ArisenBuildTool.Tests.csproj"
if errorlevel 1 goto :fail

call :run dotnet test "Arisen\ArisenKernel.Tests\ArisenKernel.Tests.csproj"
if errorlevel 1 goto :fail

call :run dotnet test "Arisen\Editor\ArisenLauncher.Tests\ArisenLauncher.Tests.csproj"
if errorlevel 1 goto :fail

call :run dotnet run --project "Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj" -- validate --workspace "Arisen\Development\PackageGame" --profile Development
if errorlevel 1 goto :fail

call :run dotnet run --project "Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj" -- validate --workspace "Arisen\Development\PackageGame" --profile Production
if errorlevel 1 goto :fail

call :run dotnet run --project "Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj" -- validate --workspace "Arisen\Development\PackageGame" --profile RHIVulkanTesting
if errorlevel 1 goto :fail

echo [Arisen] Fast validation succeeded.
popd >nul
exit /b 0

:run
echo.
echo [Arisen] Running: %*
%*
if errorlevel 1 (
    echo [ERROR] Command failed: %*
    exit /b 1
)
exit /b 0

:fail
popd >nul
exit /b 1

pause
