cmake_minimum_required(VERSION 3.29)

# ============================================================================
# ArisenEngine - Root CMake configuration
# ----------------------------------------------------------------------------
# Usage examples:
#   cmake -S Arisen -B Arisen/Projects/Visual\ Studio/<Target> -G "Visual Studio 17 2022" -A x64 \
#         -DTARGET=ArisenEngineTest -DPLATFORM=Windows
#
# Key concepts:
#   - TARGET controls which high-level target to configure (Editor, WindowsPlayerSupport, etc.)
#   - PLATFORM sets platform macros and optional .NET runtime selection
#   - PROJECT_DIR/OUTPUT_DIR unify IDE project and binary output locations
#   - Custom targets drive .NET builds and resource copy steps
# ============================================================================

# Parse TARGET and PLATFORM
if(NOT DEFINED TARGET)
    message(FATAL_ERROR "Please specify -DTARGET=[Editor|WindowsPlayerSupport|AndroidPlayerSupport|...].")
endif()

if(NOT DEFINED PLATFORM)
    message(FATAL_ERROR "Please specify -DPLATFORM=[Windows|Android|Linux|Apple].")
endif()

if(${TARGET} STREQUAL "CSharpEngineTest" OR ${TARGET} STREQUAL "NativeEngineTest")
project(${TARGET} LANGUAGES CXX)
else()
project("Arisen${TARGET}" LANGUAGES CXX)
endif()


list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake")
include(Utils)

string(TIMESTAMP START_TIME "%s")

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

option(ARISEN_GENERATE_PROJECT "Whether to generate IDE project files (sln/vcxproj/xcodeproj)" OFF)

add_definitions(-DUNICODE -D_UNICODE)
if(NOT MSVC)
    set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -Wl,--verbose")
endif()


# Set Macros
if(${TARGET} STREQUAL "Editor")
    set(TARGET_MACRO "ARISEN_EDITOR")
else()
    set(TARGET_MACRO "ARISEN_RUNTIME")
endif()
add_compile_definitions(${TARGET_MACRO}=1)
message(STATUS "Detected target macro: ${TARGET_MACRO}")

string(TOUPPER "${PLATFORM}" PLATFORM_UPPER)
if(PLATFORM_UPPER STREQUAL "WINDOWS")
    set(PLATFORM_MACRO "WINDOWS_PLATFORM")
    set(ENGINE_LIB_SUFFIX ".dll")
    set(DOTNET_RUNTIME "win-x64")
elseif(PLATFORM_UPPER STREQUAL "ANDROID")
    set(PLATFORM_MACRO "ANDROID_PLATFORM")
    set(ENGINE_LIB_SUFFIX ".so")
    # 没有正式支持�?Android RID，跳�?dotnet runtime 设置
    set(DOTNET_RUNTIME "")
elseif(PLATFORM_UPPER STREQUAL "APPLE")
    set(PLATFORM_MACRO "APPLE_PLATFORM")
    set(ENGINE_LIB_SUFFIX ".a")
    set(DOTNET_RUNTIME "osx-x64") # 如果�?ARM，可以用 osx-arm64
elseif(PLATFORM_UPPER STREQUAL "LINUX")
    set(PLATFORM_MACRO "LINUX_PLATFORM")
    set(ENGINE_LIB_SUFFIX ".so")
    set(DOTNET_RUNTIME "linux-x64")
else()
    set(PLATFORM_MACRO "UNKNOWN_PLATFORM")
    set(DOTNET_RUNTIME "")
endif()
add_compile_definitions(${PLATFORM_MACRO}=1)
message(STATUS "Detected platform macro: ${PLATFORM_MACRO}, dotnet runtime: ${DOTNET_RUNTIME}")

if(DOTNET_RUNTIME)
    set(DOTNET_RUNTIME_ARG "--runtime ${DOTNET_RUNTIME}")
else()
    set(DOTNET_RUNTIME_ARG "")
endif()


if (CMAKE_CXX_COMPILER_ID STREQUAL "MSVC")
    set(COMPILER_MACRO "MSVC_COMPILER")
