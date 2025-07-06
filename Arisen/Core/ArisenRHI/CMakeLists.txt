# 设置 CMake 最低版本
cmake_minimum_required(VERSION 3.15)

# 设置项目名称
project(ArisenRHI)

# 关闭 RTTI
if(MSVC)
    # 对于 MSVC 使用 /GR- 禁用 RTTI
    add_compile_options(/GR-)
else()
    # 对于 GCC/Clang 使用 -fno-rtti 禁用 RTTI
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fno-rtti")
endif()

# 设置模块源文件
file(GLOB_RECURSE SRC_FILES 
    "src/*.cpp"
)

file(GLOB_RECURSE INCLUDE_FILES
    "include/*.h"
)

# 包含接口目录
include_directories(
    ${PROJECT_SOURCE_DIR}/include   # ArisenRHI 本地 include 目录
    ${PROJECT_SOURCE_DIR}/src      # ArisenRHI 本地 src 目录
    ${PROJECT_SOURCE_DIR}/interface # ArisenRHI 本地 interface 目录

    # 依赖的模块（Core.Debugger、Core.Infra、Core.Platform）
    ${PROJECT_SOURCE_DIR}/../Core.Debugger
    ${PROJECT_SOURCE_DIR}/../Core.Infra
    ${PROJECT_SOURCE_DIR}/../Core.Platform
)

# 查找并链接依赖的库（Core.Debugger、Core.Infra、Core.Platform）
# 假设这些项目都使用了 CMake 编译，并且是作为子模块构建的
add_subdirectory(../Core.Debugger)
add_subdirectory(../Core.Infra)
add_subdirectory(../Core.Platform)

# 创建 ArisenRHI 的静态库或共享库
add_library(ArisenRHI STATIC
    ${SRC_FILES}  # 源文件
    ${INCLUDE_FILES}  # 包含头文件（如果需要）
)

# 目标链接到 Core.Debugger, Core.Infra, Core.Platform
target_link_libraries(ArisenRHI
    PUBLIC
    Core.Debugger
    Core.Infra
    Core.Platform
)

# 打印一下编译信息，帮助调试
message(STATUS "ArisenRHI CMake setup complete!")
