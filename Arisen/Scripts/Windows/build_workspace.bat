@echo off
setlocal EnableDelayedExpansion
set "SCRIPT_ROOT=%~dp0"

:: Defaults
set "MANIFEST_PATH="
set "BUILD_CONFIG=Debug"
set "TARGET_PROFILE="
set "TEST_PACKAGE_ID="
set "RUN_TESTS=0"
set "ARISEN_NO_PAUSE="
if defined CI set "ARISEN_NO_PAUSE=1"

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="-m" (
    set "MANIFEST_PATH=%~2"
    shift
) else if /i "%~1"=="--manifest" (
    set "MANIFEST_PATH=%~2"
    shift
) else if /i "%~1"=="-b" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="--binding-config" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="-c" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="--config" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="-p" (
    set "TARGET_PROFILE=%~2"
    shift
) else if /i "%~1"=="--profile" (
    set "TARGET_PROFILE=%~2"
    shift
) else if /i "%~1"=="-t" (
    set "TEST_PACKAGE_ID=%~2"
    shift
) else if /i "%~1"=="--package" (
    set "TEST_PACKAGE_ID=%~2"
    shift
) else if /i "%~1"=="--run-tests" (
    set "RUN_TESTS=1"
) else if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
) else (
    if not defined MANIFEST_PATH (
        set "MANIFEST_PATH=%~1"
    )
)
shift
goto parse_args
:end_parse

if "!MANIFEST_PATH!"=="" (
    if exist "!SCRIPT_ROOT!..\..\Development\PackageGame\project.arisen" (
        set "MANIFEST_PATH=!SCRIPT_ROOT!..\..\Development\PackageGame\project.arisen"
    ) else (
        set "MANIFEST_PATH=!SCRIPT_ROOT!..\..\Development\PackageGame\manifest.json"
    )
)
for %%I in ("!MANIFEST_PATH!") do set "MANIFEST_PATH=%%~fI"

if /i "!BUILD_CONFIG!"=="Release" (
    set "BINDING_BAT=run_binding_generator_release.bat"
) else (
    set "BINDING_BAT=run_binding_generator_debug.bat"
)

set "WORKSPACE_DIR=!MANIFEST_PATH!\.."
for %%I in ("!WORKSPACE_DIR!") do set "WORKSPACE_DIR=%%~fI"
set "SLN_DIR=!WORKSPACE_DIR!\.arisen"
set "BUILD_TOOL_CSPROJ=!SCRIPT_ROOT!..\..\External\ArisenBuildTool\ArisenBuildTool.csproj"
set "BUILD_TOOL_DLL=!SCRIPT_ROOT!..\..\External\ArisenBuildTool\bin\x64\Release\net9.0\ArisenBuildTool.dll"
set "ENGINE_ROOT=!SCRIPT_ROOT!..\.."

echo [Arisen] Locating Developer Command Prompt...
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] vswhere.exe not found. Is Visual Studio installed?
    goto :fail
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
    set "VS_PATH=%%i"
)

if not defined VS_PATH (
    echo [ERROR] Visual Studio with MSBuild not found.
    goto :fail
)

if not defined VSCMD_ARG_TGT_ARCH (
    echo [Arisen] Initializing vcvars64 environment...
    call "%VS_PATH%\VC\Auxiliary\Build\vcvars64.bat" >nul
)

echo [Arisen] Refreshing managed bindings...
call "!SCRIPT_ROOT!!BINDING_BAT!" --no-pause
if !errorlevel! neq 0 (
    echo [ERROR] BindingGenerator failed to refresh interop code.
    goto :fail
)

echo [Arisen] Ensuring ArisenBuildTool is compiled...
dotnet build "%BUILD_TOOL_CSPROJ%" -c Release >nul
if !errorlevel! neq 0 (
    echo [ERROR] Failed to compile ArisenBuildTool.
    goto :fail
)
if not exist "!BUILD_TOOL_DLL!" (
    echo [ERROR] ArisenBuildTool output was not found: !BUILD_TOOL_DLL!
    goto :fail
)

:: Determine mode and profiles
if defined TEST_PACKAGE_ID (
    echo [Arisen] MODE: Isolated Package Test [!TEST_PACKAGE_ID!]
    set "PROFILES=Testing"
    set "MODE=IsolatedTest"
) else (
    set "MODE=ProfileLoop"
    if defined TARGET_PROFILE (
        echo [Arisen] MODE: Specific Profile [!TARGET_PROFILE!]
        set "PROFILES=!TARGET_PROFILE!"
    ) else (
        echo [Arisen] MODE: Workspace Profile Loop
        set "PROFILES="
        for /f "usebackq delims=" %%A in (`dotnet "!BUILD_TOOL_DLL!" manifest-info --manifest "!MANIFEST_PATH!" --field profiles`) do (
            set "PROFILES=!PROFILES! %%A"
        )
        if not defined PROFILES (
            echo [ERROR] Could not read profiles from workspace manifest: !MANIFEST_PATH!
            goto :fail
        )
    )
)

