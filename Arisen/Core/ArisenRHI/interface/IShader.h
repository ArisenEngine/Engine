#pragma once
#include "IObject.h"
#include "IProvider.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE

enum class ShaderType : uint32_t
{
    Vertex = 0,
    Pixel,
    Compute,
    All
};

struct ShaderEntryFunction
{
    std::string file_name;
    std::string function_name;
};

struct ShaderSettings
{
    IProvider& data_provider;
    ShaderEntryFunction entry_function;
};

struct IShader : IObject
{
    virtual ShaderType GetType() const = 0;
};
ARISENRHI_END_NAMESPACE
