#include <windows.h>
#include<iostream>

#include <string>
#include <io.h>
#include <fcntl.h>
#include <iostream>

#include <glm/vec4.hpp>
#include <glm/matrix.hpp>

#include "VulkanApplication.h"

void RedirectIOToConsole(void)
{
    AllocConsole();

    // Get the handle for STDOUT's file system.
    HANDLE stdOutputHandle = GetStdHandle(STD_OUTPUT_HANDLE);

    // Redirect STDOUT to the new console by associating STDOUT's file
    // descriptor with an existing operating-system file handle.
    int hConsoleHandle = _open_osfhandle((intptr_t)stdOutputHandle, _O_TEXT);

    FILE* pFile = _fdopen(hConsoleHandle, "w");

    *stdout = *pFile;
    freopen_s(&pFile, "CONIN$", "r+t", stdout);
    freopen_s(&pFile, "CONOUT$", "w+t", stdout);

    setvbuf(stdout, NULL, _IONBF, 0);

    // This call ensures that iostream and C run-time library operations occur 
    // in the order that they appear in source code.
    std::ios::sync_with_stdio();

}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow)
{
    RedirectIOToConsole();

    auto vkApp = VulkanApplication(1920, 1080);

    glm::mat4x4 matrix;
    glm::vec4 vec;
    auto result = matrix * vec;


	return vkApp.Run(hInstance, nCmdShow);
}