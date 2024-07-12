#include "RHIVkInstance.h"
#include <vulkan/vulkan_core.h>

#include "Windows/RenderWindowAPI.h"


NebulaEngine::Containers::Vector<const char*> InstanceExtensionNames
{
    VK_EXT_DEBUG_UTILS_EXTENSION_NAME,
    "VK_KHR_win32_surface",
    "VK_KHR_surface"
};

bool CheckValidationLayerSupport()
{
    uint32_t layerCount;
    vkEnumerateInstanceLayerProperties(&layerCount, nullptr);

    std::vector<VkLayerProperties> availableLayers(layerCount);
    vkEnumerateInstanceLayerProperties(&layerCount, availableLayers.data());

    for (const char* layerName : ValidationLayers)
    {
        bool layerFound = false;

        for (const auto& layerProperties : availableLayers)
        {
            if (strcmp(layerName, layerProperties.layerName) == 0)
            {
                layerFound = true;
                break;
            }
        }

        if (!layerFound)
        {
            LOG_INFO("ValidationLayer not found:" + std::string(layerName));
            return false;
        }
    }

    return true;
}

VKAPI_ATTR VkBool32 VKAPI_CALL DebugCallback(
    VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity,
    VkDebugUtilsMessageTypeFlagsEXT messageType,
    const VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
    void* pUserData)
{
    if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT)
    {
        LOG_ERROR(" ######### vk message error: " + std::string(pCallbackData->pMessage));
    }
    else if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT)
    {
        LOG_WARN(" ######### vk message warning: " + std::string(pCallbackData->pMessage));
    }
    else if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT)
    {
        LOG_INFO(" ######### vk message info: " + std::string(pCallbackData->pMessage));
    }
    else
    {
        LOG_DEBUG(" ######### vk message verbose: " + std::string(pCallbackData->pMessage));
    }

    return VK_FALSE;
}

void PopulateDebugMessengerCreateInfo(VkDebugUtilsMessengerCreateInfoEXT& createInfo)
{
    createInfo = {};
    createInfo.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT;

    createInfo.messageSeverity =
        VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT
        | VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT
        | VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT
        | VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;

    createInfo.messageType =
        VK_DEBUG_UTILS_MESSAGE_TYPE_DEVICE_ADDRESS_BINDING_BIT_EXT
        | VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT
        | VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT
        | VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;

    createInfo.pfnUserCallback = DebugCallback;
}

NebulaEngine::RHI::RHIVkInstance::RHIVkInstance(InstanceInfo&& app_info): Instance(std::move(app_info))
{
    if (app_info.validationLayer && !CheckValidationLayerSupport())
    {
        LOG_FATAL_AND_THROW("validation layers requested, but not available!");
    }

    m_EnableValidation = app_info.validationLayer;
    m_VulkanVersion = { app_info.variant, app_info.major, app_info.minor };

    VkApplicationInfo appInfo{};
    appInfo.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO;
    appInfo.pApplicationName = app_info.name;
    appInfo.applicationVersion =
        VK_MAKE_VERSION(app_info.appMajor, app_info.appMinor, app_info.appPatch);
    appInfo.pEngineName = app_info.engineName;
    appInfo.engineVersion =
        VK_MAKE_VERSION(app_info.engineMajor, app_info.engineMinor, app_info.enginePatch);
    appInfo.apiVersion =
        VK_MAKE_API_VERSION(app_info.variant, app_info.major, app_info.minor, app_info.patch);

    VkInstanceCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
    createInfo.pApplicationInfo = &appInfo;

    VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo{};
    if (app_info.validationLayer)
    {
        createInfo.enabledLayerCount = static_cast<uint32_t>(ValidationLayers.size());
        createInfo.ppEnabledLayerNames = ValidationLayers.data();

        PopulateDebugMessengerCreateInfo(debugCreateInfo);
        createInfo.pNext = (VkDebugUtilsMessengerCreateInfoEXT*)&debugCreateInfo;
    }
    else
    {
        createInfo.enabledLayerCount = 0;
        createInfo.pNext = nullptr;
    }

    // shows all supported extensions
    uint32_t extensionCount = 0;
    vkEnumerateInstanceExtensionProperties(nullptr, &extensionCount, nullptr);
    std::vector<VkExtensionProperties> extensions(extensionCount);
    vkEnumerateInstanceExtensionProperties(nullptr, &extensionCount, extensions.data());

#if _DEBUG

    LOG_DEBUG("available extensions:");
    for (const auto& extension : extensions)
    {
        LOG_DEBUG(extension.extensionName);
    }

#endif


    // Extensions Slot 
    createInfo.enabledExtensionCount = static_cast<uint32_t>(InstanceExtensionNames.size());
    createInfo.ppEnabledExtensionNames = InstanceExtensionNames.data();

    if (vkCreateInstance(&createInfo, nullptr, &m_VkInstance) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create instance!");
    }

    SetupDebugMessager();

    CreateDevice();
}

VkResult CreateDebugUtilsMessengerEXT(
    VkInstance instance,
    const VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo,
    const VkAllocationCallbacks* pAllocator,
    VkDebugUtilsMessengerEXT* pDebugMessenger)
{
    auto func = (PFN_vkCreateDebugUtilsMessengerEXT)
        vkGetInstanceProcAddr(instance, "vkCreateDebugUtilsMessengerEXT");

    if (func != nullptr)
    {
        return func(instance, pCreateInfo, pAllocator, pDebugMessenger);
    }
    else
    {
        return VK_ERROR_EXTENSION_NOT_PRESENT;
    }
}

