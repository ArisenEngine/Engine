@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM ArisenEngine - Add EditorTest to solution and set outputs
REM ----------------------------------------------------------------------------
REM Usage:
REM   dotnet_add_csproj_editor_test.bat <solution_path> <outputs_dir>
REM ============================================================================

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
    for %%I in ("%SCRIPT_DIR%\..\..\..\BindingGenerator\BindingGenerator.csproj") do set "BINDING_GENERATOR=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\..\AutoBinding\AutoBinding.csproj") do set "AUTO_BINDING=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\..\Serialization\Serialization\Serialization.csproj") do set "SERIALIZATION=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\..\Engine\ArisenEngine.csproj") do set "ARISEN_ENGINE=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\..\Editor\ArisenEditor\ArisenEditor.csproj") do set "ARISEN_EDITOR=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\..\External\ArisenEditorFramework\ArisenEditorFramework.csproj") do set "ARISEN_EDITOR_FRAMEWORK=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\..\Test\EditorTest\EditorTest.csproj") do set "EDITOR_TEST=%%~fI"

    for %%V in (BINDING_GENERATOR AUTO_BINDING SERIALIZATION ARISEN_ENGINE ARISEN_EDITOR ARISEN_EDITOR_FRAMEWORK EDITOR_TEST) do (
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

    for %%I in ("%SLN_PATH%") do (
        set "SLN_DIR=%%~dpI"
        set "SLN_FILE=%%~nxI"
    )
    echo Changing directory to solution: !SLN_DIR!
    pushd "!SLN_DIR!"

    REM Relative paths for portability
    set "REL_BINDING_GENERATOR=..\..\..\BindingGenerator\BindingGenerator.csproj"
    set "REL_AUTO_BINDING=..\..\..\AutoBinding\AutoBinding.csproj"
    set "REL_SERIALIZATION=..\..\..\Serialization\Serialization\Serialization.csproj"
    set "REL_ARISEN_ENGINE=..\..\..\Engine\ArisenEngine.csproj"
    set "REL_ARISEN_EDITOR=..\..\..\Editor\ArisenEditor\ArisenEditor.csproj"
    set "REL_ARISEN_EDITOR_FRAMEWORK=..\..\..\External\ArisenEditorFramework\ArisenEditorFramework.csproj"
    set "REL_EDITOR_TEST=..\..\..\Test\EditorTest\EditorTest.csproj"

    dotnet sln "!SLN_FILE!" add --in-root "!REL_BINDING_GENERATOR!"
    dotnet sln "!SLN_FILE!" add --in-root "!REL_AUTO_BINDING!"
    dotnet sln "!SLN_FILE!" add --in-root "!REL_SERIALIZATION!"
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_ENGINE!"
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_EDITOR!"
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_EDITOR_FRAMEWORK!"
    dotnet sln "!SLN_FILE!" add --in-root "!REL_EDITOR_TEST!"

    popd
    echo Updating OutputPath for all projects in solution...
    python "!SCRIPT_DIR!\..\update_csproj_outputs.py" "!SLN_PATH!" "!PROJ_OUTPUTS!"
) else (
    echo Solution file not found: %SLN_PATH%
    set "EXIT_CODE=1"
)

:cleanup
if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
endlocal
exit /b %EXIT_CODE%
