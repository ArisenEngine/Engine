cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.Foundation SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.Foundation PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/Base
)

target_compile_definitions(Core.Foundation PRIVATE COREFOUNDATION_EXPORTS)

# No private dependencies for Core.Foundation

set_target_properties(Core.Foundation PROPERTIES FOLDER "Core")
