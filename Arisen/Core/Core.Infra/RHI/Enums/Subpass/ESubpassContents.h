#pragma once

namespace ArisenEngine::RHI
{
    typedef enum ESubpassContents
    {
        SUBPASS_CONTENTS_INLINE = 0,
        SUBPASS_CONTENTS_SECONDARY_COMMAND_BUFFERS = 1,
        SUBPASS_CONTENTS_MAX_ENUM = 0x7FFFFFFF
    } ESubpassContents;
}
