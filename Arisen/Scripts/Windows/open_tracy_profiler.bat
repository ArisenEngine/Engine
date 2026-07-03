@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "CONFIG=Release"
set "ARISEN_NO_PAUSE="
set "CLEAN_BUILD="
for %%I in ("%~f0") do (
    set "SCRIPT_ROOT=%%~dpI"
    set "SCRIPT_NAME=%%~nxI"
)

:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
    shift
    goto :parse_args
)
if /i "%~1"=="--clean" (
    set "CLEAN_BUILD=1"
    shift
    goto :parse_args
)
if /i "%~1"=="--config" (
    if "%~2"=="" goto :usage
    set "CONFIG=%~2"
    shift
    shift
    goto :parse_args
)
if /i "%~1"=="--help" goto :usage
echo [ERROR] Unknown argument: %~1
goto :usage

:args_done
if defined CI set "ARISEN_NO_PAUSE=1"

set "TRACY_ROOT=!SCRIPT_ROOT!..\..\Development\PackageGame\Local\com.arisen.core.native\3rdparty\tracy"
set "BUILD_DIR=!SCRIPT_ROOT!..\..\Projects\TracyProfiler"
for %%I in ("!SCRIPT_ROOT!..\..") do set "ENGINE_ROOT=%%~fI"
for %%I in ("!ENGINE_ROOT!\..") do set "REPO_ROOT=%%~fI"
for %%I in ("!TRACY_ROOT!") do set "TRACY_ROOT=%%~fI"
for %%I in ("!BUILD_DIR!") do set "BUILD_DIR=%%~fI"

if not defined ENGINE_ROOT (
    echo [ERROR] Failed to resolve Arisen engine root from script path: !SCRIPT_ROOT!!SCRIPT_NAME!
    set "EXIT_CODE=1"
    goto :finish_no_pop
)

set "TRACY_PROFILER_SOURCE=!TRACY_ROOT!\profiler"
set "LOG_FILE=!BUILD_DIR!\open_tracy_profiler.log"
set "EXIT_CODE=0"

if not exist "!TRACY_PROFILER_SOURCE!\CMakeLists.txt" (
    echo [ERROR] Tracy profiler source was not found:
    echo         !TRACY_PROFILER_SOURCE!
    echo [ERROR] Script root: !SCRIPT_ROOT!
    set "EXIT_CODE=1"
    goto :finish_no_pop
)

where cmake >nul 2>&1
if errorlevel 1 (
    echo [ERROR] cmake was not found in PATH.
    set "EXIT_CODE=1"
    goto :finish_no_pop
)

if defined CLEAN_BUILD (
    echo [Arisen] Cleaning Tracy profiler build directory...
    if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
)

if not exist "!BUILD_DIR!" mkdir "!BUILD_DIR!"
echo === Arisen Tracy Profiler Build Log === > "!LOG_FILE!"

call :ensure_python
if errorlevel 1 goto :fail

echo [Arisen] Building bundled Tracy profiler.
echo [Arisen] Tracy source: !TRACY_ROOT!
echo [Arisen] Build dir:    !BUILD_DIR!
echo [Arisen] Config:       !CONFIG!

call :configure
if errorlevel 1 goto :fail

call :run cmake --build "%BUILD_DIR%" --config "%CONFIG%" --target tracy-profiler --parallel
if errorlevel 1 goto :fail

call :find_profiler_exe
if errorlevel 1 goto :fail

echo [Arisen] Launching Tracy profiler:
echo [Arisen] %TRACY_PROFILER_EXE%
start "" "%TRACY_PROFILER_EXE%"
if errorlevel 1 goto :fail

echo [Arisen] Tracy profiler launched.
goto :finish

:configure
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] vswhere.exe not found. Is Visual Studio 2022 installed?
    exit /b 1
)

for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
    set "VS_PATH=%%I"
)

if not defined VS_PATH (
    echo [ERROR] Visual Studio with MSBuild was not found.
    exit /b 1
)

