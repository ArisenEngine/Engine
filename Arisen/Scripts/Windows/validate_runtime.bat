@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_ROOT=%~dp0"
for %%I in ("%SCRIPT_ROOT%..\..") do set "ENGINE_ROOT=%%~fI"
for %%I in ("%ENGINE_ROOT%\..") do set "REPO_ROOT=%%~fI"

set "PROFILES=Editor Development Production RHIVulkanTesting"
set "CONFIG=Debug"
set "FRAMES=1"
set "SMOKE_MODE=scene"
set "RUN_FAST=1"
set "GPU_SMOKE=auto"
set "GPU_AVAILABLE=0"
set "GPU_PROBE_REASON=not probed"
set "SMOKE_RUNS=0"
set "SMOKE_SKIPS=0"
set "CPU_FALLBACK_RUNS=0"
set "FAILURE_STAGE="
set "FAILED_PROFILE="
set "FAILURE_MESSAGE="
set "ARISEN_NO_PAUSE="
if defined CI set "ARISEN_NO_PAUSE=1"

:parse_args
if "%~1"=="" goto :end_parse
if /i "%~1"=="--profile" (
    set "PROFILES=%~2"
    shift
) else if /i "%~1"=="--config" (
    set "CONFIG=%~2"
    shift
) else if /i "%~1"=="--frames" (
    set "FRAMES=%~2"
    shift
) else if /i "%~1"=="--smoke-mode" (
    set "SMOKE_MODE=%~2"
    shift
) else if /i "%~1"=="--gpu-smoke" (
    set "GPU_SMOKE=%~2"
    shift
) else if /i "%~1"=="--require-gpu" (
    set "GPU_SMOKE=required"
) else if /i "%~1"=="--skip-gpu" (
    set "GPU_SMOKE=skip"
) else if /i "%~1"=="--skip-fast" (
    set "RUN_FAST=0"
) else if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
) else (
    echo [ERROR] Unknown argument: %~1
    set "EXIT_CODE=1"
    goto :finish_no_pop
)
shift
goto :parse_args
:end_parse

set "WORKSPACE_DIR=%ENGINE_ROOT%\Development\PackageGame"
set "BUILD_TOOL_CSPROJ=%ENGINE_ROOT%\External\ArisenBuildTool\ArisenBuildTool.csproj"
set "LOG_DIR=%WORKSPACE_DIR%\.arisen\Logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>nul
for /f %%I in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "RUN_TIMESTAMP=%%I"
set "SUMMARY_PATH=%LOG_DIR%\validate-runtime-%CONFIG%-latest.json"
set "SUMMARY_TIMESTAMP_PATH=%LOG_DIR%\validate-runtime-%CONFIG%-%RUN_TIMESTAMP%.json"
set "PROFILE_RESULTS_JSONL=%LOG_DIR%\validate-runtime-%CONFIG%-%RUN_TIMESTAMP%.profiles.jsonl"
type nul > "%PROFILE_RESULTS_JSONL%"

pushd "%REPO_ROOT%" >nul
if errorlevel 1 (
    echo [ERROR] Failed to enter repository root: %REPO_ROOT%
    set "EXIT_CODE=1"
    goto :finish_no_pop
)

echo [Arisen] Runtime validation started.
echo [Arisen] Repository root: %REPO_ROOT%
echo [Arisen] Profiles: %PROFILES%
echo [Arisen] Configuration: %CONFIG%
echo [Arisen] Smoke mode: %SMOKE_MODE%
echo [Arisen] Smoke frames: %FRAMES%
echo [Arisen] GPU smoke policy: %GPU_SMOKE%

call :validate_smoke_mode
if errorlevel 1 goto :fail

call :validate_gpu_policy
if errorlevel 1 goto :fail

call :probe_vulkan
if errorlevel 1 goto :fail

if "%RUN_FAST%"=="1" (
    call "%SCRIPT_ROOT%validate_fast.bat" --no-pause
    if errorlevel 1 (
        set "FAILURE_STAGE=fast validation"
        set "FAILURE_MESSAGE=validate_fast.bat failed"
        goto :fail
    )
)

for %%P in (%PROFILES%) do (
    call :validate_profile "%%P"
    if errorlevel 1 goto :fail
)

