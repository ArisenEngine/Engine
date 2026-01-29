#pragma once
namespace ArisenEngine::RHI
{
    typedef enum EAttachmentLoadOp {
        ATTACHMENT_LOAD_OP_LOAD = 0,
        ATTACHMENT_LOAD_OP_CLEAR = 1,
        ATTACHMENT_LOAD_OP_DONT_CARE = 2,
        ATTACHMENT_LOAD_OP_NONE_EXT = 1000400000,
        ATTACHMENT_LOAD_OP_MAX_ENUM = 0x7FFFFFFF
    } EAttachmentLoadOp;
}
