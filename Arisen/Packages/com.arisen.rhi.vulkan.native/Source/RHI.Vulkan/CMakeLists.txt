cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(RHI.Vulkan SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(RHI.Vulkan PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)


target_include_directories(RHI.Vulkan PRIVATE
    ${CMAKE_SOURCE_DIR}/3rdparty/vulkan/Include
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.Foundation
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.Diagnostic
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.HAL
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.RHI
    )  

target_compile_definitions(RHI.Vulkan PRIVATE RHIVULKAN_EXPORTS)

target_link_directories(RHI.Vulkan PUBLIC 
    ${CMAKE_CURRENT_SOURCE_DIR}/../../3rdparty/vulkan/Lib
    $ENV{VULKAN_SDK}/Lib
)

target_link_libraries(RHI.Vulkan PUBLIC 
    Core.Foundation Core.Diagnostic Core.HAL Core.RHI
    vulkan-1 
    debug spirv-cross-cored optimized spirv-cross-core
    debug spirv-cross-glsld optimized spirv-cross-glsl
)

set_target_properties(RHI.Vulkan PROPERTIES FOLDER "RHI")

