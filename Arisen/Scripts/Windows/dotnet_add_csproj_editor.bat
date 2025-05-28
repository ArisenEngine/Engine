@echo off
if "%~1"=="" (
    echo Missing solution path.
    goto :usage
)
if "%~2"=="" (
    echo Missing output path.
    goto :usage
)
goto :main

:usage
echo Usage: add_csproj_to_sln.bat path\to\solution.sln path\to\outputs
exit /b 1

:main

set PROJ_OUTPUTS=%~2
set SLN_PATH=%~1

if exist "%SLN_PATH%" (
    echo Adding .csproj to %SLN_PATH%...
    dotnet sln "%SLN_PATH%" add ..\..\BindingGenerator\BindingGenerator.csproj
    dotnet sln "%SLN_PATH%" add ..\..\AutoBinding\AutoBinding.csproj
    dotnet sln "%SLN_PATH%" add ..\..\Serialization\Serialization\Serialization.csproj
    dotnet sln "%SLN_PATH%" add ..\..\Engine\ArisenEngine\ArisenEngine.csproj
    
    @REM ==== avalonia dock
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.ProportionalStackPanel\Avalonia.Controls.ProportionalStackPanel.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.Recycling\Avalonia.Controls.Recycling.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Avalonia.Controls.Recycling.Model\Avalonia.Controls.Recycling.Model.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Avalonia.MarkupExtension\Avalonia.MarkupExtension.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Dock.Avalonia\Dock.Avalonia.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Dock.Model\Dock.Model.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Dock.Model.Avalonia\Dock.Model.Avalonia.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Dock.Model.Mvvm\Dock.Model.Mvvm.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Dock.Model.ReactiveUI\Dock.Model.ReactiveUI.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Dock.Serializer\Dock.Serializer.csproj
    dotnet sln "%SLN_PATH%" add ..\..\3rdparty\Ava.Dock\src\Dock.Settings\Dock.Settings.csproj
    @REM ==== end of ava dock
    
    dotnet sln "%SLN_PATH%" add ..\..\Editor\ArisenEditor\ArisenEditor.csproj
    dotnet sln "%SLN_PATH%" add ..\..\Editor\ArisenEditorShell\ArisenEditorShell.csproj
    dotnet sln "%SLN_PATH%" add ..\..\Editor\ArisenEditor.Desktop\ArisenEditor.Desktop.csproj
    echo Setting solution...
    set SCRIPT_DIR=%~dp0
    python "%SCRIPT_DIR%/update_csproj_outputs.py" "%SLN_PATH%" "%PROJ_OUTPUTS%"
) else (
    echo Solution file not found: %SLN_PATH%
)
