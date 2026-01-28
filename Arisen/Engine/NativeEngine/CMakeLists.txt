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

target_include_directories(NativeEngine PRIVATE
    ${CMAKE_SOURCE_DIR}/Tools/Profiler
    ${CMAKE_SOURCE_DIR}/Core/Core.Foundation
    ${CMAKE_SOURCE_DIR}/Core/Core.Diagnostic
    ${CMAKE_SOURCE_DIR}/Core/Core.HAL
    ${CMAKE_SOURCE_DIR}/Core/Core.RHI
    ${CMAKE_SOURCE_DIR}/3rdparty/vulkan/Include
    )  


target_compile_definitions(NativeEngine PRIVATE NATIVEENGINE_EXPORTS)

target_link_libraries(NativeEngine PRIVATE Profiler Core.Foundation Core.Diagnostic Core.HAL Core.RHI RHI.DX12 RHI.Vulkan)

set_target_properties(NativeEngine PROPERTIES FOLDER "Runtime")
