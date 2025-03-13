#include <windows.h>
#include "ProfilerTools.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:  // NOLINT(bugprone-branch-clone)
        Arisen::Tools::Profiler::Initialize();
        break;
    case DLL_PROCESS_DETACH:
        Arisen::Tools::Profiler::Terminate();
        break;
    default: ;
    }
	
    return TRUE;
}

