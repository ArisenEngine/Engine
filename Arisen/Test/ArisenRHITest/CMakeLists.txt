# 设置 CMake 最低版本
cmake_minimum_required(VERSION 3.15)

# 设置项目名称
project(ArisenRHITest)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 设置源文件
collect_sources(ARISENRHITEST_SOURCES ${CMAKE_CURRENT_SOURCE_DIR}/src)

# 可执行目标
add_executable(ArisenRHITest ${ARISENRHITEST_SOURCES})

target_include_directories(ArisenRHITest PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_include_directories(ArisenRHITest PRIVATE
    ${CMAKE_SOURCE_DIR}/Core/ArisenRHI
    ${CMAKE_SOURCE_DIR}/Core/ArisenRHID3D12
    ${CMAKE_SOURCE_DIR}/Core/Core.Infra
    ${CMAKE_SOURCE_DIR}/Core/Core.Debugger
    ${CMAKE_SOURCE_DIR}/Core/Core.Platform
    )  

# 链接依赖的库
target_link_libraries(ArisenRHITest
    PRIVATE
    Core.Infra
    Core.Debugger
    Core.Platform
    ArisenRHI
    ArisenRHID3D12
)

# 设置为 Windows GUI 子系统
set_target_properties(DXTest PROPERTIES
    WIN32_EXECUTABLE TRUE
)

# 打印 CMake 配置完毕信息
message(STATUS "ArisenRHITest CMake setup complete!")
