#include "RHIVkInstance.h"
#include <vulkan/vulkan_core.h>

NebulaEngine::Containers::vector<const char*> InstanceExtensionNames
{
    VK_EXT_DEBUG_UTILS_EXTENSION_NAME,
    "VK_KHR_win32_surface",
    "VK_KHR_surface"
};

NebulaEngine::Containers::vector<const char*> DeviceExtensionNames
{
    VK_KHR_SWAPCHAIN_EXTENSION_NAME
};

NebulaEngine::Containers::vector<const char*> ValidationLayers
{
    "VK_LAYER_KHRONOS_validation"
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

const int MAX_FRAMES_IN_FLIGHT = 2;

NebulaEngine::RHI::RHIVkInstance::RHIVkInstance(AppInfo&& app_info): Instance(std::move(app_info))
{
    if (app_info.validationLayer && !CheckValidationLayerSupport())
    {
        LOG_FATAL("validation layers requested, but not available!");
        throw std::runtime_error("validation layers requested, but not available!");
    }

    m_EnableValidation = app_info.validationLayer;

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

    if (vkCreateInstance(&createInfo, nullptr, &m_Instance) != VK_SUCCESS)
    {
        LOG_FATAL("failed to create instance!");
        throw std::runtime_error("failed to create instance!");
    }

    SetupDebugMessager();
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

void NebulaEngine::RHI::RHIVkInstance::SetupDebugMessager()
{
    if (!m_EnableValidation)
    {
        return;
    }
    
    VkDebugUtilsMessengerCreateInfoEXT createInfo;
    PopulateDebugMessengerCreateInfo(createInfo);
    
    if (CreateDebugUtilsMessengerEXT(m_Instance, &createInfo, nullptr, &m_DebugMessenger) != VK_SUCCESS)
    {
        throw std::runtime_error("failed to set up debug messenger!");
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

    DestroyDebugUtilsMessengerEXT(m_Instance, m_DebugMessenger, nullptr);
}

NebulaEngine::RHI::Instance* CreateInstance(NebulaEngine::RHI::AppInfo&& app_info)
{
    return new NebulaEngine::RHI::RHIVkInstance(std::move(app_info));
}

NebulaEngine::RHI::RHIVkInstance::~RHIVkInstance() noexcept
{
    LOG_INFO(" ~RHIVkInstance ");

    DisposeDebugMessager();

    vkDestroyInstance(m_Instance, nullptr);
}