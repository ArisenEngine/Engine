cmake_minimum_required(VERSION 3.29)
# Parse TARGET and PLATFORM
if(NOT DEFINED TARGET)
    message(FATAL_ERROR "Please specify -DTARGET=[Editor|WindowsPlayerSupport|AndroidPlayerSupport|...].")
endif()

if(NOT DEFINED PLATFORM)
    message(FATAL_ERROR "Please specify -DPLATFORM=[Windows|Android|Linux|Apple].")
endif()

project("Arisen${TARGET}" LANGUAGES CXX)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake")
include(Utils)

string(TIMESTAMP START_TIME "%s")

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

option(ARISEN_GENERATE_PROJECT "Whether to generate IDE project files (sln/vcxproj/xcodeproj)" OFF)

add_definitions(-DUNICODE -D_UNICODE)
set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -Wl,--verbose")

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
elseif(PLATFORM_UPPER STREQUAL "ANDROID")
    set(PLATFORM_MACRO "ANDROID_PLATFORM")
    set(ENGINE_LIB_SUFFIX ".so")
elseif(PLATFORM_UPPER STREQUAL "APPLE")
    set(PLATFORM_MACRO "APPLE_PLATFORM")
    set(ENGINE_LIB_SUFFIX ".a")
elseif(PLATFORM_UPPER STREQUAL "LINUX")
    set(PLATFORM_MACRO "LINUX_PLATFORM")
    set(ENGINE_LIB_SUFFIX ".so")
else()
    set(PLATFORM_MACRO "UNKNOWN_PLATFORM")
endif()
add_compile_definitions(${PLATFORM_MACRO}=1)
message(STATUS "Detected platform macro: ${PLATFORM_MACRO}")

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
    set(PROJECT_DIR "${CMAKE_SOURCE_DIR}/Projects/Visual Studio/${TARGET}")
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

# Add C++ Submodules
add_subdirectory(Core/Core.Infra)
add_subdirectory(Tools/Profiler)
add_subdirectory(3rdparty/spdlog)
add_subdirectory(Core/Core.Debugger)
add_subdirectory(Core/Core.Platform)
add_subdirectory(Core/RHI.DX12)
add_subdirectory(Core/RHI.Vulkan)
add_subdirectory(Engine/NativeEngine)

# Setup .NET Paths
set(BINDING_GENERATOR_DIR ${CMAKE_SOURCE_DIR}/BindingGenerator)
set(BINDING_GENERATOR_PROJ ${BINDING_GENERATOR_DIR}/BindingGenerator.csproj)
set(BINDING_GENERATOR_EXE ${OUTPUT_DIR}/Generator)

set(AUTO_BINDING_DIR ${CMAKE_SOURCE_DIR}/AutoBinding)
set(AUTO_BINDING_PROJ ${AUTO_BINDING_DIR}/AutoBinding.csproj)

set(ARISEN_ENGINE_PROJ ${CMAKE_SOURCE_DIR}/Engine/ArisenEngine/ArisenEngine.csproj)

# Build C# Projects
add_custom_target(BuildBindingGenerator
    COMMAND dotnet build ${BINDING_GENERATOR_PROJ} --configuration ${CMAKE_BUILD_TYPE} --output ${BINDING_GENERATOR_EXE}
    WORKING_DIRECTORY ${BINDING_GENERATOR_DIR}
    COMMENT "Building Binding Generator"
    VERBATIM
    USES_TERMINAL
)

add_custom_target(BuildAutoBinding
    COMMAND dotnet build ${AUTO_BINDING_PROJ} --configuration ${CMAKE_BUILD_TYPE}
    WORKING_DIRECTORY ${AUTO_BINDING_DIR}
    COMMENT "Building AutoBinding Project"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(BuildAutoBinding BuildBindingGenerator)

# Generate auto binding
add_custom_target(GenerateAutoBinding
    COMMAND BindingGenerator.exe --source_code ${CMAKE_SOURCE_DIR} --output ${CMAKE_SOURCE_DIR} --library ${OUTPUT_DIR}
    WORKING_DIRECTORY ${BINDING_GENERATOR_EXE}
    COMMENT "Generating Auto Binding Project"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(GenerateAutoBinding BuildAutoBinding)
add_dependencies(GenerateAutoBinding Core.Infra)

# Visual Studio Solution Integration
if(MSVC AND ARISEN_GENERATE_PROJECT)
    add_custom_target(BindingGeneratorProj SOURCES ${BINDING_GENERATOR_PROJ})
    add_custom_target(AutoBindingProj SOURCES ${AUTO_BINDING_PROJ})
    add_custom_target(ArisenEngineProj SOURCES ${ARISEN_ENGINE_PROJ})

    add_dependencies(AutoBindingProj BindingGeneratorProj)
    add_dependencies(ArisenEngineProj AutoBindingProj)
endif()

# Final Target Setup
if(${TARGET} STREQUAL "Editor")
    add_custom_target(BuildEditor ALL)
    string(TIMESTAMP END_TIME "%s")
    math(EXPR ELAPSED_TIME "${END_TIME} - ${START_TIME}")
    message(STATUS "\\033[32m Configure success. Elapsed time: ${ELAPSED_TIME} seconds \\033[0m")
elseif(${TARGET} STREQUAL "WindowsPlayerSupport")
    # Add your platform player support setup here
else()
    message(FATAL_ERROR "Unknown TARGET: ${TARGET}")
endif()
