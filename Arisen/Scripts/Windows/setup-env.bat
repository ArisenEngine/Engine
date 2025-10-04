@echo off
setlocal enabledelayedexpansion

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

REM 取得脚本目录，去掉末尾反斜杠（如果有）
set "SCRIPT_DIR=%~dp0"
if "!SCRIPT_DIR:~-1!"=="\" set "SCRIPT_DIR=!SCRIPT_DIR:~0,-1!"

REM 尝试多种可能的 vswhere 路径
set "VSWHERE_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "!VSWHERE_PATH!" (
    set "VSWHERE_PATH=%ProgramW6432%\Microsoft Visual Studio\Installer\vswhere.exe"
)
if not exist "!VSWHERE_PATH!" (
    echo ERROR: Cannot find vswhere.exe. Please install Visual Studio Installer.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM 用 vswhere 查找带 MSVC 的最新 VS 安装路径
set "VSINSTALLDIR="
for /f "usebackq tokens=*" %%i in (`"!VSWHERE_PATH!" -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
    set "VSINSTALLDIR=%%i"
)
if not defined VSINSTALLDIR (
    echo ERROR: Cannot find Visual Studio with C++ components installed.
    set "EXIT_CODE=1"
    goto :cleanup
)

echo VC Path !VSINSTALLDIR!

set "VSCMD_DEBUG=3"
echo VSCMD_DEBUG=!VSCMD_DEBUG!

REM 运行 vcvars64.bat 设置编译环境
call "!VSINSTALLDIR!\VC\Auxiliary\Build\vcvars64.bat"
set > "!SCRIPT_DIR!\vsdevcmd.env.log"
if errorlevel 1 (
    echo WARNING: vcvars64.bat returned non-zero code. Continuing anyway...
) else (
    echo MSVC environment initialized successfully.
)

REM ==== 动态添加 rc.exe 所在目录到 PATH ====
set "RC_PATH="
for /f "usebackq tokens=*" %%R in (`where rc`) do (
    set "RC_PATH=%%R"
    goto :FoundRC
)
echo ERROR: rc.exe not found in PATH or Windows SDK not installed.
set "EXIT_CODE=1"
goto :cleanup

:FoundRC
for %%D in ("!RC_PATH!") do set "RC_DIR=%%~dpD"
echo Found rc.exe at: !RC_PATH!

REM ==== 查找最新版本的 link.exe ====
set "LINKER_PATH="
set "MSVC_TOOLS_DIR=!VSINSTALLDIR!\VC\Tools\MSVC"

if exist "!MSVC_TOOLS_DIR!" (
    set "LATEST_VER="

    for /f "delims=" %%V in ('dir /b /ad "!MSVC_TOOLS_DIR!" 2^>nul') do (
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
        set "LINK_CANDIDATE=!MSVC_TOOLS_DIR!\!LATEST_VER!\bin\Hostx64\x64\link.exe"
        if exist "!LINK_CANDIDATE!" (
            set "LINKER_PATH=!LINK_CANDIDATE!"
        )
    )
)

REM ==== 7-Zip 检查 ====
set "SEVEN_ZIP_PATH=!SCRIPT_DIR!\tools\7zip\7z.exe"
if not exist "!SEVEN_ZIP_PATH!" (
    echo 7z.exe not found, running setup-7z.ps1...
    powershell -ExecutionPolicy Bypass -File "!SCRIPT_DIR!\setup-7z.ps1"
    if errorlevel 1 (
        echo ERROR: Failed to setup 7-Zip.
        set "EXIT_CODE=1"
        goto :cleanup
    )
) else (
    echo Found 7z.exe at !SEVEN_ZIP_PATH!
)

REM ==== LLVM 环境准备 ====
echo Starting LLVM setup...
powershell -ExecutionPolicy Bypass -File "!SCRIPT_DIR!\setup-llvm.ps1"
if errorlevel 1 (
    echo ERROR: Failed to setup LLVM environment.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 设置环境变量 ====
set "LLVM_DIR=!SCRIPT_DIR!\tools\llvm"
set "CLANG_CL=!LLVM_DIR!\clang+llvm-20.1.5-x86_64-pc-windows-msvc\bin\clang-cl.exe"
echo CLANG_CL: "!CLANG_CL!"
if exist "!CLANG_CL!" (
    echo Using clang-cl from: !CLANG_CL!
    set "COMPILER_PATH=!CLANG_CL!"
) else (
    echo Using clang-cl from system PATH
    set "COMPILER_PATH=clang-cl"
)

echo Compiler path set to: !COMPILER_PATH!

REM 你这里注释掉了llvm link，如果想用，去掉注释即可
REM set LINKER_PATH=!LLVM_DIR!\clang+llvm-20.1.5-x86_64-pc-windows-msvc\bin\lld-link.exe

REM === 直接使用fake-rc
REM set RC_PATH=!SCRIPT_DIR!/fake-rc.bat


if defined LINKER_PATH (
    echo Found link.exe at: !LINKER_PATH!
) else (
    echo ERROR: Failed to locate link.exe in latest MSVC version.
)

echo Using linker: !LINKER_PATH!

echo WindowsSdk: !WindowsSdkDir!
echo INCLUDE: !INCLUDE!
where rc.exe
where mt.exe

REM ==== Ninja 检查与安装 ====
powershell -ExecutionPolicy Bypass -File "!SCRIPT_DIR!\setup-ninja.ps1"
if errorlevel 1 (
    echo ERROR: Failed to setup Ninja.
    set "EXIT_CODE=1"
    goto :cleanup
) else (
    echo Ninja setup complete.
)

REM ==== 设置 NINJA ====
set "NINJA_EXE=!SCRIPT_DIR!\tools\ninja\ninja.exe"
if exist "!NINJA_EXE!" (
    echo Using ninja at !NINJA_EXE!
) else (
    echo ERROR: ninja.exe not found after install!
    set "EXIT_CODE=1"
    goto :cleanup
)

set "MT_PATH=!LLVM_DIR!\clang+llvm-20.1.5-x86_64-pc-windows-msvc\bin\llvm-mt.exe"

echo Current script dir: "!SCRIPT_DIR!"

REM ==== 在 endlocal 之前保存变量值到临时变量 ====
set "COMPILER_PATH_SAVE=!COMPILER_PATH!"
set "LINKER_PATH_SAVE=!LINKER_PATH!"
set "NINJA_EXE_SAVE=!NINJA_EXE!"
set "MT_PATH_SAVE=!MT_PATH!"
set "RC_PATH_SAVE=!RC_PATH!"

REM ==== 导出变量定义到 env-vars.bat ====
(
    echo set COMPILER_PATH=!COMPILER_PATH_SAVE!
    echo set LINKER_PATH=!LINKER_PATH_SAVE!
    echo set CMAKE_MAKE_PROGRAM=!NINJA_EXE_SAVE!
    echo set MT_PATH=!MT_PATH_SAVE!
    echo set CMAKE_RC_COMPILER=!RC_PATH_SAVE!
) > "!SCRIPT_DIR!\env-vars.bat"

:cleanup
if defined ORIGINAL_CP chcp !ORIGINAL_CP! >nul
endlocal
exit /b %EXIT_CODE%
