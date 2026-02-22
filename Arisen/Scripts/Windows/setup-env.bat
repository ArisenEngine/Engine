@echo off

REM === Save original code page ===
for /f "tokens=2 delims=:" %%I in ('chcp') do set "ORIGINAL_CP=%%I"
if defined ORIGINAL_CP set "ORIGINAL_CP=%ORIGINAL_CP: =%"

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

REM 取得脚本目录，去掉末尾反斜杠（如果有）
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

REM 尝试多种可能的 vswhere 路径
set "VSWHERE_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE_PATH%" (
    set "VSWHERE_PATH=%ProgramW6432%\Microsoft Visual Studio\Installer\vswhere.exe"
)
if not exist "%VSWHERE_PATH%" (
    echo ERROR: Cannot find vswhere.exe. Please install Visual Studio Installer.
    exit /b 1
)

REM 用 vswhere 查找带 MSVC 的最新 VS 安装路径
set "VSINSTALLDIR_ACTUAL="
for /f "usebackq tokens=*" %%i in (`"%VSWHERE_PATH%" -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
    set "VSINSTALLDIR_ACTUAL=%%i"
)
if not defined VSINSTALLDIR_ACTUAL (
    echo ERROR: Cannot find Visual Studio with C++ components installed.
    exit /b 1
)

echo VC Path %VSINSTALLDIR_ACTUAL%

REM 运行 vcvarsall.bat 设置编译环境
REM 我们在这里临时 unset 多种可能导致冲突的变量，因为 VsDevCmd.bat 可能会因为残留的环境变量报错
if not defined VCToolsInstallDir (
    set "VSCMD_ARG_no_logo=1"
    set "TEMP_PLATFORM=%PLATFORM%"
    set "TEMP_TARGET=%TARGET%"
    
    REM 清理可能导致 VsDevCmd.bat 报错的残留变量
    set "PLATFORM="
    set "TARGET="
    set "INCLUDE="
    set "LIB="
    set "LIBPATH="
    set "VisualStudioVersion="
    set "VSINSTALLDIR="
    set "VCINSTALLDIR="
    set "VCToolsVersion="
    set "WindowsSdkDir="
    set "WindowsSDKVersion="
    set "UCRTVersion="
    set "UniversalCRTSdkDir="
    set "NETFXSDKDir="
    set "FrameworkDir="
    set "FrameworkVersion="
    
    if exist "%VSINSTALLDIR_ACTUAL%\VC\Auxiliary\Build\vcvarsall.bat" (
        call "%VSINSTALLDIR_ACTUAL%\VC\Auxiliary\Build\vcvarsall.bat" x64
    ) else (
        echo ERROR: vcvarsall.bat not found at %VSINSTALLDIR_ACTUAL%
        exit /b 1
    )
    
    set "PLATFORM=%TEMP_PLATFORM%"
    set "TARGET=%TEMP_TARGET%"
    set "TEMP_PLATFORM="
    set "TEMP_TARGET="

    if errorlevel 1 (
        echo WARNING: MSVC environment initialization returned non-zero code.
    ) else (
        echo MSVC environment initialized successfully.
    )
)

REM ==== 动态添加 rc.exe 所在目录到 PATH ====
set "RC_PATH="
for /f "usebackq tokens=*" %%R in (`where rc`) do (
    set "RC_PATH=%%R"
    goto :FoundRC
)
echo ERROR: rc.exe not found in PATH or Windows SDK not installed.
exit /b 1

:FoundRC
echo Found rc.exe at: %RC_PATH%

REM ==== 查找最新版本的 link.exe ====
set "LINKER_PATH="
set "MSVC_TOOLS_DIR=%VSINSTALLDIR_ACTUAL%\VC\Tools\MSVC"

if exist "%MSVC_TOOLS_DIR%" (
    setlocal enabledelayedexpansion
    set "LATEST_VER="

    for /f "delims=" %%V in ('dir /b /ad "%MSVC_TOOLS_DIR%" 2^>nul') do (
        set "VER=%%V"
        if not defined LATEST_VER (
            set "LATEST_VER=!VER!"
        ) else (
            if "!VER!" gtr "!LATEST_VER!" (
                set "LATEST_VER=!VER!"
            )
        )
    )

    if defined LATEST_VER (
        set "LINK_CANDIDATE=%MSVC_TOOLS_DIR%\!LATEST_VER!\bin\Hostx64\x64\link.exe"
        if exist "!LINK_CANDIDATE!" (
            for /f "delims=" %%L in ("!LINK_CANDIDATE!") do endlocal & set "LINKER_PATH=%%L"
        ) else (
            endlocal
        )
    ) else (
        endlocal
    )
)

REM ==== 7-Zip 检查 ====
set "SEVEN_ZIP_PATH=%SCRIPT_DIR%\tools\7zip\7z.exe"
if not exist "%SEVEN_ZIP_PATH%" (
    echo 7z.exe not found, running setup-7z.ps1...
    powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\setup-7z.ps1"
    if errorlevel 1 (
        echo ERROR: Failed to setup 7-Zip.
        exit /b 1
    )
)

REM ==== LLVM 环境准备 ====
echo Starting LLVM setup...
powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\setup-llvm.ps1"
if errorlevel 1 (
    echo ERROR: Failed to setup LLVM environment.
    exit /b 1
)

REM ==== 设置环境变量 ====
set "LLVM_DIR=%SCRIPT_DIR%\tools\llvm"
set "CLANG_CL=%LLVM_DIR%\clang+llvm-20.1.5-x86_64-pc-windows-msvc\bin\clang-cl.exe"
if exist "%CLANG_CL%" (
    echo Using clang-cl from: %CLANG_CL%
    set "COMPILER_PATH=%CLANG_CL%"
) else (
    echo Using clang-cl from system PATH
    set "COMPILER_PATH=clang-cl"
)

REM ==== Ninja 检查与安装 ====
powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\setup-ninja.ps1"
if errorlevel 1 (
    echo ERROR: Failed to setup Ninja.
    exit /b 1
)

REM ==== 设置 NINJA ====
set "NINJA_EXE=%SCRIPT_DIR%\tools\ninja\ninja.exe"
if not exist "%NINJA_EXE%" (
    echo ERROR: ninja.exe not found after install!
    exit /b 1
)

set "MT_PATH=%LLVM_DIR%\clang+llvm-20.1.5-x86_64-pc-windows-msvc\bin\llvm-mt.exe"

REM ==== 导出变量定义到 env-vars.bat ====
echo set "COMPILER_PATH=%COMPILER_PATH%" > "%SCRIPT_DIR%\env-vars.bat"
echo set "LINKER_PATH=%LINKER_PATH%" >> "%SCRIPT_DIR%\env-vars.bat"
echo set "CMAKE_MAKE_PROGRAM=%NINJA_EXE%" >> "%SCRIPT_DIR%\env-vars.bat"
echo set "MT_PATH=%MT_PATH%" >> "%SCRIPT_DIR%\env-vars.bat"
echo set "CMAKE_RC_COMPILER=%RC_PATH%" >> "%SCRIPT_DIR%\env-vars.bat"

REM === Restore original code page or set to UTF-8 at the very end ===
if defined ARISEN_CODEPAGE (
    chcp %ARISEN_CODEPAGE% >nul
) else (
    chcp 65001 >nul
)

if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
exit /b 0

