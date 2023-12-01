#include "RHIVkDevice.h"
#include "Logger/Logger.h"

// validation layers
NebulaEngine::Containers::vector<const char*> DeviceValidationLayers
{
    "VK_LAYER_KHRONOS_validation"
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

NebulaEngine::RHI::QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice device)
{
    if (device == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("Physical device invalid!")
    }

    NebulaEngine::RHI::QueueFamilyIndices indices;
    
    uint32_t queueFamilyCount = 0;
    vkGetPhysicalDeviceQueueFamilyProperties(device,
        &queueFamilyCount, nullptr);

    NebulaEngine::Containers::vector<VkQueueFamilyProperties> queueFamilies(queueFamilyCount);
    vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount,
        queueFamilies.data());

    int i = 0;
    for (const auto& queueFamily : queueFamilies)
    {
        if (queueFamily.queueFlags & VK_QUEUE_GRAPHICS_BIT)
        {
            indices.graphicsFamily = i;
        }

        if (indices.IsComplete())
        {
            break;
        }
        
        i++;
    }

    return indices;
}

bool IsDeviceSuitable(VkPhysicalDevice device, NebulaEngine::RHI::QueueFamilyIndices& indices)
{
    indices = FindQueueFamilies(device);
    return indices.IsComplete();
}

NebulaEngine::RHI::RHIVkDevice::RHIVkDevice(Instance& instance): Device(instance)
{
    // physical device
    
    uint32_t deviceCount = 0;
    auto vkInstance = static_cast<VkInstance>(instance.GetHandle());
    vkEnumeratePhysicalDevices(vkInstance, &deviceCount, nullptr);
    
    if (deviceCount == 0)
    {
        LOG_FATAL_AND_THROW("failed to find GPUs with Vulkan support!")
    }

    Containers::vector<VkPhysicalDevice> physicalDevices(deviceCount);
    vkEnumeratePhysicalDevices(vkInstance, &deviceCount, physicalDevices.data());

    for (const auto& device : physicalDevices) 
    {
        QueueFamilyIndices indices;
        if (IsDeviceSuitable(device, indices))
        {
            int score = RateDeviceSuitability(device);
            m_Candidates.insert(std::make_pair(score, std::pair(device, indices)));
        }
    }

    // Check if the best candidate is suitable at all
    if (m_Candidates.rbegin()->first > 0)
    {
        m_CurrentPhysicsDevice = m_Candidates.rbegin()->second.first;
        m_QueueFamilyIndices = m_Candidates.rbegin()->second.second;

        if (!m_QueueFamilyIndices.IsComplete())
        {
            LOG_FATAL_AND_THROW("failed to find queue family!")
        }
    }
    else
    {
        LOG_FATAL_AND_THROW("failed to find a suitable GPU!")
    }

    VkPhysicalDeviceProperties deviceProperties;
    vkGetPhysicalDeviceProperties(m_CurrentPhysicsDevice, &deviceProperties);
    
    LOG_INFO("GPU Device : " + std::string(deviceProperties.deviceName));


    // create logical device
    VkDeviceQueueCreateInfo queueCreateInfo{};
    queueCreateInfo.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
    queueCreateInfo.queueFamilyIndex = m_QueueFamilyIndices.graphicsFamily.value();
    queueCreateInfo.queueCount = 1;
    
    float queuePriority = 1.0f;
    queueCreateInfo.pQueuePriorities = &queuePriority;
    
    VkPhysicalDeviceFeatures deviceFeatures{};
    
    VkDeviceCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;
    createInfo.pQueueCreateInfos = &queueCreateInfo;
    createInfo.queueCreateInfoCount = 1;
    createInfo.pEnabledFeatures = &deviceFeatures;
    
    createInfo.enabledExtensionCount = 0;
    if (m_Instance.IsEnableValidation())
    {
        createInfo.enabledLayerCount = static_cast<uint32_t>(DeviceValidationLayers.size());
        createInfo.ppEnabledLayerNames = DeviceValidationLayers.data();
    }
    else
    {
        createInfo.enabledLayerCount = 0;
    }

    if (vkCreateDevice(m_CurrentPhysicsDevice, &createInfo, nullptr, &m_LogicalDevice) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create logical device!");
    }

    
}

NebulaEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    LOG_INFO("~RHIVkPhysicalDevice");
    vkDestroyDevice(m_LogicalDevice, nullptr);
}