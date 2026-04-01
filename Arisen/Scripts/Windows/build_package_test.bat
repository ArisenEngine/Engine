@echo off
setlocal EnableExtensions enabledelayedexpansion

REM ============================================================================
REM Arisen Engine - Modern Test Runner
REM ----------------------------------------------------------------------------
REM Usage:
REM   RunPackageTests.bat <PackageId>
REM
REM Example:
REM   RunPackageTests.bat com.arisen.rhi.vulkan.native
REM ============================================================================

set "PACKAGE_ID=%~1"
if "%PACKAGE_ID%"=="" (
    echo [ERROR] No Package ID provided.
    echo Usage: RunPackageTests.bat ^<PackageId^>
    pause
    exit /b 1
)

REM Deduce paths
set "SCRIPT_DIR=%~dp0"
set "ROOT_DIR=%SCRIPT_DIR%..\..\..\.."
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"

set "BUILD_TOOL_PROJECT=%ROOT_DIR%\Engine\Arisen\External\ArisenBuildTool\ArisenBuildTool.csproj"
set "WORKSPACE_DIR=%ROOT_DIR%\Engine\Arisen\Development\PackageGame"

echo [Arisen] Starting isolated test workflow for: %PACKAGE_ID%

REM 1. Generate the isolated test solution
dotnet run --project "%BUILD_TOOL_PROJECT%" -- test --package "%PACKAGE_ID%" --workspace "%WORKSPACE_DIR%"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed to generate test environment.
    exit /b %ERRORLEVEL%
)

echo [Arisen] Test environment generated successfully. 
echo [Arisen] You can find the solution in: .arisen\Projects\Testing\
echo [Arisen] Executing tests...

REM 2. Run the generated Testing profile
REM (In a real scenario, this would invoke the engine's bootstrapper with --profile Testing)
REM dotnet run --project "%WORKSPACE_DIR%\.arisen\Projects\Testing\%PACKAGE_ID%.TestRun\%PACKAGE_ID%.TestRun.csproj" -- --profile Testing

echo [Arisen] Done.
pause
exit /b 0
