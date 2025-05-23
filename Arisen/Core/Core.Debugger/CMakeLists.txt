cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.Debugger SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.Debugger PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(Core.Debugger PRIVATE COREDEBUGGER_EXPORTS)

target_link_libraries(Core.Debugger PRIVATE spdlog Core.Infra)
