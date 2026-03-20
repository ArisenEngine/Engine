cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.Diagnostic SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.Diagnostic PRIVATE
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.Foundation
    )  

target_include_directories(Core.Diagnostic PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/Profiler
)

target_compile_definitions(Core.Diagnostic PRIVATE COREDIAGNOSTIC_EXPORTS)

if(ARISEN_PROFILER_ENABLED)
    target_link_libraries(Core.Diagnostic PUBLIC TracyClient)
endif()

target_link_libraries(Core.Diagnostic PRIVATE spdlog Core.Foundation)

set_target_properties(Core.Diagnostic PROPERTIES FOLDER "Core")

