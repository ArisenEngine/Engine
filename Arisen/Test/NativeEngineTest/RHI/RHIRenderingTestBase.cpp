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
        m_CmdPool = RHI_Device_CreateCommandBufferPool(m_Device);
        m_DescriptorPool = RHI_Device_GetDescriptorPool(m_Device);

        m_Surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
        m_SwapChain = RHI_Surface_GetSwapChain(m_Surface);

        for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
        {
            m_FrameTickets.emplace_back(0);
        }
    }

    std::wstring RHIRenderingTestBase::GetShaderEnvString()
    {
        std::wstring envStr;
        unsigned int len = RHI_Instance_GetEnvStringW(this->m_Instance, nullptr, 0);
        if (len > 0)
        {
            envStr.resize(len ? (len - 1) : 0);
            if (len > 1)
            {
                RHI_Instance_GetEnvStringW(this->m_Instance, envStr.data(), len);
            }
        }
        return envStr;
    }

    void RHIRenderingTestBase::InitShaderProgram(const std::wstring& shaderName)
    {
        std::wstring envStr = GetShaderEnvString();
        
        namespace fs = std::filesystem;
        wchar_t exePathW[MAX_PATH]{};
        GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
        auto exeDir = fs::path(exePathW).parent_path();
        auto currentPath = exeDir.generic_wstring() + L"\\Shader";
        auto path = currentPath + L"\\" + shaderName + L".hlsl";

        // Vertex Shader
        HAL::ShaderCompileParams vertexParams
        {
            path, L"Vert", L"6_0", L"-spirv", envStr, L"0", RHI::EProgramStage::Vertex,
            {}, {}, currentPath + L"\\" + shaderName + L".vert.spirv", true
        };

        HAL::ShaderCompilerOutput outputVertex;
        if (!HAL::CompileShaderFromFile(std::move(vertexParams), outputVertex) || outputVertex.codePointer == nullptr || outputVertex.codeSize == 0)
        {
            LOG_ERROR("Vertex shader compilation failed.");
            throw std::exception("Vertex shader compilation failed.");
        }

        m_VertProgram = RHI_Device_CreateGPUProgram(m_Device);
        {
            std::string nameStr = String::WStringToString(path);
            RHI::RHIShaderProgramDesc desc = { outputVertex.codeSize, outputVertex.codePointer, "Vert", nameStr.c_str(), RHI::SHADER_STAGE_VERTEX_BIT };
            RHI_Device_AttachProgramByteCode(m_Device, m_VertProgram, &desc);
        }
        if (outputVertex.codePointer) std::free(outputVertex.codePointer);

        // Fragment Shader
        HAL::ShaderCompileParams fragmentParams
        {
            path, L"Frag", L"6_0", L"-spirv", envStr, L"0", RHI::EProgramStage::Fragment,
            {}, {}, currentPath + L"\\" + shaderName + L".frag.spirv", true
        };

        HAL::ShaderCompilerOutput outputFragment;
        if (!HAL::CompileShaderFromFile(std::move(fragmentParams), outputFragment) || outputFragment.codePointer == nullptr || outputFragment.codeSize == 0)
        {
            LOG_ERROR("Fragment shader compilation failed.");
            throw std::exception("Fragment shader compilation failed.");
        }

        m_FragProgram = RHI_Device_CreateGPUProgram(m_Device);
        {
            std::string nameStr = String::WStringToString(path);
            RHI::RHIShaderProgramDesc desc = { outputFragment.codeSize, outputFragment.codePointer, "Frag", nameStr.c_str(), RHI::SHADER_STAGE_FRAGMENT_BIT };
            RHI_Device_AttachProgramByteCode(m_Device, m_FragProgram, &desc);
        }
        if (outputFragment.codePointer) std::free(outputFragment.codePointer);
    }

    void RHIRenderingTestBase::UploadImage(RHI_ImageHandle textureHandle, UInt64 imageSize, void* data, UInt32 texWidth, UInt32 texHeight, RHI::EImageLayout finalLayout)
    {
        RHI::RHIBufferDescriptor tsb{
            0, imageSize, RHI::BUFFER_USAGE_TRANSFER_SRC_BIT, RHI::SHARING_MODE_EXCLUSIVE,
            0, nullptr, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
        };
        auto stagingBuffer = RHI_Device_CreateBuffer(m_Device, &tsb, "Texture Staging Buffer");
        RHI_Buffer_MemoryCopy(m_Device, stagingBuffer, data, imageSize, 0);

        auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, 0);
        RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
        
        {
            RHI::RHIImageMemoryBarrier barrier{};
            barrier.srcAccess = RHI::ACCESS_NONE;
            barrier.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
            barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
            barrier.newLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
            barrier.srcQueueFamilyIndex = ~0U;
            barrier.dstQueueFamilyIndex = ~0U;
            barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&textureHandle);
            barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
            barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
            barrier.dstStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;

            Containers::Vector<RHI::RHIImageMemoryBarrier> barriers { barrier };
            RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0, &barriers);
        }

        {
            Containers::Vector<RHI::RHIBufferImageCopy> regions{
                { 0, 0, 0, { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }, 0, 0, 0, texWidth, texHeight, 1 }
            };
            RHI_Cmd_CopyBufferToImage(cmd, stagingBuffer, textureHandle, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &regions);
        }

        {
            RHI::RHIImageMemoryBarrier barrier{};
            barrier.srcAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
            barrier.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
            barrier.newLayout = static_cast<RHI::EImageLayout>(finalLayout);
            barrier.srcQueueFamilyIndex = ~0U;
            barrier.dstQueueFamilyIndex = ~0U;
            barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&textureHandle);
            barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
            barrier.srcStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;
            
            // Map layout to appropriate stage
            if (finalLayout == RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL) {
                barrier.dstStageMask = RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
                barrier.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
            } else if (finalLayout == RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL) {
                barrier.dstStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;
                barrier.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
            } else {
                barrier.dstStageMask = RHI::PIPELINE_STAGE_ALL_COMMANDS_BIT;
                barrier.dstAccess = static_cast<RHI::EAccessFlag>(RHI::ACCESS_MEMORY_READ_BIT | RHI::ACCESS_MEMORY_WRITE_BIT);
            }

            Containers::Vector<RHI::RHIImageMemoryBarrier> barriers { barrier };
            RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TRANSFER_BIT, barrier.dstStageMask, 0, &barriers);
        }

        RHI_Cmd_End(cmd);
        RHI_Device_Submit(m_Device, cmd, 0);
        RHI_Device_WaitIdle(m_Device);

        RHI_Device_ReleaseBuffer(m_Device, stagingBuffer);
        RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, 0, cmd);
    }

    void RHIRenderingTestBase::TeardownCommonResources()
    {
        if (m_Device)
        {
            RHI_Device_WaitIdle(m_Device);

            m_Model.Release(m_Device);
            
            if (m_VertProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_VertProgram);
            if (m_FragProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_FragProgram);
            
            if (m_CmdPool) RHI_Device_ReleaseCommandBufferPool(m_Device, m_CmdPool);
            
            m_VertProgram = 0;
            m_FragProgram = 0;
            m_CmdPool = 0;
            m_Surface = 0;
            m_SwapChain = 0;
        }
    }
}
