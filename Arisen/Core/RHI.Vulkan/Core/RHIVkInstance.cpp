#include "Core/RHIVkInstance.h"
using namespace ArisenEngine;
#include <vulkan/vulkan_core.h>
#include "Pipeline/RHIVkGPUProgram.h"
#include "Windowing/RenderWindowAPI.h"


bool CheckDeviceExtensionSupport(VkPhysicalDevice device)
{
    uint32_t extensionCount;
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, nullptr);

    ArisenEngine::Containers::Vector<VkExtensionProperties> availableExtensions(extensionCount);
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, availableExtensions.data());

    ArisenEngine::Containers::Set<String> requiredExtensions(ArisenEngine::RHI::VkDeviceExtensionNames.begin(),
        ArisenEngine::RHI::VkDeviceExtensionNames.end());

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
    if (deviceProperties.deviceType == VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
    {
        score += 1000;
    }

    
    // Maximum possible size of textures affects graphics quality
    score += deviceProperties.limits.maxImageDimension2D;
    score += deviceProperties.limits.maxViewports;
    score += deviceProperties.limits.maxSamplerAnisotropy;

    // Application can't function without geometry shaders
    if (!deviceFeatures.geometryShader)
    {
        return 0;
    }

    if (!deviceFeatures.samplerAnisotropy)
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

    for (const char* layerName : ArisenEngine::RHI::VkValidationLayers)
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
            LOG_INFO(String::Format("[RHIVkInstance::CheckValidationLayerSupport]: ValidationLayer not found: %s", layerName));
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
    if (pCallbackData->messageIdNumber == 0x7f1922d7)
    {
        // Silence the "all" was not a valid option for VK_LAYER_REPORT_FLAGS warning.
        // This warning is usually caused by external tools like RenderDoc and is non-critical.
        return VK_FALSE;
    }

    std::cout<<pCallbackData->pMessage<<std::endl;
    
    if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT)
    {
        LOG_ERROR(String::Format(" ######### vk message error: %s", pCallbackData->pMessage));
    }
    else if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT)
    {
        LOG_WARN(String::Format(" ######### vk message warning: %s", pCallbackData->pMessage));
    }
    else if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT)
    {
        LOG_INFO(String::Format(" ######### vk message info: %s", pCallbackData->pMessage));
    }
    else
    {
        LOG_DEBUG(String::Format(" ######### vk message verbose: %s", pCallbackData->pMessage));
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

ArisenEngine::RHI::RHIVkInstance::RHIVkInstance(RHIInstanceInfo&& app_info): RHIInstance(std::move(app_info))
{
    // Environment variable manipulation removed. We now filter non-actionable warnings 
    // in the DebugCallback for a cleaner approach that doesn't affect global state.

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
    VkLayerSettingsCreateInfoEXT settingsCreateInfo = {};
    VkLayerSettingEXT layerSettings[2] = {};
    Containers::Vector<const char*> filteredExtensions;

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

    if (app_info.validationLayer)
    {
        createInfo.enabledLayerCount = static_cast<uint32_t>(VkValidationLayers.size());
        createInfo.ppEnabledLayerNames = VkValidationLayers.data();

        // Configuration for validation layer settings
        static const char* validationLayerName = "VK_LAYER_KHRONOS_validation";
        static const char* reportFlagsValue = "error,warn";
        static VkBool32 syncVal = VK_FALSE;

        // Initialize as standard debug messenger for instance creation/destruction logging
        PopulateDebugMessengerCreateInfo(debugCreateInfo);
        createInfo.pNext = &debugCreateInfo;

        bool layerSettingsSupported = false;
        for (const auto& ext : extensions)
        {
            if (strcmp(VK_EXT_LAYER_SETTINGS_EXTENSION_NAME, ext.extensionName) == 0)
            {
                layerSettingsSupported = true;
                break;
            }
        }

        // Only use layer settings if the extension is supported by the instance
        if (layerSettingsSupported)
        {
            layerSettings[0] = { validationLayerName, "report_flags", VK_LAYER_SETTING_TYPE_STRING_EXT, 1, &reportFlagsValue };
            layerSettings[1] = { validationLayerName, "validate_sync", VK_LAYER_SETTING_TYPE_BOOL32_EXT, 1, &syncVal };

            settingsCreateInfo.sType = VK_STRUCTURE_TYPE_LAYER_SETTINGS_CREATE_INFO_EXT;
            settingsCreateInfo.pNext = &debugCreateInfo;
            settingsCreateInfo.settingCount = 2;
            settingsCreateInfo.pSettings = layerSettings;
            createInfo.pNext = &settingsCreateInfo;
            LOG_INFO("[RHIVkInstance::RHIVkInstance]: VK_EXT_layer_settings supported and used for configuration.");
        }
        else
        {
            LOG_INFO("[RHIVkInstance::RHIVkInstance]: VK_EXT_layer_settings not supported, using standard debug messenger fallback.");
        }

        // Extensions Slot 
        for (const char* extensionName : VkInstanceExtensionNames)
        {
            bool found = false;
            for (const auto& ext : extensions)
            {
                if (strcmp(extensionName, ext.extensionName) == 0)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                filteredExtensions.push_back(extensionName);
            }
            else
            {
                // Silence warning for optional extensions
                if (strcmp(extensionName, VK_EXT_LAYER_SETTINGS_EXTENSION_NAME) != 0)
                {
                    LOG_WARN(String::Format("[RHIVkInstance::RHIVkInstance]: instance extension not supported: %s", extensionName));
                }
            }
        }

        createInfo.enabledExtensionCount = static_cast<uint32_t>(filteredExtensions.size());
        createInfo.ppEnabledExtensionNames = filteredExtensions.data();
    }
    else
    {
        createInfo.enabledLayerCount = 0;
        createInfo.pNext = nullptr;

        // Extensions Slot for non-validation case
        for (const char* extensionName : VkInstanceExtensionNames)
        {
            // Skip validation layer settings extension if validation is off
            if (strcmp(extensionName, VK_EXT_LAYER_SETTINGS_EXTENSION_NAME) == 0) continue;

            bool found = false;
            for (const auto& ext : extensions)
            {
                if (strcmp(extensionName, ext.extensionName) == 0)
                {
                    found = true;
                    break;
                }
            }
            if (found) filteredExtensions.push_back(extensionName);
        }

        createInfo.enabledExtensionCount = static_cast<uint32_t>(filteredExtensions.size());
        createInfo.ppEnabledExtensionNames = filteredExtensions.data();
    }

    VkResult result = vkCreateInstance(&createInfo, nullptr, &m_VkInstance);
    if (result != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW(String::Format("[RHIVkInstance::RHIVkInstance]: failed to create instance! VkResult: %d", (int)result));
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


ArisenEngine::RHI::VkQueueFamilyIndices ArisenEngine::RHI::RHIVkInstance::FindQueueFamilies(VkSurfaceKHR surface)
{

    if (m_CurrentPhysicsDevice == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::FindQueueFamilies]: Physical device invalid!");
    }

    ArisenEngine::RHI::VkQueueFamilyIndices indices;
    
    uint32_t queueFamilyCount = 0;
    vkGetPhysicalDeviceQueueFamilyProperties(m_CurrentPhysicsDevice,
        &queueFamilyCount, nullptr);

    ArisenEngine::Containers::Vector<VkQueueFamilyProperties> queueFamilies(queueFamilyCount);
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

        if (queueFamily.queueFlags & VK_QUEUE_COMPUTE_BIT)
        {
            if (!(queueFamily.queueFlags & VK_QUEUE_GRAPHICS_BIT))
            {
                indices.computeFamily = i; // dedicated compute preferred
            }
            else if (!indices.computeFamily.has_value())
            {
                indices.computeFamily = i;
            }
        }

        if (surface != VK_NULL_HANDLE)
        {
            VkBool32 presentSupport = false;
            vkGetPhysicalDeviceSurfaceSupportKHR(m_CurrentPhysicsDevice, i, surface, &presentSupport);

            if (presentSupport)
            {
                indices.presentFamily = i;
            }
        }
        else
        {
            // For headless, we just need a valid index, but presentFamily won't be used for presentation.
            // We can leave it empty or set it to graphicsFamily. 
            // In RHIVkInstance::CreateLogicDevice, it uses uniqueQueueFamilies.
            indices.presentFamily = i; 
        }
        
        ++i;
    }

    return indices;
}

const ArisenEngine::RHI::VkSwapChainSupportDetail ArisenEngine::RHI::RHIVkInstance::
GetSwapChainSupportDetails(UInt32 windowId)
{
    ASSERT(m_Surfaces[windowId] && m_Surfaces[windowId].get());
    
    RHIVkSurface* surface = m_Surfaces[windowId].get();
    
    return surface->GetSwapChainSupportDetail();
}

const ArisenEngine::RHI::VkSwapChainSupportDetail ArisenEngine::RHI::RHIVkInstance::QuerySwapChainSupport(
    const VkSurfaceKHR surface) const
{
    ArisenEngine::RHI::VkSwapChainSupportDetail details {};

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

void ArisenEngine::RHI::RHIVkInstance::SetupDebugMessager()
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

void ArisenEngine::RHI::RHIVkInstance::DisposeDebugMessager()
{
    if (!m_EnableValidation)
    {
        return;
    }

    DestroyDebugUtilsMessengerEXT(m_VkInstance, m_VkDebugMessenger, nullptr);
}

void ArisenEngine::RHI::RHIVkInstance::CreateSurface(UInt32 windowId)
{
    UInt32 key = windowId;
    m_Surfaces.insert({key, std::make_unique<RHIVkSurface>(std::move(windowId), this)});
}

void ArisenEngine::RHI::RHIVkInstance::DestroySurface(UInt32 windowId)
{
   auto it = m_Surfaces.find(windowId);
   if (it != m_Surfaces.end())
   {
       it->second.reset();
       m_Surfaces.erase(it);
   }
}

ArisenEngine::RHI::RHISurface& ArisenEngine::RHI::RHIVkInstance::GetSurface(UInt32 windowId)
{
    ASSERT(m_Surfaces[windowId] && m_Surfaces[windowId].get());
    RHISurface& surface = *m_Surfaces[windowId].get();
    return surface;
}

bool ArisenEngine::RHI::RHIVkInstance::IsSupportLinearColorSpace(UInt32 windowId)
{
   
    auto& supportDetail = GetSwapChainSupportDetails(windowId);

    for (const auto& availableFormat : supportDetail.formats)
    {
        if (availableFormat.format == VK_FORMAT_B8G8R8A8_SRGB && availableFormat.colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR)
        {
            return true;
        }
    }
    
    return false;
}

bool ArisenEngine::RHI::RHIVkInstance::PresentModeSupported(UInt32 windowId, EPresentMode mode)
{
    auto& supportDetail = GetSwapChainSupportDetails(windowId);
    for (const auto& EPresentMode : supportDetail.presentModes)
    {
        if (EPresentMode == mode)
        {
            return true;
        }
    }

    return false;
}

void ArisenEngine::RHI::RHIVkInstance::SetCurrentPresentMode(UInt32 windowId, EPresentMode mode)
{
    m_PreferredPresentModes[windowId] = mode;
}

void ArisenEngine::RHI::RHIVkInstance::SetResolution(UInt32 windowId, UInt32 width, UInt32 height)
{
   // TODO: 
}

void ArisenEngine::RHI::RHIVkInstance::CreateLogicDevice(UInt32 windowId)
{
    RHISurface* rhiSurface = nullptr;
    VkSurfaceKHR vkSurface = VK_NULL_HANDLE;
    if (windowId != ~0u)
    {
        rhiSurface = &GetSurface(windowId);
        vkSurface = static_cast<VkSurfaceKHR>(rhiSurface->GetHandle());
    }

    VkQueueFamilyIndices indices = FindQueueFamilies(vkSurface);

    // Queue Create Info 
    Containers::Vector<VkDeviceQueueCreateInfo> queueCreateInfos;
    
    Containers::Set<uint32_t> uniqueQueueFamilies;
    if (indices.graphicsFamily.has_value()) uniqueQueueFamilies.insert(indices.graphicsFamily.value());
    if (indices.presentFamily.has_value()) uniqueQueueFamilies.insert(indices.presentFamily.value());
    if (indices.computeFamily.has_value()) uniqueQueueFamilies.insert(indices.computeFamily.value());

    float queuePriority = 1.0f;
    for (uint32_t queueFamily : uniqueQueueFamilies)
    {
        VkDeviceQueueCreateInfo queueCreateInfo{};
        queueCreateInfo.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
        queueCreateInfo.queueFamilyIndex = queueFamily;
        // If compute family is same as graphics, we still only need 1 queue for now 
        // OR we could request 2 if they are same family but different indices? 
        // Usually we just use different family if available.
        queueCreateInfo.queueCount = 1; 
        queueCreateInfo.pQueuePriorities = &queuePriority;
        queueCreateInfos.push_back(queueCreateInfo);
    }

    // Set Device Features
    VkPhysicalDeviceFeatures deviceFeatures{};
    deviceFeatures.samplerAnisotropy = VK_TRUE;
    deviceFeatures.geometryShader = VK_TRUE;

    VkPhysicalDeviceVulkan12Features vulkan12Features{};
    vulkan12Features.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES;
    vulkan12Features.timelineSemaphore = VK_TRUE;
    vulkan12Features.descriptorBindingSampledImageUpdateAfterBind = VK_TRUE;
    vulkan12Features.descriptorBindingStorageBufferUpdateAfterBind = VK_TRUE;
    vulkan12Features.descriptorBindingUpdateUnusedWhilePending = VK_TRUE;
    vulkan12Features.descriptorBindingStorageImageUpdateAfterBind = VK_TRUE;
    vulkan12Features.descriptorBindingUniformBufferUpdateAfterBind = VK_TRUE;
    vulkan12Features.runtimeDescriptorArray = VK_TRUE;
    vulkan12Features.shaderSampledImageArrayNonUniformIndexing = VK_TRUE;
    vulkan12Features.shaderStorageBufferArrayNonUniformIndexing = VK_TRUE;
    vulkan12Features.shaderStorageImageArrayNonUniformIndexing = VK_TRUE;
    vulkan12Features.descriptorBindingPartiallyBound = VK_TRUE;
    vulkan12Features.descriptorBindingVariableDescriptorCount = VK_TRUE;

    VkPhysicalDeviceVulkan13Features vulkan13Features{};
    vulkan13Features.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES;
    vulkan13Features.synchronization2 = VK_TRUE;
    vulkan13Features.dynamicRendering = VK_TRUE;
    vulkan13Features.shaderDemoteToHelperInvocation = VK_TRUE;

    VkPhysicalDeviceMeshShaderFeaturesEXT meshShaderFeatures{};
    meshShaderFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_MESH_SHADER_FEATURES_EXT;
    meshShaderFeatures.meshShader = VK_TRUE;
    meshShaderFeatures.taskShader = VK_TRUE;

    vulkan12Features.pNext = &vulkan13Features;
    vulkan13Features.pNext = &meshShaderFeatures;
    
    // Device Create Info
    VkDeviceCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;

    createInfo.pQueueCreateInfos = queueCreateInfos.data();
    createInfo.queueCreateInfoCount = static_cast<uint32_t>(queueCreateInfos.size());

    createInfo.pEnabledFeatures = &deviceFeatures;
    createInfo.pNext = &vulkan12Features;

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
    VkResult res = vkCreateDevice(m_CurrentPhysicsDevice, &createInfo, nullptr, &device);
    if (res != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW(String::Format("[RHIVkInstance::CreateLogicDevice]: failed to create logical device! VkResult: %d", (int)res));
    }

    VkQueue graphicQueue = VK_NULL_HANDLE;
    if (indices.graphicsFamily.has_value())
    {
        vkGetDeviceQueue(device, indices.graphicsFamily.value(), 0, &graphicQueue);
    }
    
    VkQueue presentQueue = VK_NULL_HANDLE;
    if (windowId != ~0u && indices.presentFamily.has_value())
    {
        vkGetDeviceQueue(device, indices.presentFamily.value(), 0, &presentQueue);
    }

    VkQueue computeQueue = VK_NULL_HANDLE;
    if (indices.computeFamily.has_value())
    {
        vkGetDeviceQueue(device, indices.computeFamily.value(), 0, &computeQueue);
    }

    VkPhysicalDeviceMemoryProperties memoryProperties;
    vkGetPhysicalDeviceMemoryProperties(m_CurrentPhysicsDevice, &memoryProperties);

    auto logicalDevice = std::make_unique<RHIVkDevice>(this, rhiSurface, graphicQueue, presentQueue, computeQueue, device, memoryProperties, indices.graphicsFamily.value(), indices.computeFamily.value_or(0));
    VkPhysicalDeviceProperties physicalProperties {};
    vkGetPhysicalDeviceProperties(m_CurrentPhysicsDevice, &physicalProperties);
    {
        logicalDevice->m_DeviceLimits.sampler.maxSamplerAnisotropy = physicalProperties.limits.maxSamplerAnisotropy;
    }
    
    LOG_INFO(String::Format("[RHIVkInstance::CreateLogicDevice]: Create Logical Device for surface %d", windowId));
    m_LogicalDevices.insert(
        {    windowId,
             std::move(logicalDevice)
        });
}

ArisenEngine::RHI::RHIDevice* ArisenEngine::RHI::RHIVkInstance::GetLogicalDevice(UInt32 windowId)
{
    ASSERT(m_LogicalDevices[windowId] && m_LogicalDevices[windowId].get());
    ASSERT(m_LogicalDevices[windowId].get()->m_VkDevice != VK_NULL_HANDLE);
    return m_LogicalDevices[windowId].get();
}

ArisenEngine::RHI::EFormat ArisenEngine::RHI::RHIVkInstance::GetSuitableSwapChainFormat(UInt32 windowId)
{
    auto& supportDetail = GetSwapChainSupportDetails(windowId);
    // Prefer SRGB BGRA8 if available, else first format
    for (const auto& f : supportDetail.formats)
    {
        if (f.format == VK_FORMAT_B8G8R8A8_SRGB && f.colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR)
        {
            return static_cast<EFormat>(f.format);
        }
    }
    return static_cast<EFormat>(supportDetail.formats[0].format);
}

ArisenEngine::RHI::EPresentMode ArisenEngine::RHI::RHIVkInstance::GetSuitablePresentMode(UInt32 windowId)
{
    auto& supportDetail = GetSwapChainSupportDetails(windowId);
    // If user set a preferred mode and it's supported, use it
    auto it = m_PreferredPresentModes.find(windowId);
    if (it != m_PreferredPresentModes.end())
    {
        for (auto pm : supportDetail.presentModes)
        {
            if (pm == static_cast<VkPresentModeKHR>(it->second))
            {
                return it->second;
            }
        }
    }
    // Else prefer IMMEDIATE, fall back to FIFO
    for (auto pm : supportDetail.presentModes)
    {
        if (pm == VK_PRESENT_MODE_IMMEDIATE_KHR) return static_cast<EPresentMode>(pm);
    }
    return PRESENT_MODE_FIFO;
}

void ArisenEngine::RHI::RHIVkInstance::UpdateSurfaceCapabilities(RHISurface* surface)
{
    auto vkSurface = static_cast<VkSurfaceKHR>(
           surface->GetHandle());
    auto swapChainSupportDetail = QuerySwapChainSupport(vkSurface);

    RHIVkSurface* rhiSurface = static_cast<RHIVkSurface*>(surface);
    rhiSurface->SetSwapChainSupportDetail(std::move(swapChainSupportDetail));
}

void ArisenEngine::RHI::RHIVkInstance::CheckSwapChainCapabilities()
{
    for (auto& surfacePair : m_Surfaces)
    {
        auto windowId = surfacePair.first;
        
        if (surfacePair.second.get() == nullptr)
        {
            LOG_WARN(String::Format(" window: {%d}'s surface is nullptr!", windowId));
            continue;
        }

        RHIVkSurface* rhiSurface = surfacePair.second.get();
        auto vkSurface = static_cast<VkSurfaceKHR>(
            rhiSurface->GetHandle());
        auto swapChainSupportDetail = QuerySwapChainSupport(vkSurface);

        rhiSurface->SetSwapChainSupportDetail(std::move(swapChainSupportDetail));
        rhiSurface->SetQueueFamilyIndices(std::move(FindQueueFamilies(vkSurface)));
    }
}

ArisenEngine::RHI::RHIInstance* CreateInstance(ArisenEngine::RHI::RHIInstanceInfo&& app_info)
{
    return new ArisenEngine::RHI::RHIVkInstance(std::move(app_info));
}

ArisenEngine::RHI::RHIVkInstance::~RHIVkInstance() noexcept
{
    LOG_INFO("[RHIVkInstance::~RHIVkInstance]: Start Destroying Vulkan Instance");
    
    // Explicitly wait for all devices to be idle before cleanup to avoid hangs
    for (auto& pair : m_LogicalDevices)
    {
        if (pair.second)
        {
            LOG_INFO(String::Format("[RHIVkInstance::~RHIVkInstance]: Waiting for Logical Device (surface %d) to idle", pair.first));
            auto* vkDevice = static_cast<RHIVkDevice*>(pair.second.get());
            if (vkDevice->GetHandle())
            {
                vkDeviceWaitIdle(static_cast<VkDevice>(vkDevice->GetHandle()));
            }
        }
    }

    LOG_INFO("[RHIVkInstance::~RHIVkInstance]: Clearing Surfaces");
    m_Surfaces.clear();

    LOG_INFO("[RHIVkInstance::~RHIVkInstance]: Clearing Logical Devices");
    m_LogicalDevices.clear();
    
    LOG_INFO("[RHIVkInstance::~RHIVkInstance]: Disposing Debug Messenger");
    DisposeDebugMessager();
    
    LOG_INFO("[RHIVkInstance::~RHIVkInstance]: Calling vkDestroyInstance");
    if (m_VkInstance != VK_NULL_HANDLE)
    {
        vkDestroyInstance(m_VkInstance, nullptr);
        m_VkInstance = VK_NULL_HANDLE;
    }
    LOG_INFO("[RHIVkInstance::~RHIVkInstance]: Destroyed Vulkan Instance");
}

void ArisenEngine::RHI::RHIVkInstance::InitLogicDevices()
{
    if (!IsPhysicalDeviceAvailable())
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::InitLogicDevices]: Should pick a physical device first before init logical devices");
    }

    if (!IsSurfacesAvailable())
    {
        LOG_INFO("[RHIVkInstance::InitLogicDevices]: No surfaces available, creating headless logical device.");
        CreateLogicDevice(~0u);
        return;
    }
    
    
    for (auto& surfacePair : m_Surfaces)
    {
        auto windowId = surfacePair.first;
        
        if (surfacePair.second.get() == nullptr)
        {
            LOG_WARN(String::Format("[RHIVkInstance::InitLogicDevices]: window: {%d}'s surface is nullptr!", windowId));
            continue;
        }
        
        CreateLogicDevice(windowId);
        surfacePair.second.get()->InitSwapChain();
    }
    
    LOG_INFO("[RHIVkInstance::InitLogicDevices]: All Logical Devices Init! ");
}

void ArisenEngine::RHI::RHIVkInstance::PickPhysicalDevice(bool considerSurface)
{
    // For headless, we might not have surfaces yet.
    // In multi-window scenarios, we might want to pick a device that supports all surfaces.
    // However, for now, we just pick the best device.

    // TODO: pick device by surface ?
   
    uint32_t deviceCount = 0;
    vkEnumeratePhysicalDevices(m_VkInstance, &deviceCount, nullptr);

    if (deviceCount == 0) 
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::PickPhysicalDevice]: failed to find GPUs with Vulkan support!");
    }
    
    LOG_DEBUG(String::Format("[RHIVkInstance::PickPhysicalDevice]: Device Count: %d", deviceCount));
    
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
    
    LOG_DEBUG(String::Format("[RHIVkDevice::PickPhysicalDevice]: Picked gpu device : %s", m_DeviceProperties.deviceName));


    // initialize limit info
    {
        // sampler 
        m_DeviceLimits.sampler.maxSamplerAnisotropy = m_DeviceProperties.limits.maxSamplerAnisotropy;
        
    }
    // TODO: configurable physical device
    // TODO: if current physical device not adequate suitable swap chain, should repick one
    CheckSwapChainCapabilities();
}








