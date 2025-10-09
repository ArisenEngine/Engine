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

REM Clean and recreate build directory (kill lockers first, retry delete)
call :kill_build_processes
call :retry_rmdir "%ROOT_DIR%\build" 5
if not exist "%ROOT_DIR%\build" mkdir "%ROOT_DIR%\build" || (
    echo ERROR: Failed to create build directory.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM Clean and recreate Projects
echo Cleaning Projects directory...
call :kill_build_processes
call :retry_rmdir "%ROOT_DIR%\Projects" 6
if not exist "%ROOT_DIR%\Projects" (
    mkdir "%ROOT_DIR%\Projects" || (
        echo ERROR: Failed to create Projects directory.
        set "EXIT_CODE=1"
        goto :cleanup
    )
) else (
    echo WARNING: Projects directory still exists; some files may be locked. Proceeding.
)

echo Done.

:cleanup
if defined ORIGINAL_CP chcp !ORIGINAL_CP! >nul
if not defined ARISEN_NO_PAUSE pause
endlocal
exit /b %EXIT_CODE%

REM ==================== Helpers ====================
:kill_build_processes
REM Try to terminate common build/IDE processes that may lock files
for %%P in (devenv.exe MSBuild.exe MSBuildNode.exe VBCSCompiler.exe mspdbsrv.exe cmake.exe ninja.exe dotnet.exe cl.exe link.exe csc.exe) do (
    tasklist /FI "IMAGENAME eq %%P" | find /I "%%P" >nul 2>&1 && (
        echo Terminating %%P ...
        taskkill /F /T /IM %%P >nul 2>&1
    )
)
REM Give the system a moment to release handles
ping -n 2 127.0.0.1 >nul
exit /b 0

:retry_rmdir
REM usage: call :retry_rmdir "dirPath" [retries]
set "__DIR__=%~1"
set "__TRIES__=%~2"
if "%__TRIES__%"=="" set "__TRIES__=5"
set /a __COUNT__=0
:__rmdir_loop
if not exist "%__DIR__%" goto :__rmdir_done
rmdir /s /q "%__DIR__%" >nul 2>&1
if exist "%__DIR__%" (
    set /a __COUNT__+=1
    if %__COUNT__% GEQ %__TRIES__% (
        echo WARNING: Could not remove "%__DIR__%" after %__TRIES__% attempts.
        goto :__rmdir_done
    )
    call :kill_build_processes
    echo Retry removing "%__DIR__%" (attempt %__COUNT__%/%__TRIES__%) ...
    ping -n 2 127.0.0.1 >nul
    goto :__rmdir_loop
)
:__rmdir_done
exit /b 0
