![Contributors](https://contrib.rocks/image?repo=ArisenEngine/Engine)<br>
![Contributors](https://img.shields.io/github/contributors/ArisenEngine/Engine)

[![License](https://img.shields.io/github/license/ArisenEngine/Engine)](https://github.com/ArisenEngine/Engine/blob/main/LICENSE)

# Requirements

### Vulkan SDK

### C++ ATL

### DotNet 9.0

### Tracy 编译环境

cmake version 3.29.2
window sdk version 10+

```
cmake -B projects -G "Visual Studio 17 2022" -A x64 -DCMAKE_CONFIGURATION_TYPES="Debug;Release" -DBUILD_SHARED_LIBS=ON -DCMAKE_SYSTEM_VERSION=10.0

```

```
cmake --build projects --config Debug   # 构建 Debug 版本
cmake --build projects --config Release # 构建 Release 版本
```
