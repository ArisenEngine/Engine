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

set SCRIPT_DIR=%~dp0

set PROJ_OUTPUTS=%~2
set SLN_PATH=%~1

if exist "%SLN_PATH%" (
    echo Adding .csproj to %SLN_PATH%...
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\..\BindingGenerator\BindingGenerator.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\..\AutoBinding\AutoBinding.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\..\Serialization\Serialization\Serialization.csproj
    dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\..\Engine\ArisenEngine\ArisenEngine.csproj
     dotnet sln "%SLN_PATH%" add %SCRIPT_DIR%\..\..\..\Test\ArisenEngineTest\ArisenEngineTest.csproj
    
    echo Setting solution...
    python "%SCRIPT_DIR%\..\update_csproj_outputs.py" "%SLN_PATH%" "%PROJ_OUTPUTS%"
) else (
    echo Solution file not found: %SLN_PATH%
)
