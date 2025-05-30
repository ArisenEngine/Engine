@echo off
setlocal ENABLEDELAYEDEXPANSION

REM Resolve script dir and root dir
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "ROOT_DIR=%SCRIPT_DIR%\..\.."

echo ROOT_DIR: %ROOT_DIR%

REM Clean and recreate build
echo Cleaning build directory...
if exist "%ROOT_DIR%\build" (
    rmdir /s /q "%ROOT_DIR%\build" || (
        echo ERROR: Failed to remove build directory.
        pause
        exit /b 1
    )
)
mkdir "%ROOT_DIR%\build"

REM Clean and recreate Projects
echo Cleaning Projects directory...
if exist "%ROOT_DIR%\Projects" (
    rmdir /s /q "%ROOT_DIR%\Projects" || (
        echo ERROR: Failed to remove Projects directory.
        pause
        exit /b 1
    )
)
mkdir "%ROOT_DIR%\Projects"

echo Done.
endlocal
