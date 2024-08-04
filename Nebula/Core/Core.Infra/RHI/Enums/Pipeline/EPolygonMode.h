#pragma once

namespace NebulaEngine::RHI
{
    typedef enum EPolygonMode {
        EPOLYGON_MODE_FILL = 0,
        EPOLYGON_MODE_LINE = 1,
        EPOLYGON_MODE_POINT = 2,
        EPOLYGON_MODE_FILL_RECTANGLE_NV = 1000153000,
        EPOLYGON_MODE_MAX_ENUM = 0x7FFFFFFF
    } EPolygonMode;
}
