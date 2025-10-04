#define _LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS
#define _LIBCPP_HIDE_FROM_ABI

#include <string>
#include <new>

template __declspec(dllexport) std::allocator<char>::allocator() noexcept;
