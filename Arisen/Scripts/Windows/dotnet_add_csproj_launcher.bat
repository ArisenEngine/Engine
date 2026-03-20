@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM ArisenEngine - Add .csproj to launcher solution and set outputs
REM ----------------------------------------------------------------------------
REM Usage:
REM   dotnet_add_csproj_launcher.bat <solution_path> <outputs_dir>
REM ============================================================================

REM === Ensure console code page matches localized tool output ===
for /f "tokens=2 delims=:" %%I in ('chcp') do set "ORIGINAL_CP=%%I"
set "ORIGINAL_CP=%ORIGINAL_CP: =%"
if defined ARISEN_CODEPAGE (
    chcp %ARISEN_CODEPAGE% >nul
) else (
    chcp 936 >nul
)

set "EXIT_CODE=0"

if "%~1"=="" (
    echo Missing solution path.
    set "EXIT_CODE=1"
    goto :cleanup
)
if "%~2"=="" (
    echo Missing output path.
    set "EXIT_CODE=1"
    goto :cleanup
)

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "PROJ_OUTPUTS=%~2"
set "SLN_PATH=%~1"

if exist "%SLN_PATH%" (
    REM Resolve absolute project paths
    for %%I in ("%SCRIPT_DIR%\..\..\ArisenKernel\ArisenKernel.csproj") do set "ARISEN_KERNEL=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\ArisenHost\ArisenHost.csproj") do set "ARISEN_HOST=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\Editor\ArisenLauncher\ArisenLauncher.csproj") do set "ARISEN_LAUNCHER=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\Editor\ArisenLauncher.Desktop\ArisenLauncher.Desktop.csproj") do set "ARISEN_LAUNCHER_DESKTOP=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\External\ArisenBuildTool\ArisenBuildTool.csproj") do set "ARISEN_BUILD_TOOL=%%~fI"

    echo SLN_PATH: !SLN_PATH!
    echo ARISEN_KERNEL: !ARISEN_KERNEL!
    echo ARISEN_HOST: !ARISEN_HOST!
    echo ARISEN_LAUNCHER: !ARISEN_LAUNCHER!
    echo ARISEN_LAUNCHER_DESKTOP: !ARISEN_LAUNCHER_DESKTOP!
    echo ARISEN_BUILD_TOOL: !ARISEN_BUILD_TOOL!

    REM Basic existence checks
    for %%V in (ARISEN_KERNEL ARISEN_HOST ARISEN_LAUNCHER ARISEN_LAUNCHER_DESKTOP ARISEN_BUILD_TOOL) do (
        if "!%%V!"=="" (
            echo ERROR: Path for %%V is empty.
            set "EXIT_CODE=1"
            goto :cleanup
        )
        if not exist "!%%V!" (
            echo ERROR: Missing project for %%V at !%%V!
            set "EXIT_CODE=1"
            goto :cleanup
        )
    )

    REM Enter solution dir
    for %%I in ("%SLN_PATH%") do (
        set "SLN_DIR=%%~dpI"
        set "SLN_FILE=%%~nxI"
    )
    echo Changing directory to solution: !SLN_DIR!
    pushd "!SLN_DIR!"

    REM Relative paths for portability
    set "REL_ARISEN_KERNEL=..\..\..\ArisenKernel\ArisenKernel.csproj"
    set "REL_ARISEN_HOST=..\..\..\ArisenHost\ArisenHost.csproj"
    set "REL_ARISEN_LAUNCHER=..\..\..\Editor\ArisenLauncher\ArisenLauncher.csproj"
    set "REL_ARISEN_LAUNCHER_DESKTOP=..\..\..\Editor\ArisenLauncher.Desktop\ArisenLauncher.Desktop.csproj"
    set "REL_ARISEN_BUILD_TOOL=..\..\..\External\ArisenBuildTool\ArisenBuildTool.csproj"

    echo Adding !REL_ARISEN_KERNEL! to !SLN_FILE! (^--in-root^)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_KERNEL!"
    if errorlevel 1 ( set "EXIT_CODE=1" & goto :cleanup )

    echo Adding !REL_ARISEN_HOST! to !SLN_FILE! (^--in-root^)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_HOST!"
    if errorlevel 1 ( set "EXIT_CODE=1" & goto :cleanup )

    echo Adding !REL_ARISEN_LAUNCHER! to !SLN_FILE! (^--in-root^)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_LAUNCHER!"
    if errorlevel 1 ( set "EXIT_CODE=1" & goto :cleanup )

    echo Adding !REL_ARISEN_LAUNCHER_DESKTOP! to !SLN_FILE! (^--in-root^)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_LAUNCHER_DESKTOP!"
    if errorlevel 1 ( set "EXIT_CODE=1" & goto :cleanup )

    echo Adding !REL_ARISEN_BUILD_TOOL! to !SLN_FILE! (^--in-root^)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_BUILD_TOOL!"
    if errorlevel 1 ( set "EXIT_CODE=1" & goto :cleanup )

    echo Updating OutputPath for all projects in solution...
    python "!SCRIPT_DIR!\update_csproj_outputs.py" "!SLN_PATH!" "!PROJ_OUTPUTS!"

    popd
) else (
    echo Solution file not found: %SLN_PATH%
    set "EXIT_CODE=1"
)

:cleanup
if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
endlocal
exit /b %EXIT_CODE%
