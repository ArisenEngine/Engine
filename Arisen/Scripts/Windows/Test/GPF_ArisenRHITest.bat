@echo off
setlocal enabledelayedexpansion

REM 获取脚本目录
set SCRIPT_DIR=%~dp0

REM 使用 for 将相对路径转为绝对路径（去除 ..）
for %%I in ("%SCRIPT_DIR%\..\..\..") do set ROOT_DIR=%%~fI

set SRC_DIR=%ROOT_DIR%\Test\ArisenRHITest
set BUILD_DIR=%ROOT_DIR%\Projects\Visual Studio\ArisenRHITest

echo [INFO] Root directory:        %ROOT_DIR%
echo [INFO] Source directory:      %SRC_DIR%
echo [INFO] Build output directory:%BUILD_DIR%
echo:

REM 创建 build 目录
if not exist "%BUILD_DIR%" (
    echo [INFO] Creating build directory...
    mkdir "%BUILD_DIR%"
)

REM 进入 build 目录
pushd "%BUILD_DIR%"

REM 调用 CMake 生成工程文件（Visual Studio 2022）
cmake "%SRC_DIR%" -G "Visual Studio 17 2022"

REM 返回原目录
popd

echo:
echo [INFO] ArisenRHITest CMake generation completed!
pause
