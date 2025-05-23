@echo off
setlocal enabledelayedexpansion

REM ==== 7-Zip 检查 ====
set SEVEN_ZIP_PATH=%~dp0tools\7zip\7z.exe
if not exist "%SEVEN_ZIP_PATH%" (
    echo 7z.exe not found, running setup-7z.ps1...
    powershell -ExecutionPolicy Bypass -File "%~dp0setup-7z.ps1"
    if errorlevel 1 (
        echo ERROR: Failed to setup 7-Zip.
        exit /b 1
    )
) else (
    echo Found 7z.exe at %SEVEN_ZIP_PATH%
)

REM ==== LLVM 环境准备 ====
echo Starting LLVM setup...
powershell -ExecutionPolicy Bypass -File "%~dp0setup-llvm.ps1"
if errorlevel 1 (
    echo ERROR: Failed to setup LLVM environment.
    exit /b 1
)

REM ==== 设置环境变量 ====
set LLVM_DIR=%~dp0tools\llvm
set CLANG_CL=%LLVM_DIR%clang+llvm-20.1.5-x86_64-pc-windows-msvc\bin\clang-cl.exe

if exist "%CLANG_CL%" (
    echo Using clang-cl from: %CLANG_CL%
    set "COMPILER_PATH=%CLANG_CL%"
) else (
    echo Using clang-cl from system PATH
    set "COMPILER_PATH=clang-cl"
)

echo Compiler path set to: %COMPILER_PATH%

REM 传递环境变量给调用者
endlocal & set COMPILER_PATH=%COMPILER_PATH%
exit /b 0
