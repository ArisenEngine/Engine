#include "RHIVkDevice.h"

#include <stacktrace>

#include "Logger/Logger.h"

// validation layers
NebulaEngine::Containers::Vector<const char*> DeviceValidationLayers
{
    "VK_LAYER_KHRONOS_validation"
};

NebulaEngine::Containers::Vector<const char*> DeviceExtensionNames
{
    VK_KHR_SWAPCHAIN_EXTENSION_NAME
};

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

    return score;
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

NebulaEngine::RHI::RHIVkDevice::RHIVkDevice(Instance& instance): Device(instance)
{
    // physical device
    PickPhysicalDevice();
    
}

void NebulaEngine::RHI::RHIVkDevice::PickPhysicalDevice()
{
    uint32_t deviceCount = 0;
    auto vkInstance = static_cast<VkInstance>(m_Instance.GetHandle());
    vkEnumeratePhysicalDevices(vkInstance, &deviceCount, nullptr);

    if (deviceCount == 0) 
    {
        LOG_FATAL_AND_THROW("failed to find GPUs with Vulkan support!");
    }
    
    LOG_INFO("Device Count:" + deviceCount);

    std::vector<VkPhysicalDevice> devices(deviceCount);
    vkEnumeratePhysicalDevices(vkInstance, &deviceCount, devices.data());

    // Use an ordered map to automatically sort candidates by increasing score
    std::multimap<int, VkPhysicalDevice> candidates;

    for (const auto& device : devices)
    {
        int score = RateDeviceSuitability(device);
        candidates.insert(std::make_pair(score, device));
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

    VkPhysicalDeviceProperties deviceProperties;
    vkGetPhysicalDeviceProperties(m_CurrentPhysicsDevice, &deviceProperties);
    
    LOG_INFO("GPU Device : " + std::string(deviceProperties.deviceName));
    
}

NebulaEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    LOG_INFO("~RHIVkPhysicalDevice");
    m_LogicalDevices.clear();
}

void NebulaEngine::RHI::RHIVkDevice::CreateLogicDevice(u32 windowId)
{
    const Surface& rhiSurface = m_Instance.GetSurface(std::move(windowId));
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

    if (m_Instance.IsEnableValidation())
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
