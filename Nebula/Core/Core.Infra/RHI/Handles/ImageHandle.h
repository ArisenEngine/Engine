#pragma once
#include "../../Common/CommandHeaders.h"
#include "MemoryHandle.h"

namespace NebulaEngine::RHI
{
    class ImageHandle : public MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE(ImageHandle)
        ImageHandle() = default;
        ~ImageHandle() noexcept override = default;
    };
}
