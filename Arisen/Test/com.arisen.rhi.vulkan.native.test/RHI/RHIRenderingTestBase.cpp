#include "RHIRenderingTestBase.h"
#include <filesystem>

namespace ArisenEngine::Testing
{
    bool RHIRenderingTestBase::SetupTest()
    {
        HAL::InitDXC();
        return true;
    }

    void RHIRenderingTestBase::TeardownTest()
    {
        TeardownCommonResources();
    }

    void RHIRenderingTestBase::InitCommonResources()
    {
        m_CmdPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
        m_DescriptorPool = m_Device->GetDescriptorPool();
        m_DescriptorPoolHandle = m_Device->GetDescriptorPoolHandle();

        m_Surface = &m_Instance->GetSurface(m_WindowId);
        m_SwapChain = m_Surface->GetSwapChain();

        for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
        {
            m_FrameTickets.emplace_back(0);
        }
    }

    String RHIRenderingTestBase::GetShaderEnvString()
    {
        return m_Instance->GetEnvString();
    }

    void RHIRenderingTestBase::InitShaderProgram(const String& shaderName)
    {
        String envStr = GetShaderEnvString();

        namespace fs = std::filesystem;
        wchar_t exePathW[MAX_PATH]{};
        GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
        auto exeDir = fs::path(exePathW).parent_path();
        String currentPath = exeDir.generic_wstring().c_str();
        currentPath += "\\Shader";
        String path = currentPath + "\\" + shaderName + ".hlsl";

        // Vertex Shader
        HAL::ShaderCompileParams vertexParams
        {
            path, L"Vert", L"6_0", L"-spirv", envStr.ToWString(), L"0", RHI::EProgramStage::Vertex,
            {}, {}, (currentPath + "\\" + shaderName + ".vert.spirv").ToWString(), true
        };

        HAL::ShaderCompilerOutput outputVertex;
        if (!HAL::CompileShaderFromFile(std::move(vertexParams), outputVertex) || outputVertex.codePointer == nullptr ||
            outputVertex.codeSize == 0)
        {
            LOG_ERROR("Vertex shader compilation failed.");
            throw std::exception("Vertex shader compilation failed.");
        }

        m_VertProgram = m_Device->GetFactory()->CreateGPUProgram();
        {
            RHI::RHIShaderProgramDesc desc = {
                (UInt32)outputVertex.codeSize, outputVertex.codePointer, "Vert", path.c_str(),
                RHI::SHADER_STAGE_VERTEX_BIT
            };
            m_Device->GetFactory()->AttachProgramByteCode(m_VertProgram, std::move(desc));
        }
        if (outputVertex.codePointer) std::free(outputVertex.codePointer);

        // Fragment Shader
        HAL::ShaderCompileParams fragmentParams
        {
            path, L"Frag", L"6_0", L"-spirv", envStr.ToWString(), L"0", RHI::EProgramStage::Fragment,
            {}, {}, (currentPath + "\\" + shaderName + ".frag.spirv").ToWString(), true
        };

        HAL::ShaderCompilerOutput outputFragment;
        if (!HAL::CompileShaderFromFile(std::move(fragmentParams), outputFragment) || outputFragment.codePointer ==
            nullptr || outputFragment.codeSize == 0)
        {
            LOG_ERROR("Fragment shader compilation failed.");
            throw std::exception("Fragment shader compilation failed.");
        }

        m_FragProgram = m_Device->GetFactory()->CreateGPUProgram();
        {
            RHI::RHIShaderProgramDesc desc = {
                (UInt32)outputFragment.codeSize, outputFragment.codePointer, "Frag", path.c_str(),
                RHI::SHADER_STAGE_FRAGMENT_BIT
            };
            m_Device->GetFactory()->AttachProgramByteCode(m_FragProgram, std::move(desc));
        }
        if (outputFragment.codePointer) std::free(outputFragment.codePointer);
    }

