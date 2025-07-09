cmake_minimum_required(VERSION 3.15)
project(ArisenRHID3D12)

# 设置 C++ 标准
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 查找源文件
file(GLOB_RECURSE ARISEN_RHI_D3D12_SOURCES CONFIGURE_DEPENDS
    ${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp
)

file(GLOB_RECURSE ARISEN_RHI_D3D12_HEADERS CONFIGURE_DEPENDS
    ${CMAKE_CURRENT_SOURCE_DIR}/include/*.h
    ${CMAKE_CURRENT_SOURCE_DIR}/include/*.hpp
)

# 创建库
add_library(ArisenRHID3D12 STATIC
    ${ARISEN_RHI_D3D12_SOURCES}
    ${ARISEN_RHI_D3D12_HEADERS} # optional, for IDE
)

# 添加头文件搜索路径（仅本模块）
target_include_directories(ArisenRHID3D12
    PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/interface
    PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# 禁用 RTTI
if(MSVC)
    target_compile_options(ArisenRHID3D12 PRIVATE /GR-)
else()
    target_compile_options(ArisenRHID3D12 PRIVATE -fno-rtti)
endif()

# 链接外部模块（由顶层 add_subdirectory 控制顺序）
target_link_libraries(ArisenRHID3D12
    PUBLIC
    Core.Debugger
    Core.Infra
    Core.Platform
    ArisenRHI
)

# 链接 D3D12 系统库（仅在 Windows/MSVC 有效）
target_link_libraries(ArisenRHID3D12
    PRIVATE
    d3d12
    dxgi
    d3dcompiler
)

# 输出状态
message(STATUS "ArisenRHID3D12 CMake setup complete!")
