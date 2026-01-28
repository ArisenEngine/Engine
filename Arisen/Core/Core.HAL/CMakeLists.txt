cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.HAL SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.HAL PRIVATE
    ${CMAKE_SOURCE_DIR}/Core/Core.Foundation
    ${CMAKE_SOURCE_DIR}/Core/Core.Diagnostic
    )    

target_include_directories(Core.HAL PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(Core.HAL PRIVATE COREHAL_EXPORTS)

target_link_libraries(Core.HAL
    PRIVATE 
    Core.Diagnostic
    Core.Foundation
)

set_target_properties(Core.HAL PROPERTIES FOLDER "Core")