for %%A in (!PROFILES!) do (
    echo [Arisen] --------------------------------------------------
    echo [Arisen] Processing Profile: %%A [!BUILD_CONFIG!]
    echo [Arisen] --------------------------------------------------
    
    if /i "!MODE!"=="IsolatedTest" (
        dotnet run --project "%BUILD_TOOL_CSPROJ%" -- test --package "!TEST_PACKAGE_ID!" --workspace "!WORKSPACE_DIR!" --engine "!ENGINE_ROOT!"
        set "PROJECT_NAME=!TEST_PACKAGE_ID!.TestRun"
    ) else (
        dotnet run --project "%BUILD_TOOL_CSPROJ%" -- generate -m "%MANIFEST_PATH%" -e "!ENGINE_ROOT!" --profile %%A
        
        :: Read the source manifest through the same comment-aware parser used by generation.
        set "PROJECT_NAME="
        for /f "usebackq delims=" %%P in (`dotnet "!BUILD_TOOL_DLL!" manifest-info --manifest "!MANIFEST_PATH!" --field name`) do set "PROJECT_NAME=%%P"
    )
    
    if not defined PROJECT_NAME set "PROJECT_NAME=MyGame"
    set "CURRENT_SLN=!SLN_DIR!\!PROJECT_NAME!_%%A.sln"
    
    if not exist "!CURRENT_SLN!" (
        echo [ERROR] Solution file not found for profile %%A: !CURRENT_SLN!
        goto :fail
    )

    :: Cleanup running instances
    taskkill /F /IM "!PROJECT_NAME!.exe" /T 2>nul
    taskkill /F /IM "!PROJECT_NAME!.Desktop.exe" /T 2>nul
    taskkill /F /IM "ArisenLauncher.exe" /T 2>nul
    taskkill /F /IM "ArisenEditor.exe" /T 2>nul

    echo [Arisen] Restoring NuGet Packages for %%A...
    dotnet restore "!CURRENT_SLN!"
    if !errorlevel! neq 0 (
        echo [ERROR] NuGet restore failed for profile %%A.
        goto :fail
    )

    :: Manual Native Build
    set "NATIVE_BUILD_DIR=%SLN_DIR%\Projects\%%A\Native\build"
    if exist "!NATIVE_BUILD_DIR!" (
        echo [Arisen] Building Native Components for %%A [!BUILD_CONFIG!]...
        cmake --build "!NATIVE_BUILD_DIR!" --config !BUILD_CONFIG!
        if !errorlevel! neq 0 (
            echo [ERROR] Native build failed for profile %%A.
            goto :fail
        )
    )

    set "LOG_FILE=!SLN_DIR!\build_%%A.log"
    for %%F in ("!LOG_FILE!") do set "LOG_FILE_ABS=%%~fF"
    echo [Arisen] MSBuild Log: !LOG_FILE_ABS!
    msbuild "!CURRENT_SLN!" /p:Configuration=!BUILD_CONFIG! /p:Platform=x64 /m /fl "/flp:logfile=!LOG_FILE_ABS!;verbosity=normal"
    if !errorlevel! neq 0 (
        echo [ERROR] MSBuild failed on profile: %%A
        goto :fail
    )

    if /i "!MODE!"=="IsolatedTest" if "!RUN_TESTS!"=="1" (
        set "TEST_EXE=!WORKSPACE_DIR!\.arisen\bin\%%A\!BUILD_CONFIG!\!PROJECT_NAME!.exe"
        if not exist "!TEST_EXE!" (
            echo [ERROR] Test executable not found: !TEST_EXE!
            goto :fail
        )

        echo [Arisen] Running package tests: !TEST_EXE!
        pushd "!WORKSPACE_DIR!\.arisen\bin\%%A\!BUILD_CONFIG!"
        "!TEST_EXE!"
        set "TEST_EXIT=!errorlevel!"
        popd

        if !TEST_EXIT! neq 0 (
            echo [ERROR] Package tests failed with exit code !TEST_EXIT!.
            goto :fail
        )
    )
)

echo.
echo [Arisen] Build successful!
if not defined ARISEN_NO_PAUSE pause
exit /b 0

:fail
echo.
echo [ERROR] Build Pipeline aborted due to execution or compilation errors.
if not defined ARISEN_NO_PAUSE pause
exit /b 1
