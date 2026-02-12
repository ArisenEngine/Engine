#include "PlatformPath.h"
#include <windows.h>
#include <filesystem>

namespace ArisenEngine::HAL
{
    String PlatformPath::GetExecutableDirectory()
    {
        char buffer[MAX_PATH];
        GetModuleFileNameA(NULL, buffer, MAX_PATH);
        std::filesystem::path path(buffer);
        // Use generic_string() to ensure forward slashes, which are generally safer cross-platform
        // though Arisen might not care. using string() is fine on Windows.
        return String(path.parent_path().string());
    }
}
