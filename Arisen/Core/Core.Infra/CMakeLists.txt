cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.Infra SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.Infra PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(Core.Infra PRIVATE COREINFRA_EXPORTS)

target_link_libraries(Core.Infra PRIVATE Profiler)