echo [Arisen] Runtime validation succeeded.
echo [Arisen] Runtime smoke runs: %SMOKE_RUNS%
echo [Arisen] Runtime smoke skips: %SMOKE_SKIPS%
echo [Arisen] CPU fallback validations: %CPU_FALLBACK_RUNS%
set "EXIT_CODE=0"
goto :finish

:validate_smoke_mode
if /i "%SMOKE_MODE%"=="boot" exit /b 0
if /i "%SMOKE_MODE%"=="scene" exit /b 0
if /i "%SMOKE_MODE%"=="hot-reload" exit /b 0
if /i "%SMOKE_MODE%"=="hotreload" (
    set "SMOKE_MODE=hot-reload"
    exit /b 0
)

echo [ERROR] Invalid --smoke-mode value: %SMOKE_MODE%
echo [ERROR] Expected one of: boot, scene, hot-reload
set "FAILURE_STAGE=smoke mode"
set "FAILURE_MESSAGE=Invalid --smoke-mode value: %SMOKE_MODE%"
exit /b 1

:validate_gpu_policy
if /i "%GPU_SMOKE%"=="auto" exit /b 0
if /i "%GPU_SMOKE%"=="required" exit /b 0
if /i "%GPU_SMOKE%"=="skip" exit /b 0

echo [ERROR] Invalid --gpu-smoke value: %GPU_SMOKE%
echo [ERROR] Expected one of: auto, required, skip
set "FAILURE_STAGE=gpu policy"
set "FAILURE_MESSAGE=Invalid --gpu-smoke value: %GPU_SMOKE%"
exit /b 1