if not defined VSCMD_ARG_TGT_ARCH (
    echo [Arisen] Initializing vcvars64 environment...
    call "%VS_PATH%\VC\Auxiliary\Build\vcvars64.bat" >nul
    if errorlevel 1 exit /b 1
)

call :run cmake -S "%TRACY_PROFILER_SOURCE%" -B "%BUILD_DIR%" -G "Visual Studio 17 2022" -A x64 -DNO_ISA_EXTENSIONS=ON
exit /b %ERRORLEVEL%

:ensure_python
set "REAL_PYTHON="
for /f "delims=" %%P in ('where python 2^>nul') do (
    if not defined REAL_PYTHON (
        echo %%P | findstr /i "\\WindowsApps\\" >nul
        if errorlevel 1 set "REAL_PYTHON=%%P"
    )
)

if not defined REAL_PYTHON (
    for /f "delims=" %%P in ('py -3 -c "import sys; print(sys.executable)" 2^>nul') do (
        if not defined REAL_PYTHON set "REAL_PYTHON=%%P"
    )
)

if not defined REAL_PYTHON (
    echo [ERROR] Python 3 was not found. Install Python or make py -3 available.
    exit /b 1
)

set "PYTHON_SHIM_DIR=!BUILD_DIR!\tools"
if not exist "!PYTHON_SHIM_DIR!" mkdir "!PYTHON_SHIM_DIR!"
> "!PYTHON_SHIM_DIR!\python3.cmd" echo @echo off
>> "!PYTHON_SHIM_DIR!\python3.cmd" echo "!REAL_PYTHON!" %%*
> "!PYTHON_SHIM_DIR!\python.cmd" echo @echo off
>> "!PYTHON_SHIM_DIR!\python.cmd" echo "!REAL_PYTHON!" %%*
set "PATH=!PYTHON_SHIM_DIR!;!PATH!"
echo [Arisen] Python: !REAL_PYTHON!
exit /b 0

:find_profiler_exe
set "TRACY_PROFILER_EXE=%BUILD_DIR%\%CONFIG%\tracy-profiler.exe"
if exist "%TRACY_PROFILER_EXE%" exit /b 0

for /f "usebackq delims=" %%F in (`powershell -NoProfile -Command "Get-ChildItem -LiteralPath '%BUILD_DIR%' -Recurse -Filter 'tracy-profiler.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName"`) do (
    set "TRACY_PROFILER_EXE=%%F"
)

if exist "%TRACY_PROFILER_EXE%" exit /b 0

echo [ERROR] Built Tracy profiler executable was not found.
echo [ERROR] Expected near: %BUILD_DIR%\%CONFIG%\tracy-profiler.exe
exit /b 1

:run
echo.
echo [Arisen] Running: %*
>> "%LOG_FILE%" echo [RUN] %*
%* >> "%LOG_FILE%" 2>&1
set "_RUN_EXIT=%ERRORLEVEL%"
if not "%_RUN_EXIT%"=="0" (
    echo [ERROR] Command failed with exit code %_RUN_EXIT%.
    echo [ERROR] Showing last 120 log lines from: %LOG_FILE%
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 120"
)
exit /b %_RUN_EXIT%

:fail
set "EXIT_CODE=1"
echo.
echo [Arisen] Tracy profiler build/open failed.
goto :finish

:finish
echo.
if "%EXIT_CODE%"=="0" (
    echo [Arisen] RESULT: SUCCESS
) else (
    echo [Arisen] RESULT: FAILED
    echo [Arisen] Log: %LOG_FILE%
)

:finish_no_pop
if not defined ARISEN_NO_PAUSE (
    echo.
    echo Press any key to close this window...
    pause >nul
)

exit /b %EXIT_CODE%

:usage
echo Usage: !SCRIPT_NAME! [--config Debug^|Release] [--clean] [--no-pause]
echo.
echo Builds Arisen's bundled Tracy profiler viewer and launches it.
exit /b 1