elseif (CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
    if (CMAKE_CXX_COMPILER MATCHES "clang-cl")
        set(COMPILER_MACRO "CLANG_CL_COMPILER")
    else()
        set(COMPILER_MACRO "CLANG_COMPILER")
    endif()
else()
    set(COMPILER_MACRO "UNKNOWN_COMPILER")
endif()
add_compile_definitions(${COMPILER_MACRO}=1)
message(STATUS "C++ compiler: ${CMAKE_CXX_COMPILER}")

# Unified output path
string(TOLOWER "${CMAKE_GENERATOR}" _GEN_LOWER)
set(IS_MULTI_CONFIG FALSE)
if(CMAKE_CONFIGURATION_TYPES)
    set(IS_MULTI_CONFIG TRUE)
endif()

set(PROJECT_DIR "")
if(_GEN_LOWER MATCHES "visual studio")
    set(PROJECT_DIR "${CMAKE_SOURCE_DIR}/Projects/VisualStudio/${TARGET}")
elseif(_GEN_LOWER MATCHES "xcode")
    set(PROJECT_DIR "${CMAKE_SOURCE_DIR}/Projects/Xcode/$${TARGET}")
endif()
if(PROJECT_DIR)
    file(MAKE_DIRECTORY ${PROJECT_DIR})
    message(STATUS "Generating project files into: ${PROJECT_DIR}")
endif()

if(PROJECT_DIR)
    set(OUTPUT_DIR "${PROJECT_DIR}/Outputs/${CMAKE_CFG_INTDIR}")
else()
    set(OUTPUT_DIR "${CMAKE_BINARY_DIR}/outputs")
endif()
set_property(GLOBAL PROPERTY GLOBAL_OUTPUT_DIR ${OUTPUT_DIR})

if(IS_MULTI_CONFIG)
    foreach(OUTPUTCONFIG IN LISTS CMAKE_CONFIGURATION_TYPES)
        string(TOUPPER ${OUTPUTCONFIG} OUTPUTCONFIG_UPPER)
        set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} ${OUTPUT_DIR})
        set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} ${OUTPUT_DIR})
        set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} ${OUTPUT_DIR})
    endforeach()
else()
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR} CACHE PATH "Runtime output directory" FORCE)
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR} CACHE PATH "Library output directory" FORCE)
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_DIR} CACHE PATH "Archive output directory" FORCE)
endif()

message(STATUS "Unified binary output dir: ${OUTPUT_DIR}")

# Add C++ submodules and third-party libraries

# 配置 tracy 作为动态库
option(ARISEN_ENABLE_PROFILER "Enable Tracy Profiler" ON)

if(ARISEN_ENABLE_PROFILER)
    set(ARISEN_PROFILER_ENABLED ON)
    set(TRACY_ENABLE ON CACHE BOOL "" FORCE)
    set(TRACY_STATIC OFF CACHE BOOL "" FORCE)
    set(BUILD_SHARED_LIBS ON CACHE BOOL "" FORCE)
    add_subdirectory(3rdparty/tracy)
    set_target_properties(TracyClient PROPERTIES FOLDER "3rdparty")
    add_compile_definitions(ARISEN_PROFILER_ENABLED=1)
else()
    set(ARISEN_PROFILER_ENABLED OFF)
    add_compile_definitions(ARISEN_PROFILER_ENABLED=0)
endif()

add_subdirectory(Core/Core.Foundation)
add_subdirectory(3rdparty/spdlog)
set_target_properties(spdlog PROPERTIES FOLDER "3rdparty")
add_subdirectory(Core/Core.Diagnostic)
add_subdirectory(Core/Core.HAL)
add_subdirectory(Core/Core.RHI)
add_subdirectory(Core/Core.ShaderCompiler)
add_subdirectory(Core/RHI.DX12)
add_subdirectory(Core/RHI.Vulkan)


if("${TARGET}" STREQUAL "NativeEngineTest")
    add_subdirectory(Test/NativeEngineTest)
endif()

if("${TARGET}" STREQUAL "DXTest")
add_subdirectory(Test/DXTest)
endif()

if(NOT "${TARGET}" STREQUAL "NativeEngineTest" AND NOT "${TARGET}" STREQUAL "DXTest")

    # Setup .NET paths (BindingGenerator, AutoBinding, Engine)