:probe_vulkan
if /i "%GPU_SMOKE%"=="skip" (
    set "GPU_AVAILABLE=0"
    set "GPU_PROBE_REASON=disabled by --gpu-smoke skip"
    echo [Arisen] Vulkan smoke probe skipped by policy.
    exit /b 0
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$cmd = Get-Command vulkaninfo -ErrorAction SilentlyContinue; if (-not $cmd) { Write-Host '[Arisen] Vulkan probe: vulkaninfo not found.'; exit 2 }; & $cmd.Source --summary *> $null; if ($LASTEXITCODE -eq 0) { Write-Host '[Arisen] Vulkan probe: available via vulkaninfo.'; exit 0 }; Write-Host ('[Arisen] Vulkan probe: vulkaninfo failed with exit code {0}.' -f $LASTEXITCODE); exit 1"
set "VULKAN_PROBE_EXIT=%ERRORLEVEL%"
if "%VULKAN_PROBE_EXIT%"=="0" (
    set "GPU_AVAILABLE=1"
    set "GPU_PROBE_REASON=vulkaninfo succeeded"
    exit /b 0
)

set "GPU_AVAILABLE=0"
if "%VULKAN_PROBE_EXIT%"=="2" (
    set "GPU_PROBE_REASON=vulkaninfo not found"
) else (
    set "GPU_PROBE_REASON=vulkaninfo failed with exit code %VULKAN_PROBE_EXIT%"
)

if /i "%GPU_SMOKE%"=="required" (
    echo [ERROR] GPU smoke is required, but Vulkan is unavailable: %GPU_PROBE_REASON%
    set "FAILURE_STAGE=vulkan probe"
    set "FAILURE_MESSAGE=GPU smoke is required, but Vulkan is unavailable: %GPU_PROBE_REASON%"
    exit /b 1
)

echo [Arisen] Vulkan smoke unavailable in auto mode: %GPU_PROBE_REASON%
exit /b 0

:validate_profile
set "CURRENT_PROFILE=%~1"
set "CURRENT_PROFILE_LOG=%LOG_DIR%\smoke-cli-%CURRENT_PROFILE%-%CONFIG%-%RUN_TIMESTAMP%.log"
echo.
echo [Arisen] --------------------------------------------------
echo [Arisen] Runtime profile smoke: %CURRENT_PROFILE% [%CONFIG%]
echo [Arisen] --------------------------------------------------

call "%SCRIPT_ROOT%build_workspace.bat" --manifest "%ENGINE_ROOT%\Development\PackageGame\manifest.json" --profile "%CURRENT_PROFILE%" --config "%CONFIG%" --no-pause
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=build"
    set "FAILURE_MESSAGE=Build failed for profile %CURRENT_PROFILE%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=0"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH="
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

set "BIN_DIR=%ENGINE_ROOT%\Development\PackageGame\.arisen\bin\%CURRENT_PROFILE%\%CONFIG%"
set "EXE_PATH=%BIN_DIR%\PackageGame.exe"
set "RESOLVED_MANIFEST=%BIN_DIR%\manifest.resolved.json"

if not exist "%RESOLVED_MANIFEST%" (
    echo [ERROR] Resolved manifest not found: %RESOLVED_MANIFEST%
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=resolved manifest"
    set "FAILURE_MESSAGE=Resolved manifest not found: %RESOLVED_MANIFEST%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=0"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH="
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

if not exist "%EXE_PATH%" (
    echo [ERROR] Runtime executable not found: %EXE_PATH%
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=runtime executable"
    set "FAILURE_MESSAGE=Runtime executable not found: %EXE_PATH%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=0"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH="
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

echo [Arisen] Validating native runtime output: %BIN_DIR%
dotnet run --project "%BUILD_TOOL_CSPROJ%" -- validate-native-output --resolved-manifest "%RESOLVED_MANIFEST%" --output-dir "%BIN_DIR%" --configuration "%CONFIG%"
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=native output validation"
    set "FAILURE_MESSAGE=Native output validation failed for profile %CURRENT_PROFILE%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=0"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH="
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

call :profile_requires_vulkan "%RESOLVED_MANIFEST%"
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=resolved manifest inspection"
    set "FAILURE_MESSAGE=Failed to inspect resolved manifest for Vulkan package: %RESOLVED_MANIFEST%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=0"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH="
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

if "%PROFILE_REQUIRES_VULKAN%"=="1" if "%GPU_AVAILABLE%"=="0" (
    echo [SKIP] Runtime smoke for profile %CURRENT_PROFILE% requires Vulkan. Reason: %GPU_PROBE_REASON%
    echo [Arisen] CPU fallback validation passed for profile %CURRENT_PROFILE% after package graph, build output, and native output checks.
    set /a SMOKE_SKIPS+=1
    set /a CPU_FALLBACK_RUNS+=1
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=cpu-fallback-passed"
    set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
    set "RESULT_EXIT_CODE=0"
    set "RESULT_LOG_PATH="
    set "RESULT_MESSAGE=GPU runtime smoke skipped; CPU fallback validation passed: %GPU_PROBE_REASON%"
    call :record_result
    exit /b 0
)

echo [Arisen] Running runtime smoke: %EXE_PATH%
pushd "%BIN_DIR%" >nul
"%EXE_PATH%" --workspace "%ENGINE_ROOT%\Development\PackageGame" --profile "%CURRENT_PROFILE%" --smoke-mode "%SMOKE_MODE%" --frames "%FRAMES%" > "%CURRENT_PROFILE_LOG%" 2>&1
set "SMOKE_EXIT=%ERRORLEVEL%"
popd >nul
type "%CURRENT_PROFILE_LOG%"

if not "%SMOKE_EXIT%"=="0" (
    echo [ERROR] Runtime smoke for profile %CURRENT_PROFILE% failed with exit code %SMOKE_EXIT%.
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=runtime smoke"
    set "FAILURE_MESSAGE=Runtime smoke for profile %CURRENT_PROFILE% failed with exit code %SMOKE_EXIT%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
    set "RESULT_EXIT_CODE=%SMOKE_EXIT%"
    set "RESULT_LOG_PATH=%CURRENT_PROFILE_LOG%"
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

set /a SMOKE_RUNS+=1
set "RESULT_PROFILE=%CURRENT_PROFILE%"
set "RESULT_STATUS=passed"
set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
set "RESULT_EXIT_CODE=%SMOKE_EXIT%"
set "RESULT_LOG_PATH=%CURRENT_PROFILE_LOG%"
set "RESULT_MESSAGE="
call :record_result
echo [Arisen] Runtime profile smoke passed: %CURRENT_PROFILE%
exit /b 0

:profile_requires_vulkan
set "PROFILE_REQUIRES_VULKAN=0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$m = Get-Content -LiteralPath '%~1' -Raw | ConvertFrom-Json; if ($m.ResolvedPackages | Where-Object { $_.Id -eq 'com.arisen.rhi.vulkan.native' }) { exit 10 }; exit 0"
set "PROFILE_VULKAN_EXIT=%ERRORLEVEL%"
if "%PROFILE_VULKAN_EXIT%"=="10" (
    set "PROFILE_REQUIRES_VULKAN=1"
    exit /b 0
)
if "%PROFILE_VULKAN_EXIT%"=="0" exit /b 0

echo [ERROR] Failed to inspect resolved manifest for Vulkan package: %~1
exit /b 1

:record_result
powershell -NoProfile -ExecutionPolicy Bypass -Command "$exitCode = $null; if ($env:RESULT_EXIT_CODE -ne '') { $exitCode = [int]$env:RESULT_EXIT_CODE }; $logPath = $null; if ($env:RESULT_LOG_PATH -ne '') { $logPath = $env:RESULT_LOG_PATH }; $message = $null; if ($env:RESULT_MESSAGE -ne '') { $message = $env:RESULT_MESSAGE }; $result = [ordered]@{ profile = $env:RESULT_PROFILE; status = $env:RESULT_STATUS; smokeMode = $env:SMOKE_MODE; requiresVulkan = ($env:RESULT_REQUIRES_VULKAN -eq '1'); exitCode = $exitCode; logPath = $logPath; message = $message }; $json = $result | ConvertTo-Json -Compress; Add-Content -LiteralPath $env:PROFILE_RESULTS_JSONL -Value $json"
exit /b %ERRORLEVEL%

:write_summary
powershell -NoProfile -ExecutionPolicy Bypass -Command "$profiles = @(); if (Test-Path -LiteralPath $env:PROFILE_RESULTS_JSONL) { $profiles = Get-Content -LiteralPath $env:PROFILE_RESULTS_JSONL | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_ | ConvertFrom-Json } }; $failure = $null; if ($env:EXIT_CODE -ne '0') { $failureStage = $null; if ($env:FAILURE_STAGE -ne '') { $failureStage = $env:FAILURE_STAGE }; $failureProfile = $null; if ($env:FAILED_PROFILE -ne '') { $failureProfile = $env:FAILED_PROFILE }; $failureMessage = 'Runtime validation failed'; if ($env:FAILURE_MESSAGE -ne '') { $failureMessage = $env:FAILURE_MESSAGE }; $failure = [ordered]@{ stage = $failureStage; profile = $failureProfile; message = $failureMessage } }; $summary = [ordered]@{ schemaVersion = 2; capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o'); repositoryRoot = $env:REPO_ROOT; workspacePath = $env:WORKSPACE_DIR; configuration = $env:CONFIG; requestedProfiles = @($env:PROFILES -split ' ' | Where-Object { $_ -ne '' }); smokeMode = $env:SMOKE_MODE; smokeFrames = [int]$env:FRAMES; gpuSmokePolicy = $env:GPU_SMOKE; gpuAvailable = ($env:GPU_AVAILABLE -eq '1'); gpuProbeReason = $env:GPU_PROBE_REASON; succeeded = ($env:EXIT_CODE -eq '0'); exitCode = [int]$env:EXIT_CODE; smokeRuns = [int]$env:SMOKE_RUNS; smokeSkips = [int]$env:SMOKE_SKIPS; cpuFallbackRuns = [int]$env:CPU_FALLBACK_RUNS; failure = $failure; profileResults = @($profiles) }; $json = $summary | ConvertTo-Json -Depth 8; Set-Content -LiteralPath $env:SUMMARY_TIMESTAMP_PATH -Value $json -Encoding UTF8; Copy-Item -LiteralPath $env:SUMMARY_TIMESTAMP_PATH -Destination $env:SUMMARY_PATH -Force"
if "%ERRORLEVEL%"=="0" (
    echo [Arisen] Runtime validation summary: %SUMMARY_PATH%
)
exit /b %ERRORLEVEL%

:fail
if not defined FAILURE_MESSAGE set "FAILURE_MESSAGE=Runtime validation failed"
set "EXIT_CODE=1"
echo.
echo [Arisen] Runtime validation failed.

:finish
popd >nul

:finish_no_pop
echo.
if "%EXIT_CODE%"=="0" (
    echo [Arisen] RESULT: SUCCESS
) else (
    echo [Arisen] RESULT: FAILED
)

if defined SUMMARY_PATH (
    call :write_summary
)

if not defined ARISEN_NO_PAUSE (
    echo.
    echo Press any key to close this validation window...
    pause >nul
)

exit /b %EXIT_CODE%
