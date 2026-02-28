#pragma once

#include "RHITestBase.h"
#include <chrono>
#include <iostream>
#include <filesystem>
#include "Base/FoundationMinimal.h"

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

#include "RHI/Descriptors/RHIDescriptorPool.h"
#include "RHI/Presentation/RHISwapChain.h"
#include "RHI/Core/RHIInstance.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"

#include "ShaderCompiler/ShaderCompilerAPI.h"

// Third Party
#define GLM_FORCE_RADIANS
#define GLM_FORCE_DEPTH_ZERO_TO_ONE
#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <cstdlib>
#include "stb_image.h"

namespace ArisenEngine::Testing
{
    class RHIRenderingTestBase : public RHITestBase
    {
    protected:
        RHI::RHICommandBufferPoolHandle m_CmdPool;
        RHI::RHIDescriptorPool* m_DescriptorPool = nullptr;
        RHI::RHIDescriptorPoolHandle m_DescriptorPoolHandle;

        RHI::RHISurface* m_Surface = nullptr;
        RHI::RHISwapChain* m_SwapChain = nullptr;

        RHI::RHIShaderProgramHandle m_VertProgram;
        RHI::RHIShaderProgramHandle m_FragProgram;

        Containers::Vector<UInt32> m_DescriptorPoolIds;
        Containers::Vector<RHI::RHIGpuTicket> m_FrameTickets;

        GLTFModel m_Model;

    public:
        virtual ~RHIRenderingTestBase() = default;

        bool SetupTest() override;
        void TeardownTest() override;

    protected:
        void InitCommonResources();
        void InitShaderProgram(const String& shaderName);
        void TeardownCommonResources();

        void UploadImage(RHI::RHIImageHandle textureHandle, UInt64 imageSize, void* data, UInt32 texWidth,
                         UInt32 texHeight, RHI::EImageLayout finalLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

        // Helper to get shader environment string
        String GetShaderEnvString();
    };
}
