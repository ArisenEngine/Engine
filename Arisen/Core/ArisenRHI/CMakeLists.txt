# 设置 CMake 最低版本
cmake_minimum_required(VERSION 3.15)
set(CMAKE_CXX_STANDARD 23)

# 项目名称
project(ArisenRHI)

# 源文件和头文件
file(GLOB_RECURSE ARISENRHI_SOURCES CONFIGURE_DEPENDS
    ${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp
)

file(GLOB_RECURSE ARISENRHI_HEADERS CONFIGURE_DEPENDS
    ${CMAKE_CURRENT_SOURCE_DIR}/include/*.h
    ${CMAKE_CURRENT_SOURCE_DIR}/include/*.hpp
    ${CMAKE_CURRENT_SOURCE_DIR}/interface/*.hpp
    ${CMAKE_CURRENT_SOURCE_DIR}/interface/*.h
)

# 创建静态库
add_library(ArisenRHI STATIC
    ${ARISENRHI_SOURCES}
    ${ARISENRHI_HEADERS}  # optional, just for IDE organization
)

# 设置 C++ 标准
target_compile_features(ArisenRHI PUBLIC cxx_std_23)

# 禁用 RTTI
if(MSVC)
    target_compile_options(ArisenRHI PRIVATE /GR-)
else()
    target_compile_options(ArisenRHI PRIVATE -fno-rtti)
endif()

# 添加包含目录，仅限本模块
target_include_directories(ArisenRHI
    PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/interface
    PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# 链接依赖库（要求外部项目提供这些 target）
target_link_libraries(ArisenRHI
    PUBLIC
    Core.Debugger
    Core.Infra
    Core.Platform
)


# 日志输出
message(STATUS "ArisenRHI CMake setup complete!")
