#include "RHIVkInstance.h"
#include <vulkan/vulkan_core.h>

#include "Program/RHIVkGPUProgram.h"
#include "Windows/RenderWindowAPI.h"

bool CheckDeviceExtensionSupport(VkPhysicalDevice device)
{
    uint32_t extensionCount;
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, nullptr);

    NebulaEngine::Containers::Vector<VkExtensionProperties> availableExtensions(extensionCount);
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, availableExtensions.data());

    NebulaEngine::Containers::Set<std::string> requiredExtensions(NebulaEngine::RHI::VkDeviceExtensionNames.begin(),
        NebulaEngine::RHI::VkDeviceExtensionNames.end());

    for (const auto& extension : availableExtensions)
    {
        requiredExtensions.erase(extension.extensionName);
    }
    
    return requiredExtensions.empty();
}

int RateDeviceSuitability(VkPhysicalDevice device) {
    
    VkPhysicalDeviceProperties deviceProperties;
    VkPhysicalDeviceFeatures deviceFeatures;
    vkGetPhysicalDeviceProperties(device, &deviceProperties);
    vkGetPhysicalDeviceFeatures(device, &deviceFeatures);
    
    int score = 0;

    // Discrete GPUs have a significant performance advantage
    if (deviceProperties.deviceType == VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU) {
        score += 1000;
    }

    // Maximum possible size of textures affects graphics quality
    score += deviceProperties.limits.maxImageDimension2D;

    // Application can't function without geometry shaders
    if (!deviceFeatures.geometryShader)
    {
        return 0;
    }

    bool extensionsSupported = CheckDeviceExtensionSupport(device);
    if (!extensionsSupported)
    {
        return 0;
    }
    
    return score;
}

bool CheckValidationLayerSupport()
{
    uint32_t layerCount;
    vkEnumerateInstanceLayerProperties(&layerCount, nullptr);

    std::vector<VkLayerProperties> availableLayers(layerCount);
    vkEnumerateInstanceLayerProperties(&layerCount, availableLayers.data());

    for (const char* layerName : NebulaEngine::RHI::VkValidationLayers)
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
            LOG_INFO("[RHIVkInstance::CheckValidationLayerSupport]: ValidationLayer not found:" + std::string(layerName));
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
        LOG_FATAL_AND_THROW("[RHIVkInstance::RHIVkInstance]: validation layers requested, but not available!");
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
        createInfo.enabledLayerCount = static_cast<uint32_t>(VkValidationLayers.size());
        createInfo.ppEnabledLayerNames = VkValidationLayers.data();

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

    LOG_DEBUG("[RHIVkInstance::RHIVkInstance]: available extensions:");
    for (const auto& extension : extensions)
    {
        LOG_DEBUG(extension.extensionName);
    }

#endif


    // Extensions Slot 
    createInfo.enabledExtensionCount = static_cast<uint32_t>(VkInstanceExtensionNames.size());
    createInfo.ppEnabledExtensionNames = VkInstanceExtensionNames.data();

    if (vkCreateInstance(&createInfo, nullptr, &m_VkInstance) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::RHIVkInstance]: failed to create instance!");
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


NebulaEngine::RHI::VkQueueFamilyIndices NebulaEngine::RHI::RHIVkInstance::FindQueueFamilies(VkSurfaceKHR surface)
{

    if (m_CurrentPhysicsDevice == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::FindQueueFamilies]: Physical device invalid!")
    }

    NebulaEngine::RHI::VkQueueFamilyIndices indices;
    
    uint32_t queueFamilyCount = 0;
    vkGetPhysicalDeviceQueueFamilyProperties(m_CurrentPhysicsDevice,
        &queueFamilyCount, nullptr);

    NebulaEngine::Containers::Vector<VkQueueFamilyProperties> queueFamilies(queueFamilyCount);
    vkGetPhysicalDeviceQueueFamilyProperties(m_CurrentPhysicsDevice, &queueFamilyCount,
        queueFamilies.data());

    int i = 0;
    for (const auto& queueFamily : queueFamilies)
    {

        if (indices.IsComplete())
        {
            break;
        }
        
        if (queueFamily.queueFlags & VK_QUEUE_GRAPHICS_BIT)
        {
            indices.graphicsFamily = i;
        }

        VkBool32 presentSupport = false;
        vkGetPhysicalDeviceSurfaceSupportKHR(m_CurrentPhysicsDevice, i, surface, &presentSupport);

        if (presentSupport)
        {
            indices.presentFamily = i;
        }
        
        ++i;
    }

    return indices;
}

const NebulaEngine::RHI::VkSwapChainSupportDetail NebulaEngine::RHI::RHIVkInstance::
GetSwapChainSupportDetails(u32&& windowId)
{
    ASSERT(m_Surfaces[windowId] && m_Surfaces[windowId].get());
    
    RHIVkSurface* surface = m_Surfaces[windowId].get();
    
    return surface->GetSwapChainSupportDetail();
}

