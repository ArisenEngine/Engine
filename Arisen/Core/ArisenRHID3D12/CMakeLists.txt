# 设置 CMake 最低版本
cmake_minimum_required(VERSION 3.15)

# 设置项目名称
project(ArisenRHID3D12)

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
    ${PROJECT_SOURCE_DIR}/include   # ArisenRHID3D12 本地 include 目录
    ${PROJECT_SOURCE_DIR}/src      # ArisenRHID3D12 本地 src 目录
    ${PROJECT_SOURCE_DIR}/interface # ArisenRHID3D12 本地 interface 目录

    # 依赖的模块（Core.Debugger、Core.Infra、Core.Platform）
    ${PROJECT_SOURCE_DIR}/../Core.Debugger
    ${PROJECT_SOURCE_DIR}/../Core.Infra
    ${PROJECT_SOURCE_DIR}/../Core.Platform
    ${PROJECT_SOURCE_DIR}/../ArisenRHI
)

# 查找并链接依赖的库（Core.Debugger、Core.Infra、Core.Platform）
# 假设这些项目都使用了 CMake 编译，并且是作为子模块构建的
add_subdirectory(../Core.Debugger)
add_subdirectory(../Core.Infra)
add_subdirectory(../Core.Platform)
add_subdirectory(../ArisenRHI)

# 创建 ArisenRHID3D12 的静态库或共享库
add_library(ArisenRHID3D12 STATIC
    ${SRC_FILES}  # 源文件
    ${INCLUDE_FILES}  # 包含头文件（如果需要）
)

# 目标链接到 Core.Debugger, Core.Infra, Core.Platform
target_link_libraries(ArisenRHID3D12
    PUBLIC
    Core.Debugger
    Core.Infra
    Core.Platform
    Core.ArisenRHI
)
target_link_libraries(DXTest PRIVATE
    d3d12
    dxgi
    d3dcompiler)

# 打印一下编译信息，帮助调试
message(STATUS "ArisenRHID3D12 CMake setup complete!")
