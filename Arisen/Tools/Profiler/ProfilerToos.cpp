#include "ProfilerTools.h"
#pragma comment(lib, "TracyClient.lib")
namespace Arisen::Tools::Profiler
{
    inline void SetThreadName(const char* name)
    {
        tracy::SetThreadName(name);
    }

    inline void SetThreadNameHint(const char* name, int32_t groupHint)
    {
        tracy::SetThreadNameWithHint(name, groupHint);
    }

    inline void Message(const char* msg, int32_t callstackDepth)
    {
        tracy::Profiler::Message(msg, strlen(msg), callstackDepth);
    }

    inline void ColoredMessage(const char* msg, uint32_t color, int32_t callstackDepth)
    {
        tracy::Profiler::MessageColor(msg, strlen(msg), color, callstackDepth);
    }

    inline void MarkFiberExit()
    {
        tracy::Profiler::LeaveFiber();
    }

    inline void MarkFiberEnter(const char* fiber, int32_t groupHint )
    {
        tracy::Profiler::EnterFiber(fiber, groupHint);
    }
    
    inline void MarkNamedMemoryFree(const void* ptr, bool secure, const char* name)
    {
        tracy::Profiler::MemFreeNamed(ptr, secure, name);
    }

    inline void MarkNamedMemoryAlloc(const void* ptr, size_t size, bool secure, const char* name)
    {
        tracy::Profiler::MemAllocNamed(ptr, size, secure, name);
    }

    inline void MarkMemoryFree(const void* ptr, bool secure)
    {
        tracy::Profiler::MemFree(ptr, secure);
    }

    inline void MarkMemoryAlloc(const void* ptr, size_t size, bool secure)
    {
        tracy::Profiler::MemAlloc(ptr, size, secure);
    }

    inline void MarkMemoryDiscard(const char* name, bool secure)
    {
        tracy::Profiler::MemDiscard(name, secure);
    }
    
    inline void MarkLock()
    {
       
    }

    inline void MarkZoneScope()
    {
    }

    inline void MarkNamedZoneScope(const char* name)
    {
       
    }

    inline void MarkZoneEnd()
    {
       
    }

    inline void CaptureFrameImage(const void* image, uint16_t w, uint16_t h, uint8_t offset, bool flip)
    {
        FrameImage(image, w, h, offset, flip);
    }

    inline void SetFrameMarkEnd(const char* name)
    {
        FrameMarkEnd(name);
    }

    inline void SetFrameMarkStart(const char* name)
    {
        tracy::Profiler::SendFrameMark(name, tracy::QueueType::FrameMarkMsgStart );
    }

    inline void SetFrameMark()
    {
        FrameMark;
    }
}
