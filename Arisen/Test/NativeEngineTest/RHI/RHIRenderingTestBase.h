#pragma once

#include "RHITestBase.h"
#include <chrono>
#include <iostream>
#include <filesystem>
#include <string>

// RHI Includes
#include "RHI/Enums/Pipeline/EAccessFlag.h"
#include "RHI/Enums/Buffer/EBufferUsage.h"
#include "RHI/Enums/Pipeline/EColorComponentFlag.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Attachment/EAttachmentLoadOp.h"
#include "RHI/Enums/Attachment/EAttachmentStoreOp.h"
#include "RHI/Enums/Image/EImageAspectFlagBits.h"
#include "RHI/Enums/Subpass/ESubpassContents.h"
#include "RHI/Presentation/RHISurface.h"
#include "RHI/RenderPass/RHIFrameBuffer.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Core/RHICommon.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "RHI/RenderPass/RHISubPass.h"

// Engine Exports
#include "../../../Engine/NativeEngine/RHI/RHIExports.h"
#include "../../../Engine/NativeEngine/RHI/InstanceExports.h"
#include "../../../Engine/NativeEngine/RHI/DeviceExports.h"
#include "../../../Engine/NativeEngine/RHI/SurfaceExports.h"
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../../Engine/NativeEngine/RHI/PipelineExports.h"
#include "../../../Engine/NativeEngine/RHI/DescriptorExports.h"
#include "../../../Engine/NativeEngine/RHI/SyncExports.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"

// Third Party
#define GLM_FORCE_RADIANS
#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <cstdlib>
#include "stb_image.h"

namespace ArisenEngine::Testing
{
    class RHIRenderingTestBase : public RHITestBase
    {
    protected:
        RHI_CommandBufferPoolHandle m_CmdPool = 0;
        RHI_RenderPassHandle m_RenderPass = 0;
        RHI_FrameBufferHandle m_FrameBuffer = 0;
        RHI_DescriptorPoolHandle m_DescriptorPool = 0;
        
        RHI_GPUProgramHandle m_VertProgram = 0;
        RHI_GPUProgramHandle m_FragProgram = 0;
        
        Containers::Vector<UInt32> m_DescriptorPoolIds;
        Containers::Vector<UInt64> m_FrameTickets;
        
        GLTFModel m_Model;

    public:
        virtual ~RHIRenderingTestBase() = default;

        bool SetupTest() override;
        void TeardownTest() override;

    protected:
        void InitCommonResources();
        void InitShaderProgram(const std::wstring& shaderName);
        void TeardownCommonResources();
        
        void UploadImage(RHI_ImageHandle textureHandle, UInt64 imageSize, void* data, UInt32 texWidth, UInt32 texHeight);
        
        // Helper to get shader environment string
        std::wstring GetShaderEnvString();
    };
}