set(BINDING_GENERATOR_DIR ${CMAKE_SOURCE_DIR}/BindingGenerator)
set(BINDING_GENERATOR_PROJ ${BINDING_GENERATOR_DIR}/BindingGenerator.csproj)
set(BINDING_GENERATOR_EXE ${OUTPUT_DIR}/Generator)

set(AUTO_BINDING_DIR ${CMAKE_SOURCE_DIR}/AutoBinding)
set(AUTO_BINDING_PROJ ${AUTO_BINDING_DIR}/AutoBinding.csproj)

set(ARISEN_ENGINE_PROJ ${CMAKE_SOURCE_DIR}/Engine/ArisenEngine/ArisenEngine.csproj)
set(ARISEN_ENGINE_PROJ_DIR ${CMAKE_SOURCE_DIR}/Engine/ArisenEngine/)

    # Build C# projects
add_custom_target(BuildBindingGenerator
    COMMAND ${CMAKE_COMMAND} -E env
        dotnet build "${BINDING_GENERATOR_PROJ}"
        --configuration $<CONFIG>
        --output "${BINDING_GENERATOR_EXE}"
    WORKING_DIRECTORY ${BINDING_GENERATOR_DIR}
    COMMENT "Building Binding Generator"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(BuildBindingGenerator Core.Foundation)
add_dependencies(BuildBindingGenerator Core.Diagnostic)
add_dependencies(BuildBindingGenerator Core.HAL)

set_target_properties(BuildBindingGenerator PROPERTIES FOLDER "CMakeProcess")

    # Generate auto binding
add_custom_target(GenerateAutoBinding
    COMMAND BindingGenerator.exe --source_code "${CMAKE_SOURCE_DIR}" --output "${CMAKE_SOURCE_DIR}" --library "${OUTPUT_DIR}"
    WORKING_DIRECTORY ${BINDING_GENERATOR_EXE}
    COMMENT "Generating Auto Binding Project"
    DEPENDS ${BINDING_GENERATOR_EXE}
    VERBATIM
    USES_TERMINAL
)
add_dependencies(GenerateAutoBinding BuildBindingGenerator)
set_target_properties(GenerateAutoBinding PROPERTIES FOLDER "CMakeProcess")

add_custom_target(BuildAutoBinding
    COMMAND dotnet build ${AUTO_BINDING_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}"
    WORKING_DIRECTORY ${AUTO_BINDING_DIR}
    COMMENT "Building AutoBinding Project"
    DEPENDS ${AUTO_BINDING_PROJ}
    VERBATIM
    USES_TERMINAL
)
add_dependencies(BuildAutoBinding GenerateAutoBinding)
set_target_properties(BuildAutoBinding PROPERTIES FOLDER "CMakeProcess")

add_custom_target(BuildArisenEngine
    COMMAND dotnet build ${ARISEN_ENGINE_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}"
    WORKING_DIRECTORY ${ARISEN_ENGINE_PROJ_DIR}
    COMMENT "Building Arisen Engine"
    DEPENDS "AutoBinding.dll"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(BuildArisenEngine BuildAutoBinding)
set_target_properties(BuildArisenEngine PROPERTIES FOLDER "CMakeProcess")

endif()

# Final target setup (Editor, Players, Tests)
if(${TARGET} STREQUAL "Editor")
    
    set(ARISEN_EDITOR_PROJ ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor/ArisenEditor.csproj)
    set(ARISEN_EDITOR_PROJ_DIR ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor/)
    set(ARISEN_EDITOR_SHELL_PROJ ${CMAKE_SOURCE_DIR}/Editor/ArisenEditorShell/ArisenEditorShell.csproj)
    set(ARISEN_EDITOR_SHELL_PROJ_DIR ${CMAKE_SOURCE_DIR}/Editor/ArisenEditorShell/)
    set(ARISEN_EDITOR_DESKTOP_PROJ ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor.Desktop/ArisenEditor.Desktop.csproj)
    set(ARISEN_EDITOR_DESKTOP_PROJ_DIR ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor.Desktop/)

    add_custom_target(BuildArisenEditor
    COMMAND dotnet build ${ARISEN_EDITOR_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}"
    WORKING_DIRECTORY ${ARISEN_EDITOR_PROJ_DIR}
    COMMENT "Building Arisen Editor"
    DEPENDS "ArisenEngine.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildArisenEditor BuildArisenEngine)
    set_target_properties(BuildArisenEditor PROPERTIES FOLDER "CMakeProcess")

    add_custom_target(BuildArisenEditorShell
    COMMAND dotnet build ${ARISEN_EDITOR_SHELL_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}"
    WORKING_DIRECTORY ${ARISEN_EDITOR_SHELL_PROJ_DIR}
    COMMENT "Building Arisen Editor Shell"
    DEPENDS "ArisenEditor.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildArisenEditorShell BuildArisenEditor)
    set_target_properties(BuildArisenEditorShell PROPERTIES FOLDER "CMakeProcess")


    add_custom_target(BuildArisenEditorDesktop ALL
    COMMAND dotnet build ${ARISEN_EDITOR_DESKTOP_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}"
    WORKING_DIRECTORY ${ARISEN_EDITOR_DESKTOP_PROJ_DIR}
    COMMENT "Building Arisen Editor Desktop"
    DEPENDS "ArisenEditorShell.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildArisenEditorDesktop BuildArisenEditorShell)
    set_target_properties(BuildArisenEditorDesktop PROPERTIES FOLDER "CMakeProcess")
    

    string(TIMESTAMP END_TIME "%s")
    math(EXPR ELAPSED_TIME "${END_TIME} - ${START_TIME}")
    message(STATUS "\\033[32m Configure success. Elapsed time: ${ELAPSED_TIME} seconds \\033[0m")
elseif(${TARGET} STREQUAL "WindowsPlayerSupport")
    # Add your platform player support setup here

elseif(${TARGET} STREQUAL "NativeEngineTest" OR ${TARGET} STREQUAL "DXTest")    

elseif(${TARGET} STREQUAL "CSharpEngineTest")    

    set(ARISEN_ENGINE_TEST_PROJ ${CMAKE_SOURCE_DIR}/Test/CSharpEngineTest/CSharpEngineTest.csproj)
    set(ARISEN_ENGINE_TEST_PROJ_DIR ${CMAKE_SOURCE_DIR}/Test/CSharpEngineTest/)

    # Copy shader resources for test if present; otherwise create an empty dir to avoid build failure
    set(SHADER_LAB_RES "${CMAKE_SOURCE_DIR}/Test/CSharpEngineTest/ShaderLabRes")
    set(SHADER_LAB_RES_DEST "${OUTPUT_DIR}/ShaderLabRes")

    if(EXISTS "${SHADER_LAB_RES}")
        add_custom_target(CopyShaderLabRes
            COMMAND ${CMAKE_COMMAND} -E make_directory "${SHADER_LAB_RES_DEST}"
            COMMAND ${CMAKE_COMMAND} -E copy_directory "${SHADER_LAB_RES}" "${SHADER_LAB_RES_DEST}"
            COMMENT "Copy shader lab res"
            USES_TERMINAL
        )
    else()
        message(WARNING "ShaderLabRes not found at ${SHADER_LAB_RES}. Creating empty destination.")
        add_custom_target(CopyShaderLabRes
            COMMAND ${CMAKE_COMMAND} -E make_directory "${SHADER_LAB_RES_DEST}"
            COMMENT "Create empty ShaderLabRes destination"
            USES_TERMINAL
        )
    endif()
    set_target_properties(CopyShaderLabRes PROPERTIES FOLDER "CMakeProcess")

    add_custom_target(BuildCSharpEngineTest ALL
    COMMAND dotnet build ${ARISEN_ENGINE_TEST_PROJ} --configuration $<CONFIG>
    WORKING_DIRECTORY ${ARISEN_ENGINE_TEST_PROJ_DIR}
    COMMENT "Building Arisen Engine Test"
    DEPENDS "ArisenEngine.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildCSharpEngineTest BuildArisenEngine)
    add_dependencies(BuildCSharpEngineTest CopyShaderLabRes)
    set_target_properties(BuildCSharpEngineTest PROPERTIES FOLDER "CMakeProcess")
else()
    message(FATAL_ERROR "Unknown TARGET: ${TARGET}")
endif()
