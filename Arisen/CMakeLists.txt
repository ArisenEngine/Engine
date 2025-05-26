list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake")
include(Utils)

cmake_minimum_required(VERSION 3.29)
project(Arisen LANGUAGES CXX)

string(TIMESTAMP START_TIME "%s")

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)


#option
option(ARISEN_GENERATE_PROJECT "Whether to generate IDE project files (sln/vcxproj/xcodeproj)" OFF)


add_definitions(-DUNICODE -D_UNICODE)



set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -Wl,--verbose")

# --------------------------
# Parse TARGET and PLATFORM
# --------------------------
if(NOT DEFINED TARGET)
    message(FATAL_ERROR "Please specify -DTARGET=[Editor|WindowsPlayerSupport|AndroidPlayerSupport|...].")
endif()

if(NOT DEFINED PLATFORM)
    message(FATAL_ERROR "Please specify -DPLATFORM=[Windows|Android|Linux|Apple].")
endif()

# --------------------------
# Set Macro
# --------------------------
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


# --------------------------
# Unified output path
# --------------------------
# 获取生成器名
string(TOLOWER "${CMAKE_GENERATOR}" _GEN_LOWER)

# 是否多配置生成器（如 VS/Xcode）
set(IS_MULTI_CONFIG FALSE)
if(CMAKE_CONFIGURATION_TYPES)
    set(IS_MULTI_CONFIG TRUE)
endif()

# 设置项目生成目录
set(PROJECT_DIR "")
if(_GEN_LOWER MATCHES "visual studio")
    set(PROJECT_DIR "${CMAKE_SOURCE_DIR}/Projects/Visual Studio")
elseif(_GEN_LOWER MATCHES "xcode")
    set(PROJECT_DIR "${CMAKE_SOURCE_DIR}/Projects/Xcode")
endif()

if(PROJECT_DIR)
    file(MAKE_DIRECTORY ${PROJECT_DIR})
    message(STATUS "Generating project files into: ${PROJECT_DIR}")
endif()

# 设置构建输出目录
if(PROJECT_DIR)
    # 对于 VS/Xcode，输出目录在 Projects/XXX/outputs/$(Configuration)
    set(OUTPUT_DIR "${PROJECT_DIR}/${CMAKE_CFG_INTDIR}")
else()
    # 对于默认生成器（如 Ninja/Unix Makefiles），输出目录为 build/outputs
    set(OUTPUT_DIR "${CMAKE_BINARY_DIR}/outputs")
endif()

# 设置全局输出路径属性（供所有 target 使用）
set_property(GLOBAL PROPERTY GLOBAL_OUTPUT_DIR ${OUTPUT_DIR})

# 配置输出路径
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


add_subdirectory(Core/Core.Infra)

add_subdirectory(Tools/Profiler)

# spdlog
add_subdirectory(3rdparty/spdlog)
set_target_properties(spdlog PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR}
)

# dxc compile dxc if needed in future
# set(LLVM_TARGETS_TO_BUILD "X64" CACHE STRING "" FORCE)
# add_subdirectory(3rdparty/DirectXShaderCompiler)
# set_target_properties(dxcompiler PROPERTIES
#     ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_DIR}
#     LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR}
#     RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR}
# )
# set_target_properties(dxcompiler PROPERTIES
#     PREFIX "" 
# )

add_subdirectory(Core/Core.Debugger)

add_subdirectory(Core/Core.Platform)
add_subdirectory(Core/RHI.DX12)
add_subdirectory(Core/RHI.Vulkan)
add_subdirectory(Engine/NativeEngine)

set(BINDING_GENERATOR_DIR ${CMAKE_SOURCE_DIR}/BindingGenerator)
set(BINDING_GENERATOR_PROJ ${CMAKE_SOURCE_DIR}/BindingGenerator/BindingGenerator.csproj)
set(BINDING_GENERATOR_EXE ${OUTPUT_DIR}/Generator)
set(AUTO_BINDING_DIR ${CMAKE_SOURCE_DIR}/AutoBinding)
set(AUTO_BINDING_PROJECT ${AUTO_BINDING_DIR}/AutoBinding.csproj)
set(ARISEN_ENGINE_PROJECT ${CMAKE_SOURCE_DIR}/Engine/ArisenEngine/ArisenEngine.csproj)

## Build Binding And Generate
add_custom_target(BuildBindingGenerator
    COMMAND dotnet build ${BINDING_GENERATOR_PROJ} --configuration ${CMAKE_BUILD_TYPE} --output ${BINDING_GENERATOR_EXE}
    WORKING_DIRECTORY ${BINDING_GENERATOR_DIR}
    COMMENT "Building Binding Generator"
    VERBATIM
    USES_TERMINAL
)

add_custom_target(GenerateAutoBinding
    COMMAND BindingGenerator.exe --source_code ${CMAKE_SOURCE_DIR} --output ${CMAKE_SOURCE_DIR} --library ${OUTPUT_DIR}
    WORKING_DIRECTORY ${BINDING_GENERATOR_EXE}
    COMMENT "Generating Auto Binding Project\n"
    VERBATIM
    USES_TERMINAL
)
add_dependencies(GenerateAutoBinding BuildBindingGenerator)
add_dependencies(GenerateAutoBinding Core.Infra)

if(${TARGET} STREQUAL "Editor")

    add_custom_target(BuildEditor ALL)
    string(TIMESTAMP END_TIME "%s")
    math(EXPR ELAPSED_TIME "${END_TIME} - ${START_TIME}")
    message(STATUS "\\033[32m Configure success. Elapsed time: ${ELAPSED_TIME} seconds \\033[0m")
elseif(${TARGET} STREQUAL "WindowsPlayerSupport")
   
else()
    message(FATAL_ERROR "Unknown TARGET: ${TARGET}")
endif()
