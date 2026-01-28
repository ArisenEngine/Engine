cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.RHI SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.RHI PRIVATE
    ${CMAKE_SOURCE_DIR}/Core/Core.Foundation
    ${CMAKE_SOURCE_DIR}/Core/Core.Diagnostic
    ${CMAKE_SOURCE_DIR}/Core/Core.HAL
    )    

target_include_directories(Core.RHI PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/RHI
)

target_compile_definitions(Core.RHI PRIVATE CORE_RHI_EXPORTS)

target_link_libraries(Core.RHI
    PRIVATE 
    Core.Diagnostic
    Core.Foundation
    Core.HAL
)

set_target_properties(Core.RHI PROPERTIES FOLDER "Core")
