cmake_minimum_required(VERSION 3.29)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect code files
collect_sources(ALL_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(RHI.DX12 SHARED
    ${ALL_SOURCE_FILES}
)

target_include_directories(RHI.DX12 PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

target_include_directories(RHI.DX12 PRIVATE
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.Foundation
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.Diagnostic
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.HAL
    `${CMAKE_SOURCE_DIR}/Packages/com.arisen.core.native/Source/Core.RHI
    )  


target_compile_definitions(RHI.DX12 PRIVATE RHIDX12_EXPORTS)

target_link_libraries(RHI.DX12 PUBLIC Core.Foundation Core.Diagnostic Core.HAL Core.RHI)

set_target_properties(RHI.DX12 PROPERTIES FOLDER "RHI")

