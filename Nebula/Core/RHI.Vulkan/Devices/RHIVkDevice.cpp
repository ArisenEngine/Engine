#include "RHIVkDevice.h"

#include <stacktrace>

#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"

// validation layers
NebulaEngine::Containers::Vector<const char*> DeviceValidationLayers
{
    "VK_LAYER_KHRONOS_validation"
};

NebulaEngine::Containers::Vector<const char*> DeviceExtensionNames
{
    VK_KHR_SWAPCHAIN_EXTENSION_NAME
};

bool CheckDeviceExtensionSupport(VkPhysicalDevice device)
{
    uint32_t extensionCount;
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, nullptr);

    std::vector<VkExtensionProperties> availableExtensions(extensionCount);
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, availableExtensions.data());

    std::set<std::string> requiredExtensions(DeviceExtensionNames.begin(), DeviceExtensionNames.end());

    for (const auto& extension : availableExtensions)
    {
        requiredExtensions.erase(extension.extensionName);
    }

    return requiredExtensions.empty();
}

NebulaEngine::RHI::SwapChainSupportDetail QuerySwapChainSupport(VkPhysicalDevice device, VkSurfaceKHR surface);
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

const NebulaEngine::RHI::SwapChainSupportDetail NebulaEngine::RHI::RHIVkDevice::QuerySwapChainSupport(const VkSurfaceKHR surface) const
{
    NebulaEngine::RHI::SwapChainSupportDetail details {};

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

void NebulaEngine::RHI::RHIVkDevice::CreateSurface(u32&& windowId)
{
    u32 key = windowId;
    m_Surfaces.insert({key, std::make_unique<RHIVkSurface>(std::move(windowId), m_Instance)});
}

void NebulaEngine::RHI::RHIVkDevice::DestroySurface(u32&& windowId)
{
}

const NebulaEngine::RHI::Surface& NebulaEngine::RHI::RHIVkDevice::GetSurface(u32&& windowId)
{
    ASSERT(m_Surfaces[windowId] && m_Surfaces[windowId].get());
    Surface& surface = *m_Surfaces[windowId].get();
    return surface;
}

void NebulaEngine::RHI::RHIVkDevice::SetResolution(const u32&& windowId, const u32&& width, const u32&& height)
{
}

void NebulaEngine::RHI::RHIVkDevice::CheckSwapChainCapabilities()
{
    for (auto& surfacePair : m_Surfaces)
    {
        auto windowId = surfacePair.first;
        
        if (surfacePair.second.get() == nullptr)
        {
            LOG_WARN(" window: {" + std::to_string(windowId) + "}'s surface is nullptr!");
            continue;
        }

        RHIVkSurface* rhiSurface = static_cast<RHIVkSurface*>(surfacePair.second.get());
        auto swapChainSupportDetail = QuerySwapChainSupport(static_cast<VkSurfaceKHR>(
            rhiSurface->GetHandle()));
        rhiSurface->SetSwapChainSupportDetail(std::move(swapChainSupportDetail));
        
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

void NebulaEngine::RHI::RHIVkDevice::InitDefaultSwapChains()
{
    for (auto& surfacePair : m_Surfaces)
    {
        auto windowId = surfacePair.first;
        
        if (surfacePair.second.get() == nullptr)
        {
            LOG_WARN(" window: {" + std::to_string(windowId) + "}'s surface is nullptr!");
            continue;
        }
        
        CreateSwapChain(windowId);
    }
}

NebulaEngine::RHI::QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice device, VkSurfaceKHR surface)
{
    if (device == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("Physical device invalid!")
    }

    NebulaEngine::RHI::QueueFamilyIndices indices;
    
    uint32_t queueFamilyCount = 0;
    vkGetPhysicalDeviceQueueFamilyProperties(device,
        &queueFamilyCount, nullptr);

    NebulaEngine::Containers::Vector<VkQueueFamilyProperties> queueFamilies(queueFamilyCount);
    vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount,
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
        vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, &presentSupport);

        if (presentSupport)
        {
            indices.presentFamily = i;
        }
        
        ++i;
    }

    return indices;
}

NebulaEngine::RHI::RHIVkDevice::RHIVkDevice(Instance* instance): Device(instance)
{
    
}

