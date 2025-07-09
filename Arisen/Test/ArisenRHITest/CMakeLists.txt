# 设置 CMake 最低版本
cmake_minimum_required(VERSION 3.15)

# 项目名称
project(ArisenRHITest)

# 设置 C++ 标准
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# MSVC UTF-8 编码输出
add_compile_options("$<$<CXX_COMPILER_ID:MSVC>:/utf-8>")

# 统一输出目录
set(ARISEN_OUTPUT_DIR ${CMAKE_BINARY_DIR}/bin)
foreach(OUTPUT_CONFIG IN ITEMS RUNTIME LIBRARY ARCHIVE)
    set(CMAKE_${OUTPUT_CONFIG}_OUTPUT_DIRECTORY ${ARISEN_OUTPUT_DIR})
    foreach(OUTPUT_TYPE IN ITEMS DEBUG RELEASE RELWITHDEBINFO MINSIZEREL)
        set(CMAKE_${OUTPUT_CONFIG}_OUTPUT_DIRECTORY_${OUTPUT_TYPE} ${ARISEN_OUTPUT_DIR})
    endforeach()
endforeach()

# 模块路径（自定义函数）
list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/../../cmake")
include(Utils OPTIONAL)

# 添加依赖子模块（并设定 binary dir）
add_subdirectory(${CMAKE_SOURCE_DIR}/../../Core/Core.Infra        ${CMAKE_BINARY_DIR}/Core/Core.Infra)
add_subdirectory(${CMAKE_SOURCE_DIR}/../../Core/Core.Debugger     ${CMAKE_BINARY_DIR}/Core/Core.Debugger)
add_subdirectory(${CMAKE_SOURCE_DIR}/../../Core/Core.Platform     ${CMAKE_BINARY_DIR}/Core/Core.Platform)
add_subdirectory(${CMAKE_SOURCE_DIR}/../../Core/ArisenRHI         ${CMAKE_BINARY_DIR}/Core/ArisenRHI)
add_subdirectory(${CMAKE_SOURCE_DIR}/../../Core/ArisenRHID3D12    ${CMAKE_BINARY_DIR}/Core/ArisenRHID3D12)

# Tracy 与 spdlog
set(TRACY_STATIC OFF CACHE BOOL "" FORCE)
add_subdirectory(${CMAKE_SOURCE_DIR}/../../3rdparty/tracy         ${CMAKE_BINARY_DIR}/3rdparty/tracy)
set_target_properties(TracyClient PROPERTIES FOLDER "3rdparty")

add_subdirectory(${CMAKE_SOURCE_DIR}/../../Tools/Profiler         ${CMAKE_BINARY_DIR}/Tools/Profiler)
add_subdirectory(${CMAKE_SOURCE_DIR}/../../3rdparty/spdlog        ${CMAKE_BINARY_DIR}/3rdparty/spdlog)
set_target_properties(spdlog PROPERTIES FOLDER "3rdparty")

# 收集源文件
if(COMMAND collect_sources)
    collect_sources(ARISENRHITEST_SOURCES ${CMAKE_CURRENT_SOURCE_DIR})
else()
    file(GLOB_RECURSE ARISENRHITEST_SOURCES CONFIGURE_DEPENDS
        ${CMAKE_CURRENT_SOURCE_DIR}/*.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/*.h
        ${CMAKE_CURRENT_SOURCE_DIR}/*.hpp
    )
endif()

# 可执行程序
add_executable(ArisenRHITest ${ARISENRHITEST_SOURCES})

# 设置为 GUI 子系统
set_target_properties(ArisenRHITest PROPERTIES WIN32_EXECUTABLE TRUE)

# 包含路径（本地）
target_include_directories(ArisenRHITest
    PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
)

# 链接依赖库
target_link_libraries(ArisenRHITest
    PRIVATE
    Core.Infra
    Core.Debugger
    Core.Platform
    ArisenRHI
    ArisenRHID3D12
    Profiler
    spdlog
)

# 打印配置完成信息
message(STATUS "ArisenRHITest CMake setup complete! Output to ${ARISEN_OUTPUT_DIR}")
