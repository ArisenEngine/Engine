/**

MIT License

Copyright (c) 2023 DreameatingCat

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

    @Author: DreameatingCat
    @Decription: Instance is act as a bridge with app and graphics driver
    @Date: 2023年11月22日
    @Modify:
    
**/


#pragma once
#include"Debugger/Logger.h"
#include "../Common/PrimitiveTypes.h"

namespace NebulaEngine::RHI
{
    struct AppInfo
    {
        /** app name */
        const char* name;
        /** engine name */
        const char* engineName;
        /** enable validation layer */
        bool validationLayer;
        /** API Version */
        u32 variant, major, minor, patch;
        /** App Version */
        u32 appMajor, appMinor, appPatch;
        /** App Version */
        u32 engineMajor, engineMinor, enginePatch;
    };
    
    class Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(Instance)
        Instance(AppInfo&& createInfo) {}
        virtual ~Instance() noexcept { LOG_INFO("~Instance()"); }
    };
}

