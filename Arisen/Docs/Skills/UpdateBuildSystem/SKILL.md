---
name: update_build_system
description: "How to modify CMake and MSBuild files to add new modules safely."
---

# Updating the Build System

Arisen Engine builds C++ (Vulkan/Core) via CMake, and C# via MSBuild/`dotnet`. The `Scripts` folder contains the orchestration.

## Rules:
1. **Adding C++ Files**: C++ source files and headers are automatically collected! You do **NOT** need to explicitly list every `.cpp` or `.h` file in the `CMakeLists.txt` thanks to the `collect_sources` utility macro. 
   - However, if you create a brand new directory module, you must ensure `collect_sources` is called on that directory.
2. **Adding C# Files**: C# files are globally picked up via `<Compile Include="**\*.cs" />` globs in `.csproj`. If you are adding entirely new Class Libraries (Projects), you must:
   - Create the `.csproj`.
   - Add it to the root `.sln` via `dotnet sln add`.
3. **Build Scripts**: **NEVER** edit the `.bat` or `.sh` files in `Scripts` unless explicitly modifying the build pipeline steps (e.g., changing the CMake Generator).

## Example: Modifying CMake

```cmake
# Core.Math/CMakeLists.txt

# The utility automatically globs all .cpp and .h in the current directory
collect_sources(SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR})

add_library(Core.Math SHARED ${SOURCE_FILES})
```
