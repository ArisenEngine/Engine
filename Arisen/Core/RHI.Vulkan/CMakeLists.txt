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
    ${CMAKE_SOURCE_DIR}/Core/Core.Infra
    ${CMAKE_SOURCE_DIR}/Core/Core.Debugger
    ${CMAKE_SOURCE_DIR}/Core/Core.Platform
    )  

target_compile_definitions(RHI.Vulkan PRIVATE RHIVULKAN_EXPORTS)

target_link_directories(RHI.Vulkan PUBLIC ${CMAKE_SOURCE_DIR}/3rdparty/vulkan/Lib)

target_link_libraries(RHI.Vulkan PUBLIC Core.Infra Core.Debugger Core.Platform vulkan-1)

set_target_properties(RHI.Vulkan PROPERTIES FOLDER "RHI")