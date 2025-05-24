list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake")
include(Utils)

set(CMAKE_RC_COMPILER "")
set(CMAKE_LINKER_USE_MANIFEST OFF)

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


# Unified output path
set(OUTPUT_DIR ${CMAKE_BINARY_DIR}/${TARGET}/${CMAKE_BUILD_TYPE}/outputs)
file(MAKE_DIRECTORY ${OUTPUT_DIR})
set_property(GLOBAL PROPERTY GLOBAL_OUTPUT_DIR ${OUTPUT_DIR})

set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_DIR})
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR})

# 生成项目文件目录（这里只用来提示，真正控制生成目录通过 cmake -B 指定）
set(PROJECT_GENERATE_DIR "")
if(ARISEN_GENERATE_PROJECT)
    if(${CMAKE_GENERATOR} MATCHES "Visual Studio")
        set(PROJECT_GENERATE_DIR VisualStudio)
    elseif(${CMAKE_GENERATOR} MATCHES "Xcode")
        set(PROJECT_GENERATE_DIR Xcode)
    else()
        set(PROJECT_GENERATE_DIR "")
    endif()
endif()

if(PROJECT_GENERATE_DIR)
    set(PROJECT_DIR ${CMAKE_SOURCE_DIR}/Projects/${PROJECT_GENERATE_DIR})
    file(MAKE_DIRECTORY ${PROJECT_DIR})
    message(STATUS "Project files will be generated under: ${PROJECT_DIR}")
endif()


if(DEFINED PROJECT_GENERATE_DIR AND NOT PROJECT_GENERATE_DIR STREQUAL "")
    set(PROJECT_DIR ${CMAKE_SOURCE_DIR}/Projects/${PROJECT_GENERATE_DIR})
    file(MAKE_DIRECTORY ${PROJECT_DIR})

    foreach(OUTPUTCONFIG IN LISTS CMAKE_CONFIGURATION_TYPES)
        string(TOUPPER ${OUTPUTCONFIG} OUTPUTCONFIG)
        set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${OUTPUTCONFIG} ${PROJECT_DIR})
        set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${OUTPUTCONFIG} ${PROJECT_DIR})
        set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_${OUTPUTCONFIG} ${PROJECT_DIR})
    endforeach()
else()
    # 单配置生成器（比如 Makefile 或 Ninja）直接设置
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR} CACHE PATH "Runtime output directory" FORCE)
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR} CACHE PATH "Library output directory" FORCE)
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_DIR} CACHE PATH "Archive output directory" FORCE)    
endif()



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
add_dependencies(GenerateAutoBinding Core.Debugger)

if(${TARGET} STREQUAL "Editor")

    add_custom_target(BuildEditor ALL)
    string(TIMESTAMP END_TIME "%s")
    math(EXPR ELAPSED_TIME "${END_TIME} - ${START_TIME}")
    message(STATUS "\\033[32m Configure success. Elapsed time: ${ELAPSED_TIME} seconds \\033[0m")
elseif(${TARGET} STREQUAL "WindowsPlayerSupport")
   
else()
    message(FATAL_ERROR "Unknown TARGET: ${TARGET}")
endif()
