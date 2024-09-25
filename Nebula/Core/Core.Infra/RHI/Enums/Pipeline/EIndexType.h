#pragma once
namespace NebulaEngine::RHI
{
    typedef enum EIndexType {
        INDEX_TYPE_UINT16 = 0,
        INDEX_TYPE_UINT32 = 1,
        INDEX_TYPE_NONE_KHR = 1000165000,
        INDEX_TYPE_UINT8_KHR = 1000265000,
        INDEX_TYPE_NONE_NV = INDEX_TYPE_NONE_KHR,
        INDEX_TYPE_UINT8_EXT = INDEX_TYPE_UINT8_KHR,
        INDEX_TYPE_MAX_ENUM = 0x7FFFFFFF
    } EIndexType;
    
}
