@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM ArisenEngine - Add .csproj to generated solution and set outputs
REM ----------------------------------------------------------------------------
REM Usage:
REM   dotnet_add_csproj_engine_test.bat <solution_path> <outputs_dir>
REM
REM This script:
REM   1) Validates and resolves project paths
REM   2) Adds required .csproj files into the solution root using `dotnet sln add --in-root`
REM   3) Calls update_csproj_outputs.py to update OutputPath for the solution
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
REM Outputs dir and solution path
set "PROJ_OUTPUTS=%~2"
set "SLN_PATH=%~1"

if exist "%SLN_PATH%" (

    REM Resolve absolute paths to project files
    for %%I in ("!SCRIPT_DIR!\..\..\..\BindingGenerator\BindingGenerator.csproj") do set "BINDING_GENERATOR=%%~fI"
    for %%I in ("!SCRIPT_DIR!\..\..\..\AutoBinding\AutoBinding.csproj") do set "AUTO_BINDING=%%~fI"
    for %%I in ("!SCRIPT_DIR!\..\..\..\Serialization\Serialization\Serialization.csproj") do set "SERIALIZATION=%%~fI"
    for %%I in ("!SCRIPT_DIR!\..\..\..\Engine\ArisenEngine\ArisenEngine.csproj") do set "ARISEN_ENGINE=%%~fI"
    for %%I in ("!SCRIPT_DIR!\..\..\..\Test\CSharpEngineTest\CSharpEngineTest.csproj") do set "ARISEN_ENGINE_TEST=%%~fI"

    echo SLN_PATH: !SLN_PATH!
    echo BINDING_GENERATOR: !BINDING_GENERATOR!
    echo AUTO_BINDING: !AUTO_BINDING!
    echo SERIALIZATION: !SERIALIZATION!
    echo ARISEN_ENGINE: !ARISEN_ENGINE!
    echo ARISEN_ENGINE_TEST: !ARISEN_ENGINE_TEST!

    REM Sanity checks on resolved paths
    if "!BINDING_GENERATOR!"=="" (
        echo ERROR: BINDING_GENERATOR path is empty.
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if "!AUTO_BINDING!"=="" (
        echo ERROR: AUTO_BINDING path is empty.
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if "!SERIALIZATION!"=="" (
        echo ERROR: SERIALIZATION path is empty.
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if "!ARISEN_ENGINE!"=="" (
        echo ERROR: ARISEN_ENGINE path is empty.
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if "!ARISEN_ENGINE_TEST!"=="" (
        echo ERROR: ARISEN_ENGINE_TEST path is empty.
        set "EXIT_CODE=1"
        goto :cleanup
    )

    REM Existence checks for projects
    if not exist "!BINDING_GENERATOR!" (
        echo ERROR: Missing project ^(BindingGenerator^) at !SCRIPT_DIR!\..\..\..\BindingGenerator\BindingGenerator.csproj
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if not exist "!AUTO_BINDING!" (
        echo ERROR: Missing project ^(AutoBinding^) at !SCRIPT_DIR!\..\..\..\AutoBinding\AutoBinding.csproj
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if not exist "!SERIALIZATION!" (
        echo ERROR: Missing project ^(Serialization^) at !SCRIPT_DIR!\..\..\..\Serialization\Serialization\Serialization.csproj
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if not exist "!ARISEN_ENGINE!" (
        echo ERROR: Missing project ^(ArisenEngine^) at !SCRIPT_DIR!\..\..\..\Engine\ArisenEngine\ArisenEngine.csproj
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if not exist "!ARISEN_ENGINE_TEST!" (
        echo ERROR: Missing project ^(CSharpEngineTest^) at !SCRIPT_DIR!\..\..\..\Test\CSharpEngineTest\CSharpEngineTest.csproj
        set "EXIT_CODE=1"
        goto :cleanup
    )

    REM Extract solution dir and file
    for %%I in ("!SLN_PATH!") do (
        set "SLN_DIR=%%~dpI"
        set "SLN_FILE=%%~nxI"
    )

    echo Changing directory to solution: !SLN_DIR!
    pushd "!SLN_DIR!"

    REM Use relative paths so the solution stays portable
    set "REL_BINDING_GENERATOR=..\..\..\BindingGenerator\BindingGenerator.csproj"
    set "REL_AUTO_BINDING=..\..\..\AutoBinding\AutoBinding.csproj"
    set "REL_SERIALIZATION=..\..\..\Serialization\Serialization\Serialization.csproj"
    set "REL_ARISEN_ENGINE=..\..\..\Engine\ArisenEngine\ArisenEngine.csproj"
    set "REL_ARISEN_ENGINE_TEST=..\..\..\Test\CSharpEngineTest\CSharpEngineTest.csproj"

    echo Adding !REL_BINDING_GENERATOR! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_BINDING_GENERATOR!"

    echo Adding !REL_AUTO_BINDING! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_AUTO_BINDING!"

    echo Adding !REL_SERIALIZATION! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_SERIALIZATION!"

    echo Adding !REL_ARISEN_ENGINE! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_ENGINE!"

    echo Adding !REL_ARISEN_ENGINE_TEST! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_ENGINE_TEST!"

    popd
) else (
    echo Solution file not found: %SLN_PATH%
    set "EXIT_CODE=1"
)

:cleanup
if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
endlocal
exit /b %EXIT_CODE%
