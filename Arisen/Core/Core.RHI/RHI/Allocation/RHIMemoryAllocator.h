#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHI_DLL RHIMemoryAllocator
    {
    public:
        RHIMemoryAllocator();
        virtual ~RHIMemoryAllocator() noexcept;
        NO_COPY_NO_MOVE(RHIMemoryAllocator)

        // TODO(CppSharp-P0): GetHandle() \u8fd4\u56de void*\uff0c\u6cc4\u6f0f VmaAllocator\u3002\r\n        // \u6b64\u7c7b\u51e0\u4e4e\u4e3a\u7a7a\uff0c\u8003\u8651\u6dfb\u52a0\u6709\u610f\u4e49\u7684\u62bd\u8c61\u65b9\u6cd5 (GetMemoryBudget, GetStats)\r\n        // \u6216\u5c06\u5176\u5b8c\u5168\u9690\u85cf\u5728\u540e\u7aef\uff0c\u4e0d\u5bfc\u51fa\u5230 C#\u3002\r\n        virtual void* GetHandle() const = 0;
    };
}

