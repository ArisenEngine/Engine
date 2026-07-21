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
set "EDITOR_VIEWPORT_SMOKE_RUNS=0"
set "RELOCATED_PRODUCTION_SMOKE_RUNS=0"
set "WORLD_STREAMING_SMOKE_RUNS=0"
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
echo [Arisen] Editor viewport smoke runs: %EDITOR_VIEWPORT_SMOKE_RUNS%
echo [Arisen] Relocated Production smoke runs: %RELOCATED_PRODUCTION_SMOKE_RUNS%
echo [Arisen] World-streaming smoke runs: %WORLD_STREAMING_SMOKE_RUNS%
set "EXIT_CODE=0"
goto :finish

:validate_smoke_mode
if /i "%SMOKE_MODE%"=="boot" exit /b 0
if /i "%SMOKE_MODE%"=="scene" exit /b 0
if /i "%SMOKE_MODE%"=="hot-reload" exit /b 0
if /i "%SMOKE_MODE%"=="world-streaming" exit /b 0
if /i "%SMOKE_MODE%"=="hotreload" (
    set "SMOKE_MODE=hot-reload"
    exit /b 0
)

echo [ERROR] Invalid --smoke-mode value: %SMOKE_MODE%
echo [ERROR] Expected one of: boot, scene, hot-reload, world-streaming
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
set "CURRENT_VISUAL_SUMMARY_REQUESTED=0"
set "CURRENT_VISUAL_SUMMARY_ARGS="
set "CURRENT_VISUAL_SUMMARY_PATH="
set "CURRENT_VISUAL_SUMMARY_PASSED="
set "CURRENT_EDITOR_VIEWPORT_SMOKE_REQUESTED=0"
set "CURRENT_EDITOR_VIEWPORT_SMOKE_PATH="
set "CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH="
set "CURRENT_EDITOR_VIEWPORT_SMOKE_PASSED="
set "CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE="
set "CURRENT_RELOCATED_PRODUCTION_REQUESTED=0"
set "CURRENT_RELOCATED_PRODUCTION_LOG_PATH="
set "CURRENT_RELOCATED_PRODUCTION_SUMMARY_PATH="
set "CURRENT_RELOCATED_PRODUCTION_PASSED="
set "CURRENT_RELOCATED_PRODUCTION_EXIT_CODE="
set "CURRENT_WORLD_STREAMING_REQUESTED=0"
set "CURRENT_WORLD_STREAMING_SUMMARY_PATH="
set "CURRENT_WORLD_STREAMING_VISUAL_BASE_PATH="
set "CURRENT_WORLD_STREAMING_LOG_PATH="
set "CURRENT_WORLD_STREAMING_PASSED="
set "CURRENT_WORLD_STREAMING_EXIT_CODE="
set "CURRENT_WORLD_STREAMING_REQUIRED=0"
if /i "%CURRENT_PROFILE%"=="Development" set "CURRENT_WORLD_STREAMING_REQUIRED=1"
if /i "%CURRENT_PROFILE%"=="Production" set "CURRENT_WORLD_STREAMING_REQUIRED=1"
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

call :prepare_workspace_runtime_assets
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=runtime asset preparation"
    set "FAILURE_MESSAGE=Explicit runtime asset cooking failed for profile %CURRENT_PROFILE%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH=%CURRENT_ASSET_PREPARATION_LOG%"
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

call :configure_visual_summary
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=visual summary preparation"
    set "FAILURE_MESSAGE=Failed to prepare visual-summary capture for profile %CURRENT_PROFILE%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH="
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

echo [Arisen] Running runtime smoke: %EXE_PATH%
set "CURRENT_RUNTIME_SMOKE_VULKAN_LOG_REQUIRED=%PROFILE_REQUIRES_VULKAN%"
if /i "%CURRENT_PROFILE%"=="Editor" set "CURRENT_RUNTIME_SMOKE_VULKAN_LOG_REQUIRED=0"
call :prepare_vulkan_validation_log "%CURRENT_RUNTIME_SMOKE_VULKAN_LOG_REQUIRED%"
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=Vulkan validation log preparation"
    set "FAILURE_MESSAGE=Failed to prepare Vulkan validation log for profile %CURRENT_PROFILE%"
    exit /b 1
)
for /f "delims=" %%I in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Date).ToUniversalTime().ToString('o')"') do set "CURRENT_SMOKE_STARTED_UTC=%%I"
pushd "%BIN_DIR%" >nul
if /i "%CURRENT_PROFILE%"=="Production" (
    "%EXE_PATH%" --smoke-mode "%SMOKE_MODE%" --frames "%FRAMES%" !CURRENT_VISUAL_SUMMARY_ARGS! > "%CURRENT_PROFILE_LOG%" 2>&1
) else (
    "%EXE_PATH%" --workspace "%ENGINE_ROOT%\Development\PackageGame" --profile "%CURRENT_PROFILE%" --smoke-mode "%SMOKE_MODE%" --frames "%FRAMES%" !CURRENT_VISUAL_SUMMARY_ARGS! > "%CURRENT_PROFILE_LOG%" 2>&1
)
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

