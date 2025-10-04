@echo off
setlocal ENABLEDELAYEDEXPANSION

REM === Ensure console code page matches localized tool output ===
for /f "tokens=2 delims=:" %%I in ('chcp') do set "ORIGINAL_CP=%%I"
set "ORIGINAL_CP=!ORIGINAL_CP: =!"
if defined ARISEN_CODEPAGE (
    chcp %ARISEN_CODEPAGE% >nul
) else (
    chcp 65001 >nul
)

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

set "EXIT_CODE=0"

REM Resolve script dir and root dir
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "ROOT_DIR=%SCRIPT_DIR%\..\.."

echo ROOT_DIR: %ROOT_DIR%

REM Clean and recreate build
REM Clean and recreate build directory
if exist "%ROOT_DIR%\build" (
    rmdir /s /q "%ROOT_DIR%\build" || (
        echo ERROR: Failed to remove build directory.
        set "EXIT_CODE=1"
        goto :cleanup
    )
)
mkdir "%ROOT_DIR%\build" || (
    echo ERROR: Failed to create build directory.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM Clean and recreate Projects
echo Cleaning Projects directory...
if exist "%ROOT_DIR%\Projects" (
    rmdir /s /q "%ROOT_DIR%\Projects" || (
        echo ERROR: Failed to remove Projects directory.
        set "EXIT_CODE=1"
        goto :cleanup
    )
)
mkdir "%ROOT_DIR%\Projects" || (
    echo ERROR: Failed to create Projects directory.
    set "EXIT_CODE=1"
    goto :cleanup
)

echo Done.

:cleanup
if not "%EXIT_CODE%"=="0" pause
if defined ORIGINAL_CP chcp !ORIGINAL_CP! >nul
endlocal
exit /b %EXIT_CODE%
