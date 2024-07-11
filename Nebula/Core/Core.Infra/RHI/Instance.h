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
#include "../Common/CommandHeaders.h"
#include "../Common/PrimitiveTypes.h"

namespace NebulaEngine::RHI
{
    class Surface;

    struct InstanceInfo
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
        VIRTUAL_DECONSTRUCTOR(Instance)
        
        Instance(InstanceInfo&& instance_info)
        {
            
        }
        
        bool IsEnableValidation()
        {
            return m_EnableValidation;
        }

        virtual void* GetHandle() const = 0;

        virtual void InitLogicDevices() = 0;

        
        /// \brief used for DXC args
        /// \return api env value
        virtual const std::string GetEnvString() const = 0;

        virtual void CreateSurface(u32&& windowId) = 0;
        virtual void DestroySurface(u32&& windowId) = 0;
        virtual const Surface& GetSurface(u32&& windowId) = 0;
    
    protected:

        // NOTE: 这里在静态库，所以m_Surfaces在不同的api dll上会有多份拷贝，不过同时只会加载一种dll。
        Containers::Map<u32, std::unique_ptr<Surface>> m_Surfaces;
        bool m_EnableValidation { false };

    };
}

