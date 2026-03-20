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

if(${TARGET} STREQUAL "CSharpEngineTest" OR ${TARGET} STREQUAL "NativeEngineTest" OR ${TARGET} STREQUAL "EditorTest")
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
    if(NOT ${TARGET} STREQUAL "Launcher")
        add_subdirectory(3rdparty/tracy)
        set_target_properties(TracyClient PROPERTIES FOLDER "3rdparty")
    endif()
    add_compile_definitions(ARISEN_PROFILER_ENABLED=1)
    set(DOTNET_PROFILER_ARG "-p:DefineConstants=ARISEN_PROFILER_ENABLED")
else()
    set(ARISEN_PROFILER_ENABLED OFF)
    add_compile_definitions(ARISEN_PROFILER_ENABLED=0)
    set(DOTNET_PROFILER_ARG "")
endif()

set(DOTNET_MACRO_ARG "-p:DefineConstants=${TARGET_MACRO}")

if(NOT ${TARGET} STREQUAL "Launcher")
add_subdirectory(3rdparty/spdlog)
set_target_properties(spdlog PROPERTIES FOLDER "3rdparty")
add_subdirectory(Packages/com.arisen.core.native)
add_subdirectory(Packages/com.arisen.rhi.dx12.native)
add_subdirectory(Packages/com.arisen.rhi.vulkan.native)
endif()


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

set(ARISEN_DAG_DIR ${CMAKE_SOURCE_DIR}/External/ArisenDAG)
set(ARISEN_DAG_PROJ ${ARISEN_DAG_DIR}/ArisenDAG.csproj)

set(ARISEN_KERNEL_PROJ ${CMAKE_SOURCE_DIR}/ArisenKernel/ArisenKernel.csproj)
set(ARISEN_KERNEL_PROJ_DIR ${CMAKE_SOURCE_DIR}/ArisenKernel/)

set(ARISEN_ENGINE_PROJ ${CMAKE_SOURCE_DIR}/Engine/ArisenEngine.csproj)
set(ARISEN_ENGINE_PROJ_DIR ${CMAKE_SOURCE_DIR}/Engine/)

    # Build C# projects
