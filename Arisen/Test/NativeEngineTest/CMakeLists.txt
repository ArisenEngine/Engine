cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

set(ASSETS_DIR "${CMAKE_SOURCE_DIR}/Test/NativeEngineTest/Assets")
set(SHADER_DIR "${CMAKE_SOURCE_DIR}/Test/NativeEngineTest/Shader")

get_property(OUTPUT_DIR GLOBAL PROPERTY GLOBAL_OUTPUT_DIR)
set(ASSETS_DEST "${OUTPUT_DIR}/Assets")
set(SHADER_DEST "${OUTPUT_DIR}/Shader")

add_custom_target(CopyAll
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${ASSETS_DIR}" "${ASSETS_DEST}"
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${SHADER_DIR}" "${SHADER_DEST}"
    COMMENT "Copying"
    USES_TERMINAL
)

add_executable(NativeEngineTest
    ${ALL_SOURCE_FILES}
)

add_dependencies(NativeEngineTest CopyAll)

target_include_directories(NativeEngineTest PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_include_directories(NativeEngineTest PRIVATE
    ${CMAKE_SOURCE_DIR}/3rdparty/image
    ${CMAKE_SOURCE_DIR}/3rdparty/vulkan/Include
    ${CMAKE_SOURCE_DIR}/Tools/Profiler
    ${CMAKE_SOURCE_DIR}/Core/Core.Foundation
    ${CMAKE_SOURCE_DIR}/Core/Core.Diagnostic
    ${CMAKE_SOURCE_DIR}/Core/Core.HAL
    ${CMAKE_SOURCE_DIR}/Core/Core.RHI
    ${CMAKE_SOURCE_DIR}/Core/Core.ShaderCompiler
    ${CMAKE_SOURCE_DIR}/Engine/NativeEngine
    )  

set_target_properties(NativeEngineTest PROPERTIES
    WIN32_EXECUTABLE TRUE
)

target_link_libraries(NativeEngineTest PRIVATE Profiler Core.Foundation Core.Diagnostic Core.HAL Core.RHI Core.ShaderCompiler RHI.Vulkan NativeEngine)
