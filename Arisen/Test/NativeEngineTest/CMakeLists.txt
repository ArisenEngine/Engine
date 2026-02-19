cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
# Collect code files
file(GLOB_RECURSE ALL_SOURCE_FILES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp" "${CMAKE_CURRENT_SOURCE_DIR}/*.h")

# Collect shader and asset files to include in SLN
file(GLOB_RECURSE SHADER_FILES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/Shader/*")
file(GLOB_RECURSE ASSET_FILES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/Assets/*")

# Prevent IDE from trying to compile shaders with default tools
set_source_files_properties(${SHADER_FILES} PROPERTIES HEADER_FILE_ONLY TRUE)
set_source_files_properties(${ASSET_FILES} PROPERTIES HEADER_FILE_ONLY TRUE)

# Organize files in IDE solution
source_group(TREE "${CMAKE_CURRENT_SOURCE_DIR}/Shader" PREFIX "Shader" FILES ${SHADER_FILES})
source_group(TREE "${CMAKE_CURRENT_SOURCE_DIR}/Assets" PREFIX "Assets" FILES ${ASSET_FILES})

set(ASSETS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/Assets")
set(SHADER_DIR "${CMAKE_CURRENT_SOURCE_DIR}/Shader")

get_property(OUTPUT_DIR GLOBAL PROPERTY GLOBAL_OUTPUT_DIR)
set(ASSETS_DEST "${OUTPUT_DIR}/Assets")
set(SHADER_DEST "${OUTPUT_DIR}/Shader")

add_executable(NativeEngineTest
    ${ALL_SOURCE_FILES}
    ${SHADER_FILES}
    ${ASSET_FILES}
)

# Use POST_BUILD to ensure assets are copied when building in IDEs like Rider
add_custom_command(TARGET NativeEngineTest POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${ASSETS_DIR}" "${ASSETS_DEST}"
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${SHADER_DIR}" "${SHADER_DEST}"
    COMMENT "Copying assets and shaders to output directory"
)

target_include_directories(NativeEngineTest PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_include_directories(NativeEngineTest PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/../../3rdparty/cgltf
    ${CMAKE_CURRENT_SOURCE_DIR}/../../3rdparty/image
    ${CMAKE_CURRENT_SOURCE_DIR}/../../3rdparty/vulkan/Include
    ${CMAKE_CURRENT_SOURCE_DIR}/../../Core/Core.Foundation
    ${CMAKE_CURRENT_SOURCE_DIR}/../../Core/Core.Diagnostic
    ${CMAKE_CURRENT_SOURCE_DIR}/../../Core/Core.HAL
    ${CMAKE_CURRENT_SOURCE_DIR}/../../Core/Core.RHI
    ${CMAKE_CURRENT_SOURCE_DIR}/../../Core/Core.ShaderCompiler
    ${CMAKE_CURRENT_SOURCE_DIR}/../../Core/RHI.Vulkan
    ${CMAKE_CURRENT_SOURCE_DIR}/../../3rdparty/glm
    )  

set_target_properties(NativeEngineTest PROPERTIES
    WIN32_EXECUTABLE TRUE
)

target_link_libraries(NativeEngineTest PRIVATE Core.Foundation Core.HAL Core.RHI Core.ShaderCompiler RHI.Vulkan)
