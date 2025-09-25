#pragma once
#include "CoreMinimalRHI.h"
#include "IObject.h"
#include "ICommandKit.h"
#include "ICommandList.h"
#include "IDescriptorManager.h"
#include "IDevice.h"
#include "IProgram.h"
#include "ITexture.h"

ARISENRHI_BEGIN_NAMEPSACE
enum class ContextType
{
    Render,
    Compute,
};

enum class ContextOption
{
    DefaultProgramBindingsInitialization,
    TransferWithD3D12DirectQueue,
};

struct IRHIContext : IObject
{
    virtual Ptr<ICommandKit> CreateCommandKit(CommandListType type) const = 0;
    virtual Ptr<ICommandQueue> CreateCommandQueue(CommandListType type) const = 0;
    [[nodiscard]] virtual Ptr<ITexture> CreateTexture(const TextureSettings& settings) const = 0;
    [[nodiscard]] virtual Ptr<IProgram> CreateProgram(const ProgramSettings& settings) const = 0;
    
    virtual ICommandKit& GetDefaultCommandKit(CommandListType type) const = 0;
    virtual const IDevice& GetDevice() const = 0;
    virtual ContextOption GetOptions() const noexcept =0;

    virtual IDescriptorManager& GetDescriptorManager() = 0;
};
ARISENRHI_END_NAMESPACE