call :validate_vulkan_validation_log "%CURRENT_RUNTIME_SMOKE_VULKAN_LOG_REQUIRED%"
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=Vulkan validation"
    set "FAILURE_MESSAGE=Vulkan validation log check failed for profile %CURRENT_PROFILE%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH=%CURRENT_PROFILE_LOG%"
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

call :validate_runtime_asset_policy
if errorlevel 1 (
    echo [ERROR] Runtime asset source-access policy validation failed for profile %CURRENT_PROFILE%.
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=runtime asset policy"
    set "FAILURE_MESSAGE=Runtime asset source-access policy validation failed for profile %CURRENT_PROFILE%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH=%CURRENT_PROFILE_LOG%"
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

if "!CURRENT_VISUAL_SUMMARY_REQUESTED!"=="1" (
    call :validate_visual_summary
    if errorlevel 1 (
        echo [ERROR] Visual-summary validation failed for profile %CURRENT_PROFILE%.
        set "FAILED_PROFILE=%CURRENT_PROFILE%"
        set "FAILURE_STAGE=visual summary validation"
        set "FAILURE_MESSAGE=Visual-summary validation failed for profile %CURRENT_PROFILE%"
        set "RESULT_PROFILE=%CURRENT_PROFILE%"
        set "RESULT_STATUS=failed"
        set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
        set "RESULT_EXIT_CODE=1"
        set "RESULT_LOG_PATH=%CURRENT_PROFILE_LOG%"
        set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
        call :record_result
        exit /b 1
    )
    set "CURRENT_VISUAL_SUMMARY_PASSED=1"
)

if "!CURRENT_WORLD_STREAMING_REQUIRED!"=="1" (
    call :run_world_streaming_smoke
    if errorlevel 1 (
        echo [ERROR] World-streaming smoke failed for profile %CURRENT_PROFILE%.
        set "FAILED_PROFILE=%CURRENT_PROFILE%"
        set "FAILURE_STAGE=world-streaming smoke"
        set "FAILURE_MESSAGE=World-streaming smoke failed for profile %CURRENT_PROFILE%"
        set "RESULT_PROFILE=%CURRENT_PROFILE%"
        set "RESULT_STATUS=failed"
        set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
        set "RESULT_EXIT_CODE=1"
        set "RESULT_LOG_PATH=%CURRENT_WORLD_STREAMING_LOG_PATH%"
        set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
        call :record_result
        exit /b 1
    )
    set /a WORLD_STREAMING_SMOKE_RUNS+=1
)

if /i "!CURRENT_PROFILE!"=="Production" (
    call :run_relocated_production_validation
    if errorlevel 1 (
        echo [ERROR] Relocated Production validation failed.
        set "FAILED_PROFILE=%CURRENT_PROFILE%"
        set "FAILURE_STAGE=relocated Production validation"
        set "FAILURE_MESSAGE=Copied Production output validation failed for %CURRENT_PROFILE%"
        set "RESULT_PROFILE=%CURRENT_PROFILE%"
        set "RESULT_STATUS=failed"
        set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
        set "RESULT_EXIT_CODE=1"
        set "RESULT_LOG_PATH=%CURRENT_RELOCATED_PRODUCTION_LOG_PATH%"
        set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
        call :record_result
        exit /b 1
    )
    set /a RELOCATED_PRODUCTION_SMOKE_RUNS+=1
    set /a WORLD_STREAMING_SMOKE_RUNS+=1
)

call :configure_editor_viewport_smoke
if errorlevel 1 (
    set "FAILED_PROFILE=%CURRENT_PROFILE%"
    set "FAILURE_STAGE=editor viewport smoke preparation"
    set "FAILURE_MESSAGE=Failed to prepare editor viewport smoke for profile %CURRENT_PROFILE%"
    set "RESULT_PROFILE=%CURRENT_PROFILE%"
    set "RESULT_STATUS=failed"
    set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
    set "RESULT_EXIT_CODE=1"
    set "RESULT_LOG_PATH=%CURRENT_PROFILE_LOG%"
    set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
    call :record_result
    exit /b 1
)

if "!CURRENT_EDITOR_VIEWPORT_SMOKE_REQUESTED!"=="1" (
    set /a EDITOR_VIEWPORT_SMOKE_RUNS+=1
    call :run_editor_viewport_smoke
    if errorlevel 1 (
        echo [ERROR] Editor viewport smoke failed for profile %CURRENT_PROFILE%.
        set "FAILED_PROFILE=%CURRENT_PROFILE%"
        set "FAILURE_STAGE=editor viewport smoke"
        set "FAILURE_MESSAGE=Editor viewport smoke failed for profile %CURRENT_PROFILE% with exit code !CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE!"
        set "RESULT_PROFILE=%CURRENT_PROFILE%"
        set "RESULT_STATUS=failed"
        set "RESULT_REQUIRES_VULKAN=%PROFILE_REQUIRES_VULKAN%"
        set "RESULT_EXIT_CODE=1"
        set "RESULT_LOG_PATH=%CURRENT_PROFILE_LOG%"
        set "RESULT_MESSAGE=!FAILURE_MESSAGE!"
        call :record_result
        exit /b 1
    )
    set "CURRENT_EDITOR_VIEWPORT_SMOKE_PASSED=1"
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

:validate_runtime_asset_policy
set "EXPECTED_SOURCE_ACCESS=Disabled"
if /i "%CURRENT_PROFILE%"=="Editor" set "EXPECTED_SOURCE_ACCESS=EditorAuthoring"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$start = [DateTime]::Parse($env:CURRENT_SMOKE_STARTED_UTC, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime(); $log = Get-ChildItem -LiteralPath (Join-Path $env:BIN_DIR 'logs') -Filter 'player_*.log' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTimeUtc -ge $start.AddSeconds(-1) } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1; if ($null -eq $log) { Write-Host '[ERROR] Runtime smoke produced no fresh player log for source-access validation.'; exit 1 }; $text = Get-Content -LiteralPath $log.FullName -Raw; $expected = 'with {0} source access' -f $env:EXPECTED_SOURCE_ACCESS; if ($text.IndexOf($expected, [StringComparison]::Ordinal) -lt 0) { Write-Host ('[ERROR] Runtime log {0} does not contain expected source-access policy: {1}' -f $log.FullName, $expected); exit 1 }; Write-Host ('[Arisen] Runtime asset policy passed: {0} ({1})' -f $env:EXPECTED_SOURCE_ACCESS, $log.FullName)"
exit /b %ERRORLEVEL%

:prepare_workspace_runtime_assets
set "CURRENT_ASSET_PREPARATION_LOG="
if /i "%CURRENT_PROFILE%"=="Editor" exit /b 0
if /i "%CURRENT_PROFILE%"=="Production" exit /b 0
set "CURRENT_ASSET_PREPARATION_LOG=%LOG_DIR%\cook-runtime-assets-%CURRENT_PROFILE%-%CONFIG%-%RUN_TIMESTAMP%.log"
echo [Arisen] Explicitly cooking runtime assets for cooked-only %CURRENT_PROFILE% smoke.
pushd "%BIN_DIR%" >nul
"%EXE_PATH%" --arisen-cook-runtime-assets --workspace "%WORKSPACE_DIR%" --profile "%CURRENT_PROFILE%" --configuration "%CONFIG%" --runtime-identifier win-x64 > "!CURRENT_ASSET_PREPARATION_LOG!" 2>&1
set "ASSET_PREPARATION_EXIT=!ERRORLEVEL!"
popd >nul
type "!CURRENT_ASSET_PREPARATION_LOG!"
if not "!ASSET_PREPARATION_EXIT!"=="0" (
    echo [ERROR] Runtime asset cooking for %CURRENT_PROFILE% failed with exit code !ASSET_PREPARATION_EXIT!.
    exit /b 1
)
exit /b 0

:prepare_vulkan_validation_log
set "CURRENT_VULKAN_VALIDATION_LOG="
if not "%~1"=="1" exit /b 0
set "CURRENT_VULKAN_VALIDATION_LOG=%BIN_DIR%\vk_validation.log"
if exist "!CURRENT_VULKAN_VALIDATION_LOG!" del /q "!CURRENT_VULKAN_VALIDATION_LOG!" >nul 2>nul
if exist "!CURRENT_VULKAN_VALIDATION_LOG!" (
    echo [ERROR] Failed to remove stale Vulkan validation log: !CURRENT_VULKAN_VALIDATION_LOG!
    exit /b 1
)
exit /b 0

:validate_vulkan_validation_log
if not "%~1"=="1" exit /b 0
if not exist "!CURRENT_VULKAN_VALIDATION_LOG!" (
    echo [ERROR] Vulkan validation log was not produced: !CURRENT_VULKAN_VALIDATION_LOG!
    exit /b 1
)
for %%I in ("!CURRENT_VULKAN_VALIDATION_LOG!") do set "CURRENT_VULKAN_VALIDATION_BYTES=%%~zI"
if not "!CURRENT_VULKAN_VALIDATION_BYTES!"=="0" (
    echo [ERROR] Vulkan validation log is not empty: !CURRENT_VULKAN_VALIDATION_LOG! ^(!CURRENT_VULKAN_VALIDATION_BYTES! bytes^)
    type "!CURRENT_VULKAN_VALIDATION_LOG!"
    exit /b 1
)
echo [Arisen] Vulkan validation log is empty: !CURRENT_VULKAN_VALIDATION_LOG!
exit /b 0

:run_world_streaming_smoke
set "CURRENT_WORLD_STREAMING_REQUESTED=1"
set "CURRENT_WORLD_STREAMING_SUMMARY_PATH=%LOG_DIR%\world-streaming-summary-%CURRENT_PROFILE%-latest.json"
set "CURRENT_WORLD_STREAMING_VISUAL_BASE_PATH=%LOG_DIR%\world-streaming-visual-%CURRENT_PROFILE%-latest.json"
set "CURRENT_WORLD_STREAMING_LOG_PATH=%LOG_DIR%\world-streaming-%CURRENT_PROFILE%-%CONFIG%-%RUN_TIMESTAMP%.log"
set "CURRENT_WORLD_STREAMING_EXIT_CODE="
set "CURRENT_WORLD_STREAMING_PASSED=0"
del /q "!CURRENT_WORLD_STREAMING_SUMMARY_PATH!" "!CURRENT_WORLD_STREAMING_LOG_PATH!" >nul 2>nul
for %%N in (before during after) do del /q "%LOG_DIR%\world-streaming-visual-%CURRENT_PROFILE%-latest.%%N.json" >nul 2>nul

call :prepare_vulkan_validation_log "%PROFILE_REQUIRES_VULKAN%"
if errorlevel 1 exit /b 1
echo [Arisen] Running bounded world-streaming smoke: %EXE_PATH%
pushd "%BIN_DIR%" >nul
if /i "%CURRENT_PROFILE%"=="Production" (
    "%EXE_PATH%" --smoke-mode world-streaming --frames 1 --smoke-summary-output "!CURRENT_WORLD_STREAMING_SUMMARY_PATH!" --visual-summary --visual-summary-output "!CURRENT_WORLD_STREAMING_VISUAL_BASE_PATH!" > "!CURRENT_WORLD_STREAMING_LOG_PATH!" 2>&1
) else (
    "%EXE_PATH%" --workspace "%WORKSPACE_DIR%" --profile "%CURRENT_PROFILE%" --smoke-mode world-streaming --frames 1 --smoke-summary-output "!CURRENT_WORLD_STREAMING_SUMMARY_PATH!" --visual-summary --visual-summary-output "!CURRENT_WORLD_STREAMING_VISUAL_BASE_PATH!" > "!CURRENT_WORLD_STREAMING_LOG_PATH!" 2>&1
)
set "CURRENT_WORLD_STREAMING_EXIT_CODE=!ERRORLEVEL!"
popd >nul
type "!CURRENT_WORLD_STREAMING_LOG_PATH!"
if not "!CURRENT_WORLD_STREAMING_EXIT_CODE!"=="0" exit /b 1

call :validate_vulkan_validation_log "%PROFILE_REQUIRES_VULKAN%"
if errorlevel 1 exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_ROOT%validate_world_streaming_summary.ps1" -SummaryPath "!CURRENT_WORLD_STREAMING_SUMMARY_PATH!" -ExpectedProfile "%CURRENT_PROFILE%" -ExpectedVisualCaptureCount 3
if errorlevel 1 exit /b 1
set "CURRENT_WORLD_STREAMING_PASSED=1"
exit /b 0

:run_relocated_production_validation
set "CURRENT_RELOCATED_PRODUCTION_REQUESTED=1"
set "CURRENT_RELOCATED_PRODUCTION_LOG_PATH=%LOG_DIR%\relocated-production-%CONFIG%-latest.log"
set "CURRENT_RELOCATED_PRODUCTION_SUMMARY_PATH=%LOG_DIR%\relocated-production-%CONFIG%-latest.json"
echo [Arisen] Running copied-output Production validation: %BIN_DIR%
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_ROOT%validate_relocated_production.ps1" -SourceRoot "%BIN_DIR%" -WorkspaceRoot "%WORKSPACE_DIR%" -SmokeMode "%SMOKE_MODE%" -Frames "%FRAMES%" -LogPath "!CURRENT_RELOCATED_PRODUCTION_LOG_PATH!" -SummaryPath "!CURRENT_RELOCATED_PRODUCTION_SUMMARY_PATH!"
set "CURRENT_RELOCATED_PRODUCTION_EXIT_CODE=%ERRORLEVEL%"
if "%ERRORLEVEL%"=="0" (
    set "CURRENT_RELOCATED_PRODUCTION_PASSED=1"
) else (
    set "CURRENT_RELOCATED_PRODUCTION_PASSED=0"
)
exit /b %CURRENT_RELOCATED_PRODUCTION_EXIT_CODE%

:configure_visual_summary
if /i not "!SMOKE_MODE!"=="scene" exit /b 0
if /i "!CURRENT_PROFILE!"=="Development" set "CURRENT_VISUAL_SUMMARY_REQUESTED=1"
if /i "!CURRENT_PROFILE!"=="Production" set "CURRENT_VISUAL_SUMMARY_REQUESTED=1"
if not "!CURRENT_VISUAL_SUMMARY_REQUESTED!"=="1" exit /b 0

set "CURRENT_VISUAL_SUMMARY_PATH=%LOG_DIR%\visual-summary-%CURRENT_PROFILE%-latest.json"
set "CURRENT_VISUAL_SUMMARY_ARGS=--visual-summary --visual-summary-output "!CURRENT_VISUAL_SUMMARY_PATH!""
if exist "!CURRENT_VISUAL_SUMMARY_PATH!" del /q "!CURRENT_VISUAL_SUMMARY_PATH!" >nul 2>nul
if exist "!CURRENT_VISUAL_SUMMARY_PATH!" (
    echo [ERROR] Failed to remove stale visual-summary artifact: !CURRENT_VISUAL_SUMMARY_PATH!
    exit /b 1
)

echo [Arisen] Visual-summary capture enabled: !CURRENT_VISUAL_SUMMARY_PATH!
exit /b 0

:validate_visual_summary
powershell -NoProfile -ExecutionPolicy Bypass -Command "$path = $env:CURRENT_VISUAL_SUMMARY_PATH; if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Write-Host ('[ERROR] Visual-summary artifact was not produced: {0}' -f $path); exit 1 }; try { $artifact = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } catch { Write-Host ('[ERROR] Visual-summary artifact is not valid JSON: {0}' -f $_.Exception.Message); exit 1 }; if ([int]$artifact.schemaVersion -ne 2) { Write-Host ('[ERROR] Visual-summary schema mismatch. Expected 2, received {0}.' -f $artifact.schemaVersion); exit 1 }; if ([string]$artifact.profile -cne $env:CURRENT_PROFILE) { Write-Host ('[ERROR] Visual-summary profile mismatch. Expected {0}, received {1}.' -f $env:CURRENT_PROFILE, $artifact.profile); exit 1 }; if ($artifact.passed -ne $true -or $artifact.checks.passed -ne $true) { Write-Host ('[ERROR] Visual-summary color checks did not pass: {0}' -f $path); exit 1 }; $depth = $artifact.depth; if ($null -eq $depth) { Write-Host '[ERROR] Visual-summary artifact is missing the required depth result.'; exit 1 }; if ($depth.passed -ne $true -or $depth.checks.passed -ne $true) { Write-Host ('[ERROR] Visual-summary depth checks did not pass: {0}' -f $path); exit 1 }; if ([uint32]$depth.width -ne [uint32]$artifact.width -or [uint32]$depth.height -ne [uint32]$artifact.height) { Write-Host ('[ERROR] Visual-summary depth dimensions {0}x{1} do not match color dimensions {2}x{3}.' -f $depth.width, $depth.height, $artifact.width, $artifact.height); exit 1 }; if ([string]$depth.format -cne 'FORMAT_D32_SFLOAT') { Write-Host ('[ERROR] Visual-summary depth format mismatch. Expected FORMAT_D32_SFLOAT, received {0}.' -f $depth.format); exit 1 }; if ([long]$depth.finiteDepthPixelCount -ne [long]$depth.pixelCount -or [long]$depth.normalizedDepthPixelCount -ne [long]$depth.pixelCount) { Write-Host '[ERROR] Visual-summary depth contains non-finite or out-of-range values.'; exit 1 }; if ([long]$depth.writtenDepthPixelCount -lt [long]$depth.checks.requiredWrittenDepthPixelCount) { Write-Host ('[ERROR] Visual-summary depth written coverage is too small: {0}/{1}.' -f $depth.writtenDepthPixelCount, $depth.checks.requiredWrittenDepthPixelCount); exit 1 }; if (@($depth.depthHistogram).Count -ne 16 -or @($depth.spatialDepthGrid).Count -ne 16) { Write-Host '[ERROR] Visual-summary depth distribution has an unexpected shape.'; exit 1 }; $histogramCount = [long]0; @($depth.depthHistogram) | ForEach-Object { $histogramCount += [long]$_ }; if ($histogramCount -ne [long]$depth.pixelCount) { Write-Host ('[ERROR] Visual-summary depth histogram covers {0} values, expected {1}.' -f $histogramCount, $depth.pixelCount); exit 1 }; Write-Host ('[Arisen] Visual-summary passed: {0}x{1}, color={2}, nonblank={3}/{4}, depth={5}, written={6}/{7}, clear={8}, output={9}' -f $artifact.width, $artifact.height, $artifact.format, $artifact.nonBlankPixelCount, $artifact.pixelCount, $depth.format, $depth.writtenDepthPixelCount, $depth.pixelCount, $depth.clearDepthPixelCount, $path)"
exit /b %ERRORLEVEL%

:configure_editor_viewport_smoke
if /i not "!SMOKE_MODE!"=="scene" exit /b 0
if /i not "!CURRENT_PROFILE!"=="Editor" exit /b 0

set "CURRENT_EDITOR_VIEWPORT_SMOKE_REQUESTED=1"
set "CURRENT_EDITOR_VIEWPORT_SMOKE_PATH=%LOG_DIR%\editor-viewport-summary-%CURRENT_PROFILE%-latest.json"
set "CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH=%LOG_DIR%\editor-viewport-smoke-%CURRENT_PROFILE%-%CONFIG%-%RUN_TIMESTAMP%.log"
if exist "!CURRENT_EDITOR_VIEWPORT_SMOKE_PATH!" del /q "!CURRENT_EDITOR_VIEWPORT_SMOKE_PATH!" >nul 2>nul
if exist "!CURRENT_EDITOR_VIEWPORT_SMOKE_PATH!" (
    echo [ERROR] Failed to remove stale editor viewport smoke artifact: !CURRENT_EDITOR_VIEWPORT_SMOKE_PATH!
    exit /b 1
)

echo [Arisen] Editor viewport smoke enabled: !CURRENT_EDITOR_VIEWPORT_SMOKE_PATH!
exit /b 0

:run_editor_viewport_smoke
echo [Arisen] Running bounded Avalonia editor viewport smoke: %EXE_PATH%
call :prepare_vulkan_validation_log "1"
if errorlevel 1 exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$stderrPath = $env:CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH + '.stderr'; Remove-Item -LiteralPath $env:CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH,$stderrPath -Force -ErrorAction SilentlyContinue; $quote = [char]34; $arguments = '--workspace {0}{1}{0} --profile {0}{2}{0} --editor-viewport-smoke --editor-viewport-smoke-timeout 30' -f $quote,$env:WORKSPACE_DIR,$env:CURRENT_PROFILE; try { $process = Start-Process -FilePath $env:EXE_PATH -ArgumentList $arguments -WorkingDirectory $env:BIN_DIR -PassThru -RedirectStandardOutput $env:CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH -RedirectStandardError $stderrPath; if (-not $process.WaitForExit(45000)) { try { $process.Kill() } catch {}; $process.WaitForExit(); Add-Content -LiteralPath $env:CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH -Value '[ERROR] Editor viewport smoke exceeded the 45 second process timeout.'; exit 124 }; $process.WaitForExit(); $exitCode = $process.ExitCode } catch { Add-Content -LiteralPath $env:CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH -Value ('[ERROR] Failed to launch editor viewport smoke: {0}' -f $_.Exception.Message); $exitCode = 125 }; if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath | Add-Content -LiteralPath $env:CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH; Remove-Item -LiteralPath $stderrPath -Force }; exit $exitCode"
set "CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE=%ERRORLEVEL%"
if exist "%CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH%" type "%CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH%"
if not "%CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE%"=="0" exit /b 1

call :validate_vulkan_validation_log "1"
set "CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE=%ERRORLEVEL%"
if not "%CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE%"=="0" exit /b 1

call :validate_editor_viewport_smoke
set "CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE=%ERRORLEVEL%"
exit /b %CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE%

:validate_editor_viewport_smoke
powershell -NoProfile -ExecutionPolicy Bypass -Command "$path = $env:CURRENT_EDITOR_VIEWPORT_SMOKE_PATH; if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Write-Host ('[ERROR] Editor viewport smoke artifact was not produced: {0}' -f $path); exit 1 }; try { $artifact = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } catch { Write-Host ('[ERROR] Editor viewport smoke artifact is not valid JSON: {0}' -f $_.Exception.Message); exit 1 }; if ([int]$artifact.schemaVersion -ne 2) { Write-Host ('[ERROR] Editor viewport smoke schema mismatch. Expected 2, received {0}.' -f $artifact.schemaVersion); exit 1 }; if ([string]$artifact.profile -cne $env:CURRENT_PROFILE) { Write-Host ('[ERROR] Editor viewport smoke profile mismatch. Expected {0}, received {1}.' -f $env:CURRENT_PROFILE, $artifact.profile); exit 1 }; if ($artifact.passed -ne $true -or $artifact.checks.passed -ne $true) { Write-Host ('[ERROR] Editor viewport smoke checks did not pass: {0}' -f $path); exit 1 }; if ($null -eq $artifact.sceneFirstFrame -or $null -eq $artifact.sceneResizedFrame -or $null -eq $artifact.gameFirstFrame) { Write-Host '[ERROR] Editor viewport smoke is missing a required SceneView or GameView observation.'; exit 1 }; if ($artifact.sceneFirstFrame.consumptionReported -ne $true -or $artifact.sceneResizedFrame.consumptionReported -ne $true -or $artifact.gameFirstFrame.consumptionReported -ne $true) { Write-Host '[ERROR] Editor viewport smoke did not report all presented frames as consumed.'; exit 1 }; if ([double]$artifact.sceneFirstFrame.presentationScaleY -ne -1.0 -or [double]$artifact.sceneResizedFrame.presentationScaleY -ne -1.0 -or [double]$artifact.gameFirstFrame.presentationScaleY -ne -1.0) { Write-Host '[ERROR] Editor viewport smoke compositor Y-flip policy is incorrect.'; exit 1 }; if ($null -eq $artifact.worldPartition -or [int]$artifact.worldPartition.cellCount -le 0 -or $artifact.worldPartition.loadRequested -ne $true -or $artifact.worldPartition.activeObserved -ne $true -or $artifact.worldPartition.unloadRequested -ne $true -or $artifact.worldPartition.unloadedObserved -ne $true) { Write-Host '[ERROR] Editor viewport smoke did not prove first-open world visibility and explicit cell load/unload.'; exit 1 }; Write-Host ('[Arisen] Editor viewport smoke passed: Scene={0}x{1}, Resized={2}x{3}, Game={4}x{5}, WorldCells={6}, output={7}' -f $artifact.sceneFirstFrame.width, $artifact.sceneFirstFrame.height, $artifact.sceneResizedFrame.width, $artifact.sceneResizedFrame.height, $artifact.gameFirstFrame.width, $artifact.gameFirstFrame.height, $artifact.worldPartition.cellCount, $path)"
exit /b %ERRORLEVEL%

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
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_ROOT%write_runtime_profile_result.ps1"
exit /b %ERRORLEVEL%

:write_summary
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_ROOT%write_runtime_validation_summary.ps1"
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
