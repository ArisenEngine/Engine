@echo off
setlocal

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

set SCRIPT_DIR=%~dp0
set PROJ_OUTPUTS=%~2
set SLN_PATH=%~1

if exist "%SLN_PATH%" (
    echo Adding .csproj to %SLN_PATH%...
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\BindingGenerator\BindingGenerator.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\AutoBinding\AutoBinding.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\Serialization\Serialization\Serialization.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\Engine\ArisenEngine\ArisenEngine.csproj
    
    @REM ==== avalonia dock
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.ProportionalStackPanel\Avalonia.Controls.ProportionalStackPanel.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.Recycling\Avalonia.Controls.Recycling.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.Recycling.Model\Avalonia.Controls.Recycling.Model.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Avalonia.MarkupExtension\Avalonia.MarkupExtension.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Avalonia\Dock.Avalonia.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model\Dock.Model.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model.Avalonia\Dock.Model.Avalonia.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model.Mvvm\Dock.Model.Mvvm.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Model.ReactiveUI\Dock.Model.ReactiveUI.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Serializer\Dock.Serializer.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\3rdparty\Ava.Dock\src\Dock.Settings\Dock.Settings.csproj
    @REM ==== end of ava dock
    
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\Editor\ArisenEditor\ArisenEditor.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\Editor\ArisenEditorShell\ArisenEditorShell.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\Editor\ArisenEditor.Desktop\ArisenEditor.Desktop.csproj
    echo Setting solution...
    python "%SCRIPT_DIR%/update_csproj_outputs.py" "%SLN_PATH%" "%PROJ_OUTPUTS%"
) else (
    echo Solution file not found: %SLN_PATH%
    set "EXIT_CODE=1"
)

:cleanup
if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
endlocal
exit /b %EXIT_CODE%