const NebulaEngine::RHI::VkSwapChainSupportDetail NebulaEngine::RHI::RHIVkInstance::QuerySwapChainSupport(
    const VkSurfaceKHR surface) const
{
    NebulaEngine::RHI::VkSwapChainSupportDetail details {};

    vkGetPhysicalDeviceSurfaceCapabilitiesKHR(m_CurrentPhysicsDevice, surface, &details.capabilities);

    uint32_t formatCount;
    vkGetPhysicalDeviceSurfaceFormatsKHR(m_CurrentPhysicsDevice, surface, &formatCount, nullptr);

    if (formatCount != 0)
    {
        details.formats.resize(formatCount);
        vkGetPhysicalDeviceSurfaceFormatsKHR(m_CurrentPhysicsDevice, surface, &formatCount, details.formats.data());
    }

    uint32_t presentModeCount;
    vkGetPhysicalDeviceSurfacePresentModesKHR(m_CurrentPhysicsDevice, surface, &presentModeCount, nullptr);

    if (presentModeCount != 0)
    {
        details.presentModes.resize(presentModeCount);
        vkGetPhysicalDeviceSurfacePresentModesKHR(m_CurrentPhysicsDevice, surface, &presentModeCount, details.presentModes.data());
    }
    
    return details;
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
        LOG_FATAL_AND_THROW("[RHIVkInstance::SetupDebugMessager]: failed to set up debug messenger!");
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
    u32 key = windowId;
    m_Surfaces.insert({key, std::make_unique<RHIVkSurface>(std::move(windowId), this)});
}

void NebulaEngine::RHI::RHIVkInstance::DestroySurface(u32&& windowId)
{
   // TODO: 
}

const NebulaEngine::RHI::Surface& NebulaEngine::RHI::RHIVkInstance::GetSurface(u32&& windowId)
{
    ASSERT(m_Surfaces[windowId] && m_Surfaces[windowId].get());
    Surface& surface = *m_Surfaces[windowId].get();
    return surface;
}

bool NebulaEngine::RHI::RHIVkInstance::IsSupportLinearColorSpace(u32&& windowId)
{
   
    auto& supportDetail = GetSwapChainSupportDetails(std::move(windowId));

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
    auto& supportDetail = GetSwapChainSupportDetails(std::move(windowId));
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
   // TODO: 
}

void NebulaEngine::RHI::RHIVkInstance::CreateLogicDevice(u32 windowId)
{
    const Surface& rhiSurface = GetSurface(std::move(windowId));
    VkQueueFamilyIndices indices = FindQueueFamilies(static_cast<VkSurfaceKHR>(rhiSurface.GetHandle()));

    // Queue Create Info 
    Containers::Vector<VkDeviceQueueCreateInfo> queueCreateInfos;
    Containers::Set<uint32_t> uniqueQueueFamilies = { indices.graphicsFamily.value(), indices.presentFamily.value() };

    float queuePriority = 1.0f;
    for (uint32_t queueFamily : uniqueQueueFamilies)
    {
        VkDeviceQueueCreateInfo queueCreateInfo{};
        queueCreateInfo.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
        queueCreateInfo.queueFamilyIndex = queueFamily;
        queueCreateInfo.queueCount = 1;
        queueCreateInfo.pQueuePriorities = &queuePriority;
        queueCreateInfos.push_back(queueCreateInfo);
    }

    // Device Features
    VkPhysicalDeviceFeatures deviceFeatures{};

    // Device Create Info
    VkDeviceCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;

    createInfo.pQueueCreateInfos = queueCreateInfos.data();
    createInfo.queueCreateInfoCount = static_cast<uint32_t>(queueCreateInfos.size());

    createInfo.pEnabledFeatures = &deviceFeatures;

    createInfo.enabledExtensionCount = static_cast<uint32_t>(VkDeviceExtensionNames.size());
    createInfo.ppEnabledExtensionNames = VkDeviceExtensionNames.data();

    if (IsEnableValidation())
    {
        createInfo.enabledLayerCount = static_cast<uint32_t>(VkValidationLayers.size());
        createInfo.ppEnabledLayerNames = VkValidationLayers.data();
    }
    else
    {
        createInfo.enabledLayerCount = 0;
    }

    VkDevice device;
    if (vkCreateDevice(m_CurrentPhysicsDevice, &createInfo, nullptr, &device) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::CreateLogicDevice]: failed to create logical device!");
    }

    VkQueue graphicQueue;
    vkGetDeviceQueue(device, indices.graphicsFamily.value(), 0, &graphicQueue);
    VkQueue presentQueue;
    vkGetDeviceQueue(device, indices.presentFamily.value(), 0, &presentQueue);

    LOG_INFO("[RHIVkInstance::CreateLogicDevice]: Create Logical Device for surface " + std::to_string(windowId));
    m_LogicalDevices.insert({windowId, std::make_unique<RHIVkDevice>(this, graphicQueue, presentQueue, device)});
}

