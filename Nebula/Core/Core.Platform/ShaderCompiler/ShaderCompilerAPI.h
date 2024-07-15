#pragma once
#include <windows.h>
#include "dxcapi.h"
#include <atlbase.h>

#include "../Common.h"
#include "Logger/Logger.h"

namespace NebulaEngine::Platforms
{
    static CComPtr<IDxcLibrary> s_DXCLibrary;
    static CComPtr<IDxcCompiler3> s_DXCompiler;
    static CComPtr<IDxcUtils> s_DXCUtils;
    extern "C" DLL void InitDXC();
    inline void InitDXC()
    {
        HRESULT hres;
        
        // Initialize DXC library
        hres = DxcCreateInstance(CLSID_DxcLibrary, IID_PPV_ARGS(&s_DXCLibrary));
        if (FAILED(hres))
        {
            LOG_FATAL_AND_THROW("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Library");
        }

        // Initialize DXC compiler
        hres = DxcCreateInstance(CLSID_DxcCompiler, IID_PPV_ARGS(&s_DXCompiler));
        if (FAILED(hres))
        {
            LOG_FATAL_AND_THROW("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Compiler");
        }

        // Initialize DXC utility
        hres = DxcCreateInstance(CLSID_DxcUtils, IID_PPV_ARGS(&s_DXCUtils));
        if (FAILED(hres))
        {
           LOG_FATAL_AND_THROW("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Utiliy");
        }

        LOG_DEBUG("[NebulaEngine::Platforms::InitDXC]: DXC init. ");
        
    }

    extern "C" DLL void ReleaseDXC();
    inline void ReleaseDXC()
    {
        s_DXCompiler.Release();
        s_DXCLibrary.Release();
        s_DXCUtils.Release();
        LOG_DEBUG("[Platforms::ReleaseDXC]: DXC Release. ");
    }
    
    
}
