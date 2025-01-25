#pragma once

namespace ArisenEngine::Job
{
    typedef enum EJobType
    {
        MainThreadJob = 0,
        CommonJob = 1,
        RenderingJob = 2,
        ComputeJob = 3,
        IOJob = 4,
        DebugJob = 5,
        
    } EJobType;
}