    void RHIRenderingTestBase::UploadImage(RHI::RHIImageHandle textureHandle, UInt64 imageSize, void* data,
                                           UInt32 texWidth, UInt32 texHeight, RHI::EImageLayout finalLayout)
    {
        RHI::RHIBufferDescriptor tsb{
            0, imageSize, RHI::BUFFER_USAGE_TRANSFER_SRC_BIT, RHI::SHARING_MODE_EXCLUSIVE,
            0, nullptr, RHI::ERHIMemoryUsage::Upload
        };
        auto stagingBuffer = m_Device->GetFactory()->CreateBuffer(std::move(tsb), "Texture Staging Buffer");
        m_Device->GetFactory()->BufferMemoryCopy(stagingBuffer, data, imageSize, 0);

        auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
        // Assuming 0 is a valid index for a one-time command buffer
        UInt32 currentIndex = 0;
        auto cmdHandle = pool->GetCommandBuffer(currentIndex);
        auto cmd = m_Device->GetCommandBuffer(cmdHandle);

        // Begin
        cmd->Begin(currentIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

        {
            RHI::RHIImageMemoryBarrier barrier{};
            barrier.srcAccess = RHI::ACCESS_NONE;
            barrier.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
            barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
            barrier.newLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
            barrier.image = textureHandle;
            barrier.subresourceRange = {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1};
            barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
            barrier.dstStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;

            Containers::Vector<RHI::RHIImageMemoryBarrier> barriers{barrier};
            cmd->PipelineBarrier(RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0, {},
                                 std::move(barriers), {});
        }

        {
            Containers::Vector<RHI::RHIBufferImageCopy> regions{
                {0, 0, 0, {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1}, 0, 0, 0, texWidth, texHeight, 1}
            };
            cmd->CopyBufferToImage(stagingBuffer, textureHandle, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                                   std::move(regions));
        }

        {
            RHI::RHIImageMemoryBarrier barrier{};
            barrier.srcAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
            barrier.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
            barrier.newLayout = finalLayout;
            barrier.image = textureHandle;
            barrier.subresourceRange = {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1};
            barrier.srcStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;

            // Map layout to appropriate stage
            if (finalLayout == RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL)
            {
                barrier.dstStageMask = RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
                barrier.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
            }
            else if (finalLayout == RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL)
            {
                barrier.dstStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;
                barrier.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
            }
            else
            {
                barrier.dstStageMask = RHI::PIPELINE_STAGE_ALL_COMMANDS_BIT;
                barrier.dstAccess = static_cast<RHI::EAccessFlag>(RHI::ACCESS_MEMORY_READ_BIT |
                    RHI::ACCESS_MEMORY_WRITE_BIT);
            }

            Containers::Vector<RHI::RHIImageMemoryBarrier> barriers{barrier};
            cmd->PipelineBarrier(RHI::PIPELINE_STAGE_TRANSFER_BIT, barrier.dstStageMask, 0, {}, std::move(barriers),
                                 {});
        }

        cmd->End();
        m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle);
        m_Device->DeviceWaitIdle();

        m_Device->GetFactory()->ReleaseBuffer(stagingBuffer);
        m_Device->GetCommandBufferPool(m_CmdPool)->ReleaseCommandBuffer(0, cmdHandle);
    }

    void RHIRenderingTestBase::TeardownCommonResources()
    {
        if (m_Device)
        {
            m_Device->DeviceWaitIdle();

            m_Model.Release(m_Device);

            if (m_VertProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_VertProgram);
            if (m_FragProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_FragProgram);

            if (m_CmdPool.IsValid()) m_Device->GetFactory()->ReleaseCommandBufferPool(m_CmdPool);

            m_VertProgram = {};
            m_FragProgram = {};
            m_CmdPool = {};
            m_Surface = nullptr;
            m_SwapChain = nullptr;
        }
    }
}
