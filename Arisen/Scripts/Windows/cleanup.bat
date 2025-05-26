:: 清理构建目录
if exist "!ROOT_DIR!\build" (
    echo Removing build directory...
    rmdir /s /q "!ROOT_DIR!\build"
)

if exist "!ROOT_DIR!\build" (
    echo ERROR: Failed to remove build directory.
    pause
    exit /b 1
)

mkdir "!ROOT_DIR!\build"

if exist "!ROOT_DIR!\Projects" (
    echo Removing build directory...
    rmdir /s /q "!ROOT_DIR!\Projects"
)

if exist "!ROOT_DIR!\Projects" (
    echo ERROR: Failed to remove build directory.
    pause
    exit /b 1
)

mkdir "!ROOT_DIR!\Projects"