void NebulaEngine::RHI::RHIVkDevice::PickPhysicalDevice()
{
    uint32_t deviceCount = 0;
    auto vkInstance = static_cast<VkInstance>(m_Instance->GetHandle());
    vkEnumeratePhysicalDevices(vkInstance, &deviceCount, nullptr);

    if (deviceCount == 0) 
    {
        LOG_FATAL_AND_THROW("failed to find GPUs with Vulkan support!");
    }
    
    LOG_INFO("Device Count:" + std::to_string(deviceCount));
    
    std::vector<VkPhysicalDevice> devices(deviceCount);
    vkEnumeratePhysicalDevices(vkInstance, &deviceCount, devices.data());

    // Use an ordered map to automatically sort candidates by increasing score
    std::multimap<int, VkPhysicalDevice> candidates;

    for (const auto& device : devices)
    {
        VkPhysicalDeviceProperties deviceProperties;
        vkGetPhysicalDeviceProperties(device, &deviceProperties);
        
        int score = RateDeviceSuitability(device);
        candidates.insert(std::make_pair(score, device));

        LOG_INFO(std::string(deviceProperties.deviceName) + "'s score :" + std::to_string(score));
    }

    // Check if the best candidate is suitable at all
    if (candidates.rbegin()->first > 0)
    {
        m_CurrentPhysicsDevice = candidates.rbegin()->second;
    }
    else
    {
        LOG_FATAL_AND_THROW("failed to find a suitable GPU!");
    }
    
    vkGetPhysicalDeviceProperties(m_CurrentPhysicsDevice, &m_DeviceProperties);
    
    LOG_INFO("Picked gpu device : " + std::string(m_DeviceProperties.deviceName));
    
}

const NebulaEngine::RHI::SwapChainSupportDetail NebulaEngine::RHI::RHIVkDevice::GetSwapChainSupportDetails(u32&& windowId)
{
    ASSERT(m_Surfaces[windowId] && m_Surfaces[windowId].get());
    
    RHIVkSurface* surface = static_cast<RHIVkSurface*>(m_Surfaces[windowId].get());
    
    return surface->GetSwapChainSupportDetail();
}

NebulaEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    m_Instance = nullptr;
    m_LogicalDevices.clear();
    LOG_INFO("~RHIVkDevice");
}

void NebulaEngine::RHI::RHIVkDevice::CreateLogicDevice(u32 windowId)
{
    const Surface& rhiSurface = m_Instance->GetSurface(std::move(windowId));
    QueueFamilyIndices indices = FindQueueFamilies(m_CurrentPhysicsDevice, static_cast<VkSurfaceKHR>(rhiSurface.GetHandle()));

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

    createInfo.enabledExtensionCount = static_cast<uint32_t>(DeviceExtensionNames.size());
    createInfo.ppEnabledExtensionNames = DeviceExtensionNames.data();

    if (m_Instance->IsEnableValidation())
    {
        createInfo.enabledLayerCount = static_cast<uint32_t>(ValidationLayers.size());
        createInfo.ppEnabledLayerNames = ValidationLayers.data();
    }
    else
    {
        createInfo.enabledLayerCount = 0;
    }

    VkDevice device;
    if (vkCreateDevice(m_CurrentPhysicsDevice, &createInfo, nullptr, &device) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create logical device!");
    }

    VkQueue graphicQueue;
    vkGetDeviceQueue(device, indices.graphicsFamily.value(), 0, &graphicQueue);
    VkQueue presentQueue;
    vkGetDeviceQueue(device, indices.presentFamily.value(), 0, &presentQueue);

    LOG_INFO(" Create Logical Device for surface " + std::to_string(windowId));
    m_LogicalDevices.insert({windowId, std::make_unique<LogicalDevice>(graphicQueue, presentQueue, device)});
}

void NebulaEngine::RHI::RHIVkDevice::InitLogicDevices()
{
    for (auto& surfacePair : m_Surfaces)
    {
        auto windowId = surfacePair.first;
        
        if (surfacePair.second.get() == nullptr)
        {
            LOG_WARN(" window: {" + std::to_string(windowId) + "}'s surface is nullptr!");
            continue;
        }
        
        CreateLogicDevice(windowId);
        surfacePair.second.get()->InitSwapChain();
    }
}

void* NebulaEngine::RHI::RHIVkDevice::GetLogicalDevice(u32 windowId)
{
    ASSERT(m_LogicalDevices[windowId] && m_LogicalDevices[windowId].get());
    
    return m_LogicalDevices[windowId].get()->vkDevice;
}

void NebulaEngine::RHI::RHIVkDevice::CreateSwapChain(u32 windowId)
{
    
}
