cmake_minimum_required(VERSION 3.29)
project(Core.Infra LANGUAGES CXX)

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

set(CORE_INFRA_SOURCES "")
foreach(file_path IN LISTS ALL_SOURCE_FILES)
    if(
        NOT file_path MATCHES "/Tests/"
      )
        list(APPEND CORE_INFRA_SOURCES ${file_path})
    endif()
endforeach()

list(LENGTH CORE_INFRA_SOURCES CORE_INFRA_SOURCES_COUNT)

message(STATUS "Found ${CORE_INFRA_SOURCES_COUNT} source/header files for Core.Infra")

add_library(Core.Infra SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.Infra PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(Core.Infra PRIVATE CORE_INFRA_EXPORTS)

set_target_properties(Core.Infra PROPERTIES
    PREFIX "" 
)