NebulaEngine::RHI::Device& NebulaEngine::RHI::RHIVkInstance::GetLogicalDevice(u32 windowId)
{
    ASSERT(m_LogicalDevices[windowId] && m_LogicalDevices[windowId].get());
    ASSERT(m_LogicalDevices[windowId].get()->m_VkDevice != VK_NULL_HANDLE);
    return *m_LogicalDevices[windowId].get();
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
    for (auto& surfacePair : m_Surfaces)
    {
        auto windowId = surfacePair.first;
        
        if (surfacePair.second.get() == nullptr)
        {
            LOG_WARN(" window: {" + std::to_string(windowId) + "}'s surface is nullptr!");
            continue;
        }

        RHIVkSurface* rhiSurface = surfacePair.second.get();
        auto vkSurface = static_cast<VkSurfaceKHR>(
            rhiSurface->GetHandle());
        auto swapChainSupportDetail = QuerySwapChainSupport(vkSurface);

        rhiSurface->SetSwapChainSupportDetail(std::move(swapChainSupportDetail));
        rhiSurface->SetQueueFamilyIndices(std::move(FindQueueFamilies(vkSurface)));
        
        LOG_DEBUG("Surface " + std::to_string(windowId) + " supported format and color space : ");
        for (auto& format : swapChainSupportDetail.formats)
        {
            LOG_DEBUG("Format:" + std::to_string(format.format) + ", Space:" + std::to_string(format.colorSpace));
        }
        LOG_DEBUG("Surface " + std::to_string(windowId) + " supported present mode : ");
        for (auto& present : swapChainSupportDetail.presentModes)
        {
            LOG_DEBUG("Mode :" + std::to_string(present));
        }
    }
}

NebulaEngine::RHI::Instance* CreateInstance(NebulaEngine::RHI::InstanceInfo&& app_info)
{
    return new NebulaEngine::RHI::RHIVkInstance(std::move(app_info));
}

NebulaEngine::RHI::RHIVkInstance::~RHIVkInstance() noexcept
{
    
    DisposeDebugMessager();
    m_Surfaces.clear();
    m_LogicalDevices.clear();
    
    vkDestroyInstance(m_VkInstance, nullptr);
    LOG_INFO("[RHIVkInstance::~RHIVkInstance]: Destroy Vulkan Instance");
}

void NebulaEngine::RHI::RHIVkInstance::InitLogicDevices()
{
    if (!IsPhysicalDeviceAvailable())
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::InitLogicDevices]: Should pick a physical device first before init logical devices");
    }

    if (!IsSurfacesAvailable())
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::InitLogicDevices]: Should create all the surfaces first before init logical devices");
    }
    
    
    for (auto& surfacePair : m_Surfaces)
    {
        auto windowId = surfacePair.first;
        
        if (surfacePair.second.get() == nullptr)
        {
            LOG_WARN("[RHIVkInstance::InitLogicDevices]: window: {" + std::to_string(windowId) + "}'s surface is nullptr!");
            continue;
        }
        
        CreateLogicDevice(windowId);
        surfacePair.second.get()->InitSwapChain();
    }
    
    LOG_INFO("[RHIVkInstance::InitLogicDevices]: All Logical Devices Init! ");
}

void NebulaEngine::RHI::RHIVkInstance::PickPhysicalDevice(bool considerSurface)
{
    if (!IsSurfacesAvailable())
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::PickPhysicalDevice]: Should create all the surfaces first before pick physical devices");
    }

    // TODO: pick device by surface ?
   
    uint32_t deviceCount = 0;
    vkEnumeratePhysicalDevices(m_VkInstance, &deviceCount, nullptr);

    if (deviceCount == 0) 
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::PickPhysicalDevice]: failed to find GPUs with Vulkan support!");
    }
    
    LOG_DEBUG("[RHIVkInstance::PickPhysicalDevice]: Device Count:" + std::to_string(deviceCount));
    
    Containers::Vector<VkPhysicalDevice> devices(deviceCount);
    vkEnumeratePhysicalDevices(m_VkInstance, &deviceCount, devices.data());

    // Use an ordered map to automatically sort candidates by increasing score
    Containers::Multimap<int, VkPhysicalDevice> candidates;

    for (const auto& device : devices)
    {
        VkPhysicalDeviceProperties deviceProperties;
        vkGetPhysicalDeviceProperties(device, &deviceProperties);
        
        int score = RateDeviceSuitability(device);
        candidates.insert(std::make_pair(score, device));

        LOG_DEBUG("[RHIVkInstance::PickPhysicalDevice]: " + std::string(deviceProperties.deviceName)
            + "'s score :" + std::to_string(score));
    }

    // Check if the best candidate is suitable at all
    if (candidates.rbegin()->first > 0)
    {
        m_CurrentPhysicsDevice = candidates.rbegin()->second;
    }
    else
    {
        LOG_FATAL_AND_THROW("[RHIVkDevice::PickPhysicalDevice]: failed to find a suitable GPU!");
    }
    
    vkGetPhysicalDeviceProperties(m_CurrentPhysicsDevice, &m_DeviceProperties);
    
    LOG_DEBUG("[RHIVkDevice::PickPhysicalDevice]: Picked gpu device : " + std::string(m_DeviceProperties.deviceName));
    
    // TODO: configurable physical device
    // TODO: if current physical device not adequate suitable swap chain, should repick one
    CheckSwapChainCapabilities();
}




