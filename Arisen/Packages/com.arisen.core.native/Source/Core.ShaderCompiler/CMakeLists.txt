cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(DXC_ROOT ${CMAKE_SOURCE_DIR}/3rdparty/dxc)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.ShaderCompiler SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.ShaderCompiler PRIVATE
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.Foundation
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.Diagnostic
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.HAL
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.RHI
    )


target_include_directories(Core.ShaderCompiler PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/ShaderCompiler
)

target_compile_definitions(Core.ShaderCompiler PRIVATE CORE_SHADERCOMPILER_EXPORTS)

target_link_libraries(Core.ShaderCompiler
    PRIVATE 
    Core.Diagnostic
    Core.Foundation
    Core.HAL
    Core.RHI
)

# setup dxc
if (CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    add_compile_options(-Wno-attributes)
endif()
setup_dxc_for_target(Core.ShaderCompiler "${CMAKE_SOURCE_DIR}/3rdparty/dxc")

set_target_properties(Core.ShaderCompiler PROPERTIES FOLDER "Core")