void NebulaEngine::RHI::RHIVkInstance::CreateDevice()
{
    m_Device = new RHIVkDevice(this);
}

void NebulaEngine::RHI::RHIVkInstance::SetupDebugMessager()
{
    if (!m_EnableValidation)
    {
        return;
    }
    
    VkDebugUtilsMessengerCreateInfoEXT createInfo;
    PopulateDebugMessengerCreateInfo(createInfo);
    
    if (CreateDebugUtilsMessengerEXT(m_VkInstance, &createInfo, nullptr, &m_VkDebugMessenger) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to set up debug messenger!");
    }
}

void DestroyDebugUtilsMessengerEXT(
    VkInstance instance,
    VkDebugUtilsMessengerEXT debugMessenger,
    const VkAllocationCallbacks* pAllocator)
{
    auto func = (PFN_vkDestroyDebugUtilsMessengerEXT)
        vkGetInstanceProcAddr(instance, "vkDestroyDebugUtilsMessengerEXT");

    if (func != nullptr)
    {
        func(instance, debugMessenger, pAllocator);
    }
}

void NebulaEngine::RHI::RHIVkInstance::DisposeDebugMessager()
{
    if (!m_EnableValidation)
    {
        return;
    }

    DestroyDebugUtilsMessengerEXT(m_VkInstance, m_VkDebugMessenger, nullptr);
}

void NebulaEngine::RHI::RHIVkInstance::CreateSurface(u32&& windowId)
{
    m_Device->CreateSurface(std::move(windowId));
}

void NebulaEngine::RHI::RHIVkInstance::DestroySurface(u32&& windowId)
{
    m_Device->DestroySurface(std::move(windowId));
}

const NebulaEngine::RHI::Surface& NebulaEngine::RHI::RHIVkInstance::GetSurface(u32&& windowId)
{
    return m_Device->GetSurface(std::move(windowId));
}

bool NebulaEngine::RHI::RHIVkInstance::IsSupportLinearColorSpace(u32&& windowId)
{
   
    auto& supportDetail = m_Device->GetSwapChainSupportDetails(std::move(windowId));

    for (const auto& availableFormat : supportDetail.formats)
    {
        if (availableFormat.format == VK_FORMAT_B8G8R8A8_SRGB && availableFormat.colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR)
        {
            return true;
        }
    }
    
    return false;
}

bool NebulaEngine::RHI::RHIVkInstance::PresentModeSupported(u32&& windowId, PresentMode mode)
{
    auto& supportDetail = m_Device->GetSwapChainSupportDetails(std::move(windowId));
    for (const auto& presentMode : supportDetail.presentModes)
    {
        if (presentMode == mode)
        {
            return true;
        }
    }

    return false;
}

void NebulaEngine::RHI::RHIVkInstance::SetCurrentPresentMode(u32&& windowId, PresentMode mode)
{
    
}

void NebulaEngine::RHI::RHIVkInstance::SetResolution(const u32&& windowId, const u32&& width, const u32&& height)
{
    m_Device->SetResolution(std::move(windowId), std::move(width), std::move(height));
}

const NebulaEngine::RHI::Format NebulaEngine::RHI::RHIVkInstance::GetSuitableSwapChainFormat(u32&& windowId)
{
    return FORMAT_R8G8B8_SRGB;
}

const NebulaEngine::RHI::PresentMode NebulaEngine::RHI::RHIVkInstance::GetSuitablePresentMode(u32&& windowId)
{
    return PRESENT_MODE_FIFO;
}

void NebulaEngine::RHI::RHIVkInstance::CheckSwapChainCapabilities()
{
    m_Device->CheckSwapChainCapabilities();
}

NebulaEngine::RHI::Instance* CreateInstance(NebulaEngine::RHI::InstanceInfo&& app_info)
{
    return new NebulaEngine::RHI::RHIVkInstance(std::move(app_info));
}

NebulaEngine::RHI::RHIVkInstance::~RHIVkInstance() noexcept
{
    
    DisposeDebugMessager();
    delete m_Device;
    
    vkDestroyInstance(m_VkInstance, nullptr);
    LOG_INFO(" ~RHIVkInstance ");
}

void NebulaEngine::RHI::RHIVkInstance::InitLogicDevices()
{
    if (!m_Device->IsPhysicalDeviceAvailable())
    {
        LOG_FATAL_AND_THROW("Should pick a physical device first before init logical devices");
    }

    if (!m_Device->IsSurfaceAvailable())
    {
        LOG_FATAL_AND_THROW("Should create all the surfaces first before init logical devices");
    }
    
    m_Device->InitLogicDevices();
    
    LOG_INFO(" Logical Devices Init! ");
}

void NebulaEngine::RHI::RHIVkInstance::PickPhysicalDevice(bool considerSurface)
{
    if (!m_Device->IsSurfaceAvailable())
    {
        LOG_FATAL_AND_THROW("Should create all the surfaces first before pick physical devices");
    }

    // TODO: pick device by surface ?
    m_Device->PickPhysicalDevice();
    // TODO: configurable physical device
    // TODO: if current physical device not adequate suitable swap chain, should repick one
    CheckSwapChainCapabilities();
}

void NebulaEngine::RHI::RHIVkInstance::InitDefaultSwapChains()
{
    if (!m_Device->IsSurfaceAvailable())
    {
        LOG_FATAL_AND_THROW("Should create all the surfaces first before init swap chain.");
    }

    if (!m_Device->IsPhysicalDeviceAvailable())
    {
        LOG_FATAL_AND_THROW("Should pick a physical device first before init swap chain.");
    }

    m_Device->InitDefaultSwapChains();

    LOG_INFO(" SwapChains Init! ");
}


