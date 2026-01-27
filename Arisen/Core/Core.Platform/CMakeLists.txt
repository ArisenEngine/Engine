cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(DXC_ROOT ${CMAKE_SOURCE_DIR}/3rdparty/dxc)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.Platform SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(Core.Platform PRIVATE
    ${CMAKE_SOURCE_DIR}/Core/Core.Infra
    ${CMAKE_SOURCE_DIR}/Core/Core.Debugger
    )    

target_include_directories(Core.Platform PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(Core.Platform PRIVATE COREPLATFORM_EXPORTS)

target_link_libraries(Core.Platform
    PRIVATE 
    Core.Debugger
    Core.Infra
)

# setup dxc
if (CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    add_compile_options(-Wno-attributes)
endif()
setup_dxc_for_target(Core.Platform "${CMAKE_SOURCE_DIR}/3rdparty/dxc")

set_target_properties(Core.Platform PROPERTIES FOLDER "Core")