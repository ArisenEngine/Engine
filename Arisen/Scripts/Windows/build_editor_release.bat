@echo off
setlocal

set BUILD_CONFIG=Release

echo === Configuring (%BUILD_CONFIG%) ===
cmake -S ../../. -B ../../build -DTARGET=Editor -DPLATFORM=Windows -DCMAKE_BUILD_TYPE=%BUILD_CONFIG%
if %errorlevel% neq 0 (
    echo CMake configuration failed.
    exit /b %errorlevel%
)

echo === Building (%BUILD_CONFIG%) ===
cmake --build ../../build --config %BUILD_CONFIG%
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)

echo === Done (%BUILD_CONFIG%) ===
pause
