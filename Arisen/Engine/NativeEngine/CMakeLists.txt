cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(NativeEngine SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(NativeEngine PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(NativeEngine PRIVATE NATIVEENGINE_EXPORTS)

target_link_libraries(NativeEngine PRIVATE Core.Platform Core.Debugger RHI.DX12 RHI.Vulkan)