if(NOT ${TARGET} STREQUAL "Launcher")
add_custom_target(BuildBindingGenerator
    COMMAND ${CMAKE_COMMAND} -E env
        dotnet build "${BINDING_GENERATOR_PROJ}"
        --configuration $<CONFIG>
        --output "${BINDING_GENERATOR_EXE}"
        ${DOTNET_PROFILER_ARG} ${DOTNET_MACRO_ARG}
    WORKING_DIRECTORY ${BINDING_GENERATOR_DIR}
    COMMENT "Building Binding Generator"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(BuildBindingGenerator Core.Foundation)
add_dependencies(BuildBindingGenerator Core.Diagnostic)
add_dependencies(BuildBindingGenerator Core.HAL)
add_dependencies(BuildBindingGenerator Core.RHI)
add_dependencies(BuildBindingGenerator Core.ShaderCompiler)
add_dependencies(BuildBindingGenerator RHI.DX12)
add_dependencies(BuildBindingGenerator RHI.Vulkan)

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



add_custom_target(BuildArisenDAG
    COMMAND dotnet build ${ARISEN_DAG_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG} ${DOTNET_MACRO_ARG}
    WORKING_DIRECTORY ${ARISEN_DAG_DIR}
    COMMENT "Building Arisen DAG"
    DEPENDS ${ARISEN_DAG_PROJ}
    VERBATIM
    USES_TERMINAL
)
set_target_properties(BuildArisenDAG PROPERTIES FOLDER "CMakeProcess")

endif() # Explicitly ending the NOT Launcher block early to expose Kernel/Host universally

add_custom_target(BuildArisenKernel ALL
    COMMAND dotnet build ${ARISEN_KERNEL_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG} ${DOTNET_MACRO_ARG}
    WORKING_DIRECTORY ${ARISEN_KERNEL_PROJ_DIR}
    COMMENT "Building Arisen Kernel"
    VERBATIM
    USES_TERMINAL
)
set_target_properties(BuildArisenKernel PROPERTIES FOLDER "CMakeProcess")

set(ARISEN_HOST_PROJ ${CMAKE_SOURCE_DIR}/ArisenHost/ArisenHost.csproj)
set(ARISEN_HOST_PROJ_DIR ${CMAKE_SOURCE_DIR}/ArisenHost/)

add_custom_target(BuildArisenHost ALL
    COMMAND dotnet build ${ARISEN_HOST_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG} ${DOTNET_MACRO_ARG}
    WORKING_DIRECTORY ${ARISEN_HOST_PROJ_DIR}
    COMMENT "Building Arisen Host"
    DEPENDS "ArisenKernel.dll"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(BuildArisenHost BuildArisenKernel)
set_target_properties(BuildArisenHost PROPERTIES FOLDER "CMakeProcess")

if(NOT ${TARGET} STREQUAL "Launcher") # Resuming exclusion block for heavy monolith targets

add_custom_target(BuildArisenEngine
    COMMAND dotnet build ${ARISEN_ENGINE_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG} ${DOTNET_MACRO_ARG}
    WORKING_DIRECTORY ${ARISEN_ENGINE_PROJ_DIR}
    COMMENT "Building Arisen Engine"
    DEPENDS "ArisenDAG.dll" "ArisenKernel.dll"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(BuildArisenEngine GenerateAutoBinding BuildArisenDAG BuildArisenKernel)
set_target_properties(BuildArisenEngine PROPERTIES FOLDER "CMakeProcess")

# Builtin Packages
set(BUILTIN_PACKAGES_DIR ${CMAKE_SOURCE_DIR}/Packages/Builtin)
file(GLOB_RECURSE BUILTIN_PACKAGES_PROJS CONFIGURE_DEPENDS "${BUILTIN_PACKAGES_DIR}/*.csproj")

add_custom_target(BuildBuiltinPackages
    COMMAND ${CMAKE_COMMAND} -E echo "Building Builtin Packages..."
)

foreach(PROJ_PATH IN LISTS BUILTIN_PACKAGES_PROJS)
    get_filename_component(PROJ_NAME ${PROJ_PATH} NAME_WE)
    set(TARGET_NAME "BuildPackage_${PROJ_NAME}")
    
    add_custom_target(${TARGET_NAME}
        COMMAND dotnet build "${PROJ_PATH}" --configuration $<CONFIG> ${DOTNET_PROFILER_ARG} ${DOTNET_MACRO_ARG}
        WORKING_DIRECTORY "${BUILTIN_PACKAGES_DIR}"
        COMMENT "Building Package: ${PROJ_NAME}"
        VERBATIM
        USES_TERMINAL
    )
    add_dependencies(${TARGET_NAME} BuildArisenEngine)
    add_dependencies(BuildBuiltinPackages ${TARGET_NAME})
    set_target_properties(${TARGET_NAME} PROPERTIES FOLDER "CMakeProcess/Packages")
endforeach()
set_target_properties(BuildBuiltinPackages PROPERTIES FOLDER "CMakeProcess")

endif() # End NOT Launcher block

endif()

# Final target setup (Editor, Players, Tests)
    set(ARISEN_EDITOR_PROJ ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor/ArisenEditor.csproj)
    set(ARISEN_EDITOR_PROJ_DIR ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor/)
    set(ARISEN_EDITOR_FRAMEWORK_PROJ ${CMAKE_SOURCE_DIR}/External/ArisenEditorFramework/ArisenEditorFramework.csproj)
    set(ARISEN_EDITOR_FRAMEWORK_PROJ_DIR ${CMAKE_SOURCE_DIR}/External/ArisenEditorFramework/)
    set(ARISEN_EDITOR_DESKTOP_PROJ ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor.Desktop/ArisenEditor.Desktop.csproj)
    set(ARISEN_EDITOR_DESKTOP_PROJ_DIR ${CMAKE_SOURCE_DIR}/Editor/ArisenEditor.Desktop/)

    set(ARISEN_LAUNCHER_PROJ ${CMAKE_SOURCE_DIR}/Editor/ArisenLauncher/ArisenLauncher.csproj)
    set(ARISEN_LAUNCHER_PROJ_DIR ${CMAKE_SOURCE_DIR}/Editor/ArisenLauncher/)
    set(ARISEN_LAUNCHER_DESKTOP_PROJ ${CMAKE_SOURCE_DIR}/Editor/ArisenLauncher.Desktop/ArisenLauncher.Desktop.csproj)
    set(ARISEN_LAUNCHER_DESKTOP_PROJ_DIR ${CMAKE_SOURCE_DIR}/Editor/ArisenLauncher.Desktop/)
    
    set(ARISEN_BUILD_TOOL_PROJ ${CMAKE_SOURCE_DIR}/External/ArisenBuildTool/ArisenBuildTool.csproj)
    set(ARISEN_BUILD_TOOL_PROJ_DIR ${CMAKE_SOURCE_DIR}/External/ArisenBuildTool/)

if(${TARGET} STREQUAL "Editor" OR ${TARGET} STREQUAL "EditorTest")
    add_custom_target(BuildArisenEditorFramework
    COMMAND dotnet build ${ARISEN_EDITOR_FRAMEWORK_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_EDITOR_FRAMEWORK_PROJ_DIR}
    COMMENT "Building Arisen Editor Framework"
    VERBATIM
    USES_TERMINAL
    )
    set_target_properties(BuildArisenEditorFramework PROPERTIES FOLDER "CMakeProcess")
endif()

if(${TARGET} STREQUAL "Editor" OR ${TARGET} STREQUAL "EditorTest")

    add_custom_target(BuildArisenEditor
    COMMAND dotnet build ${ARISEN_EDITOR_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_EDITOR_PROJ_DIR}
    COMMENT "Building Arisen Editor"
    DEPENDS "ArisenEngine.dll" "ArisenEditorFramework.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildArisenEditor BuildArisenEngine BuildArisenEditorFramework)
    set_target_properties(BuildArisenEditor PROPERTIES FOLDER "CMakeProcess")

    if(${TARGET} STREQUAL "Editor")
        set(EDITOR_DESKTOP_ALL "ALL")
    else()
        set(EDITOR_DESKTOP_ALL "")
    endif()

    add_custom_target(BuildArisenEditorDesktop ${EDITOR_DESKTOP_ALL}
    COMMAND dotnet build ${ARISEN_EDITOR_DESKTOP_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_EDITOR_DESKTOP_PROJ_DIR}
    COMMENT "Building Arisen Editor Desktop"
    DEPENDS "ArisenEditor.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildArisenEditorDesktop BuildArisenEditor)
    set_target_properties(BuildArisenEditorDesktop PROPERTIES FOLDER "CMakeProcess")

    set(ARISEN_EDITOR_TEST_PROJ ${CMAKE_SOURCE_DIR}/Test/EditorTest/EditorTest.csproj)
    set(ARISEN_EDITOR_TEST_PROJ_DIR ${CMAKE_SOURCE_DIR}/Test/EditorTest/)

    add_custom_target(BuildEditorTest
    COMMAND dotnet build ${ARISEN_EDITOR_TEST_PROJ} --configuration $<CONFIG> ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_EDITOR_TEST_PROJ_DIR}
    COMMENT "Building Arisen Editor Test"
    DEPENDS "ArisenEditor.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildEditorTest BuildArisenEditor)
    set_target_properties(BuildEditorTest PROPERTIES FOLDER "CMakeProcess")
endif()

if(${TARGET} STREQUAL "Editor" OR ${TARGET} STREQUAL "Launcher")
    # Launcher
    add_custom_target(BuildArisenLauncher
    COMMAND dotnet build ${ARISEN_LAUNCHER_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_LAUNCHER_PROJ_DIR}
    COMMENT "Building Arisen Launcher"
    VERBATIM
    USES_TERMINAL
    )
    set_target_properties(BuildArisenLauncher PROPERTIES FOLDER "CMakeProcess")

    # BuildTool for Launcher Bootstrapping
    add_custom_target(BuildArisenBuildTool ALL
    COMMAND dotnet build ${ARISEN_BUILD_TOOL_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_BUILD_TOOL_PROJ_DIR}
    COMMENT "Building Arisen Build Tool"
    VERBATIM
    USES_TERMINAL
    )
    set_target_properties(BuildArisenBuildTool PROPERTIES FOLDER "CMakeProcess")

    add_custom_target(BuildArisenLauncherDesktop ALL
    COMMAND dotnet build ${ARISEN_LAUNCHER_DESKTOP_PROJ} --configuration $<CONFIG> --output "${OUTPUT_DIR}" ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_LAUNCHER_DESKTOP_PROJ_DIR}
    COMMENT "Building Arisen Launcher Desktop"
    DEPENDS "ArisenLauncher.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildArisenLauncherDesktop BuildArisenLauncher)
    set_target_properties(BuildArisenLauncherDesktop PROPERTIES FOLDER "CMakeProcess")
endif()

if(${TARGET} STREQUAL "Editor")
    # Alredy added above as part of common logic if needed
    message(STATUS "Configuring for Editor")
elseif(${TARGET} STREQUAL "WindowsPlayerSupport")
    # Add your platform player support setup here

elseif(${TARGET} STREQUAL "NativeEngineTest" OR ${TARGET} STREQUAL "DXTest")    

elseif(${TARGET} STREQUAL "CSharpEngineTest")    

    set(ARISEN_ENGINE_TEST_PROJ ${CMAKE_SOURCE_DIR}/Test/CSharpEngineTest/CSharpEngineTest.csproj)
    set(ARISEN_ENGINE_TEST_PROJ_DIR ${CMAKE_SOURCE_DIR}/Test/CSharpEngineTest/)

    set(SHARED_ASSETS_DIR "${CMAKE_SOURCE_DIR}/Test/Assets")
    set(SHARED_SHADER_DIR "${CMAKE_SOURCE_DIR}/Test/Shader")

    # Include resources in SLN
    file(GLOB_RECURSE SHADER_FILES CONFIGURE_DEPENDS "${SHARED_SHADER_DIR}/*")
    file(GLOB_RECURSE ASSET_FILES CONFIGURE_DEPENDS "${SHARED_ASSETS_DIR}/*")
    set_source_files_properties(${SHADER_FILES} PROPERTIES HEADER_FILE_ONLY TRUE)
    set_source_files_properties(${ASSET_FILES} PROPERTIES HEADER_FILE_ONLY TRUE)
    source_group(TREE "${SHARED_SHADER_DIR}" PREFIX "Shader" FILES ${SHADER_FILES})
    source_group(TREE "${SHARED_ASSETS_DIR}" PREFIX "Assets" FILES ${ASSET_FILES})

    add_custom_target(TestResources SOURCES ${SHADER_FILES} ${ASSET_FILES})
    set_target_properties(TestResources PROPERTIES FOLDER "Resources")

    add_custom_target(BuildCSharpEngineTest ALL
    COMMAND dotnet build ${ARISEN_ENGINE_TEST_PROJ} --configuration $<CONFIG> ${DOTNET_PROFILER_ARG}
    WORKING_DIRECTORY ${ARISEN_ENGINE_TEST_PROJ_DIR}
    COMMENT "Building Arisen Engine Test"
    DEPENDS "ArisenEngine.dll"
    VERBATIM
    USES_TERMINAL
    )
    add_dependencies(BuildCSharpEngineTest BuildArisenEngine)
    add_dependencies(BuildCSharpEngineTest BuildBuiltinPackages)
    set_target_properties(BuildCSharpEngineTest PROPERTIES FOLDER "CMakeProcess")
elseif(${TARGET} STREQUAL "EditorTest")
    message(STATUS "Configuring for EditorTest")
    add_custom_target(BuildEditorTestAll ALL)
    add_dependencies(BuildEditorTestAll BuildEditorTest)
elseif(${TARGET} STREQUAL "BindingGenerator")
    message(STATUS "Configuring for Standalone Binding Generation")
elseif(${TARGET} STREQUAL "Launcher")
    message(STATUS "Configuring for Launcher")
else()
    message(FATAL_ERROR "Unknown TARGET: ${TARGET}")
endif()
