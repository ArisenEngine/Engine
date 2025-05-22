cmake_minimum_required(VERSION 3.29)
project(Core.Debugger LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
file(GLOB_RECURSE ALL_CPP_FILES CONFIGURE_DEPENDS
    ${CMAKE_CURRENT_SOURCE_DIR}/*.cpp
)

file(GLOB_RECURSE ALL_HEADER_FILES CONFIGURE_DEPENDS
    ${CMAKE_CURRENT_SOURCE_DIR}/*.h
    ${CMAKE_CURRENT_SOURCE_DIR}/*.hpp
)

set(ALL_SOURCE_FILES ${ALL_CPP_FILES} ${ALL_HEADER_FILES})

set(CORE_DEBUGGER_SOURCES "")
foreach(file_path IN LISTS ALL_SOURCE_FILES)
    if(
        NOT file_path MATCHES "/Tests/"
      )
        
        list(APPEND CORE_DEBUGGER_SOURCES ${file_path})
    endif()
endforeach()

list(LENGTH CORE_DEBUGGER_SOURCES CORE_DEBUGGER_SOURCES_COUNT)
message(STATUS "Found ${CORE_DEBUGGER_SOURCES_COUNT} source/header files for Core.Debugger")

add_library(Core.Debugger SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.Debugger PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(Core.Debugger PRIVATE COREDEBUGGER_EXPORTS)

set_target_properties(Core.Debugger PROPERTIES
    PREFIX "" 
)

target_link_libraries(Core.Debugger PRIVATE spdlog Core.Infra)