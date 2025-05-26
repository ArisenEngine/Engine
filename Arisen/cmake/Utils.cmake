# cmake/Utils.cmake
# 工具函数合集：统一 DLL 拷贝、输出目录设置、源码收集等

# 控制是否启用构建后复制 DLL 的步骤（默认启用）
option(ENABLE_RUNTIME_COPY "Enable runtime DLL copy step" ON)

# ✅ 拷贝 runtime DLL 到构建输出目录
# 参数：
#   target      – 构建目标名称
#   dll_target  – DLL 的构建目标名，如 dxcompiler
function(copy_runtime_dll target dll_target)
    if(ENABLE_RUNTIME_COPY)
        add_custom_command(TARGET ${target} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
                $<TARGET_FILE:${dll_target}>
                ${OUTPUT_DIR}
            COMMENT "Copying $<TARGET_FILE_NAME:${dll_target}> to ${OUTPUT_DIR}"
        )
    endif()
endfunction()

# ✅ 统一设置输出目录
# 参数：
#   target – 构建目标
function(set_common_output_dirs target)
    set_target_properties(${target} PROPERTIES
        ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_DIR}
        LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR}
        RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR}
    )
endfunction()

# ✅ 收集 C++ 源文件和头文件（自动排除 Tests 目录）
# 参数：
#   return_var – 要返回的变量名
#   root_dir   – 源码根目录
function(collect_sources return_var root_dir)
    file(GLOB_RECURSE ALL_CPP_FILES CONFIGURE_DEPENDS
        ${root_dir}/*.cpp
    )
    file(GLOB_RECURSE ALL_HEADER_FILES CONFIGURE_DEPENDS
        ${root_dir}/*.h
        ${root_dir}/*.hpp
    )

    set(SOURCE_FILES "")
    foreach(file_path IN LISTS ALL_CPP_FILES ALL_HEADER_FILES)
        if(NOT file_path MATCHES "/Tests/")
            list(APPEND SOURCE_FILES ${file_path})
        endif()
    endforeach()

    list(LENGTH ALL_CPP_FILES CPP_COUNT)
    list(LENGTH ALL_HEADER_FILES HEADER_COUNT)
    list(LENGTH SOURCE_FILES TOTAL_COUNT)

    message(STATUS "[collect_sources] Collected ${TOTAL_COUNT} files from ${root_dir}")
    message(STATUS "[collect_sources]    ${CPP_COUNT} .cpp files, ${HEADER_COUNT} header files")

    set(${return_var} ${SOURCE_FILES} PARENT_SCOPE)
endfunction()

function(setup_dxc_for_target target dxc_root)
    # 自动判断架构
    if(CMAKE_SIZEOF_VOID_P EQUAL 8)
        set(DXC_ARCH_DIR "x64")
    elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "arm64")
        set(DXC_ARCH_DIR "arm64")
    else()
        set(DXC_ARCH_DIR "x86")
    endif()

    # 构造路径
    set(DXC_INCLUDE_DIR "${dxc_root}/inc")
    set(DXC_LIB_DIR     "${dxc_root}/lib/${DXC_ARCH_DIR}")
    set(DXC_BIN_DIR     "${dxc_root}/bin/${DXC_ARCH_DIR}")
    set(DXC_DLL         "${DXC_BIN_DIR}/dxcompiler.dll")
    set(DXC_LIB         "${DXC_LIB_DIR}/dxcompiler.lib")

    # 如果还未定义 dxcompiler 目标，就定义
    if(NOT TARGET dxcompiler)
        add_library(dxcompiler SHARED IMPORTED GLOBAL)
        set_target_properties(dxcompiler PROPERTIES
            IMPORTED_LOCATION_RELEASE "${DXC_DLL}"
            IMPORTED_IMPLIB_RELEASE   "${DXC_LIB}"
            IMPORTED_LOCATION_DEBUG   "${DXC_DLL}"     # 可以调整为调试版 DLL 路径
            IMPORTED_IMPLIB_DEBUG     "${DXC_LIB}"
            IMPORTED_CONFIGURATIONS   "Debug;Release"
        )
    endif()

    # 设置 include / link
    target_include_directories(${target} PRIVATE "${DXC_INCLUDE_DIR}")
    target_link_libraries(${target} PRIVATE dxcompiler)

    # 获取输出目录
    get_property(OUTPUT_DIR GLOBAL PROPERTY GLOBAL_OUTPUT_DIR)
    message(STATUS "Global Output Dir: ${OUTPUT_DIR}")
    if(NOT OUTPUT_DIR)
        set(OUTPUT_DIR "$<TARGET_FILE_DIR:${target}>")  # fallback：默认输出目录
    endif()

    # 拷贝 dxcompiler.dll 和 dxil.dll
    add_custom_command(TARGET ${target} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${DXC_BIN_DIR}/dxcompiler.dll"
            "${DXC_BIN_DIR}/dxil.dll"
            "${OUTPUT_DIR}"
        COMMENT "Copying DXC runtime DLLs to output directory"
    )
endfunction()

