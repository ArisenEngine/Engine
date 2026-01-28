#pragma once

// Standard C/C++ headers
#include <stdint.h>
#include <math.h>
#include <memory>
#include <typeinfo>
#include <iostream>
#include <fstream>
#include <wchar.h>
#include <string>
#include <cwchar>
#include <algorithm>
#include <optional>
#include <vector>
#include <map>
#include <set>
#include <unordered_map>
#include <unordered_set>
#include <string_view>
#include <utility>
#include <format>
#include <source_location>
#include <cassert>
#include <cstdlib>
#include <stdexcept>
#include <chrono>
#include <thread>
#include <atomic>
#include <mutex>
#include <condition_variable>

#if _WIN64
#include <DirectXMath.h>
#include <Windows.h>
// TODO: Need Consider Multiple Platform
#define VK_USE_PLATFORM_WIN32_KHR 1
#endif
