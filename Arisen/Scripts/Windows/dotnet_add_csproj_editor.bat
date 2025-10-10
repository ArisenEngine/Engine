@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM ArisenEngine - Add .csproj to editor solution and set outputs
REM ----------------------------------------------------------------------------
REM Usage:
REM   dotnet_add_csproj_editor.bat <solution_path> <outputs_dir>
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
    for %%I in ("%SCRIPT_DIR%\..\..\BindingGenerator\BindingGenerator.csproj") do set "BINDING_GENERATOR=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\AutoBinding\AutoBinding.csproj") do set "AUTO_BINDING=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\Serialization\Serialization\Serialization.csproj") do set "SERIALIZATION=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\Engine\ArisenEngine\ArisenEngine.csproj") do set "ARISEN_ENGINE=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\Editor\ArisenEditor\ArisenEditor.csproj") do set "ARISEN_EDITOR=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\Editor\ArisenEditorShell\ArisenEditorShell.csproj") do set "ARISEN_EDITOR_SHELL=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\Editor\ArisenEditor.Desktop\ArisenEditor.Desktop.csproj") do set "ARISEN_EDITOR_DESKTOP=%%~fI"
    REM Avalonia Dock set - correct paths
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.ProportionalStackPanel\Avalonia.Controls.ProportionalStackPanel.csproj") do set "AVA_ProportionalStack=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.Recycling\Avalonia.Controls.Recycling.csproj") do set "AVA_Recycling=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.Recycling.Model\Avalonia.Controls.Recycling.Model.csproj") do set "AVA_RecyclingModel=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.MarkupExtension\Avalonia.MarkupExtension.csproj") do set "AVA_MarkupExt=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Avalonia\Dock.Avalonia.csproj") do set "AVA_DockAvalonia=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model\Dock.Model.csproj") do set "AVA_DockModel=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model.Avalonia\Dock.Model.Avalonia.csproj") do set "AVA_DockModelAvalonia=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model.Mvvm\Dock.Model.Mvvm.csproj") do set "AVA_DockModelMvvm=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model.ReactiveUI\Dock.Model.ReactiveUI.csproj") do set "AVA_DockModelReactiveUI=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Serializer\Dock.Serializer.csproj") do set "AVA_DockSerializer=%%~fI"
    for %%I in ("%SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Settings\Dock.Settings.csproj") do set "AVA_DockSettings=%%~fI"

    echo SLN_PATH: !SLN_PATH!
    echo BINDING_GENERATOR: !BINDING_GENERATOR!
    echo AUTO_BINDING: !AUTO_BINDING!
    echo SERIALIZATION: !SERIALIZATION!
    echo ARISEN_ENGINE: !ARISEN_ENGINE!
    echo ARISEN_EDITOR: !ARISEN_EDITOR!
    echo ARISEN_EDITOR_SHELL: !ARISEN_EDITOR_SHELL!
    echo ARISEN_EDITOR_DESKTOP: !ARISEN_EDITOR_DESKTOP!

    REM Basic existence checks
    for %%V in (BINDING_GENERATOR AUTO_BINDING SERIALIZATION ARISEN_ENGINE ARISEN_EDITOR ARISEN_EDITOR_SHELL ARISEN_EDITOR_DESKTOP) do (
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
    set "REL_BINDING_GENERATOR=..\..\..\BindingGenerator\BindingGenerator.csproj"
    set "REL_AUTO_BINDING=..\..\..\AutoBinding\AutoBinding.csproj"
    set "REL_SERIALIZATION=..\..\..\Serialization\Serialization\Serialization.csproj"
    set "REL_ARISEN_ENGINE=..\..\..\Engine\ArisenEngine\ArisenEngine.csproj"
    set "REL_ARISEN_EDITOR=..\..\..\Editor\ArisenEditor\ArisenEditor.csproj"
    set "REL_ARISEN_EDITOR_SHELL=..\..\..\Editor\ArisenEditorShell\ArisenEditorShell.csproj"
    set "REL_ARISEN_EDITOR_DESKTOP=..\..\..\Editor\ArisenEditor.Desktop\ArisenEditor.Desktop.csproj"

    echo Adding !REL_BINDING_GENERATOR! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_BINDING_GENERATOR!"

    echo Adding !REL_AUTO_BINDING! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_AUTO_BINDING!"

    echo Adding !REL_SERIALIZATION! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_SERIALIZATION!"

    echo Adding !REL_ARISEN_ENGINE! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_ENGINE!"

    echo Adding !REL_ARISEN_EDITOR! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_EDITOR!"

    echo Adding !REL_ARISEN_EDITOR_SHELL! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_EDITOR_SHELL!"

    echo Adding !REL_ARISEN_EDITOR_DESKTOP! to !SLN_FILE! (--in-root)
    dotnet sln "!SLN_FILE!" add --in-root "!REL_ARISEN_EDITOR_DESKTOP!"

    REM Avalonia Dock projects are optional; add if exist
    for %%P in ("%AVA_ProportionalStack%" "%AVA_Recycling%" "%AVA_RecyclingModel%" "%AVA_MarkupExt%" "%AVA_DockAvalonia%" "%AVA_DockModel%" "%AVA_DockModelAvalonia%" "%AVA_DockModelMvvm%" "%AVA_DockModelReactiveUI%" "%AVA_DockSerializer%" "%AVA_DockSettings%") do (
        if exist "%%~fP" (
            for %%Q in ("%%~fP") do (
                set "PROJ_NAME=%%~nQ"
                set "REL_AVA=..\..\..\3rdparty\Ava.Dock\src\!PROJ_NAME!\!PROJ_NAME!.csproj"
            )
            echo Adding !REL_AVA! to !SLN_FILE! (--in-root)
            dotnet sln "!SLN_FILE!" add --in-root "!REL_AVA!"
        )
    )

    popd
) else (
    echo Solution file not found: %SLN_PATH%
    set "EXIT_CODE=1"
)

:cleanup
if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
endlocal
exit /b %EXIT_CODE%
