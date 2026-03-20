#include "Core/RHIVkInstance.h"
using namespace ArisenEngine;
#include <vulkan/vulkan_core.h>
#include "Pipeline/RHIVkGPUProgram.h"
#include "Windowing/RenderWindowAPI.h"
#include "Windowing/RenderWindowAPI.h"

namespace ArisenEngine::RHI
{
    VulkanInitSettings VulkanInitSettings::GetDefault()
    {
        VulkanInitSettings settings;
        settings.validationLayers = { "VK_LAYER_KHRONOS_validation" };
        settings.instanceExtensions = {
            VK_EXT_DEBUG_UTILS_EXTENSION_NAME,
            VK_EXT_LAYER_SETTINGS_EXTENSION_NAME,
            "VK_KHR_win32_surface",
            "VK_KHR_surface"
        };
        settings.mandatoryDeviceExtensions = {
            VK_KHR_SWAPCHAIN_EXTENSION_NAME,
            VK_KHR_TIMELINE_SEMAPHORE_EXTENSION_NAME,
            VK_KHR_SYNCHRONIZATION_2_EXTENSION_NAME,
            VK_KHR_DYNAMIC_RENDERING_EXTENSION_NAME,
            VK_KHR_BUFFER_DEVICE_ADDRESS_EXTENSION_NAME
        };
        settings.optionalDeviceExtensions = {
            VK_EXT_MESH_SHADER_EXTENSION_NAME,
            VK_KHR_ACCELERATION_STRUCTURE_EXTENSION_NAME,
            VK_KHR_RAY_TRACING_PIPELINE_EXTENSION_NAME,
            VK_KHR_RAY_QUERY_EXTENSION_NAME,
            VK_KHR_DEFERRED_HOST_OPERATIONS_EXTENSION_NAME,
            VK_EXT_ROBUSTNESS_2_EXTENSION_NAME,
            VK_KHR_FRAGMENT_SHADING_RATE_EXTENSION_NAME,
            VK_KHR_MAINTENANCE_2_EXTENSION_NAME,
            VK_EXT_DESCRIPTOR_BUFFER_EXTENSION_NAME
        };
        return settings;
    }
}
bool CheckDeviceExtensionSupport(VkPhysicalDevice device)
{
    uint32_t extensionCount;
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, nullptr);

    ArisenEngine::Containers::Vector<VkExtensionProperties> availableExtensions(extensionCount);
    vkEnumerateDeviceExtensionProperties(device, nullptr, &extensionCount, availableExtensions.data());

    const auto& mandatoryExtensions = ArisenEngine::RHI::VulkanInitSettings::GetDefault().mandatoryDeviceExtensions;
    ArisenEngine::Containers::Set<String> requiredExtensions(mandatoryExtensions.begin(),
                                                             mandatoryExtensions.end());

    for (const auto& extension : availableExtensions)
    {
        requiredExtensions.erase(extension.extensionName);
    }

    if (!requiredExtensions.empty())
    {
        for (const auto& ext : requiredExtensions)
        {
            LOG_WARN(
                String::Format("[CheckDeviceExtensionSupport]: Mandatory extension not supported: %s", ext.c_str()));
        }
    }

    return requiredExtensions.empty();
}

int RateDeviceSuitability(VkPhysicalDevice device)
{
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

    // Application can't function without geometry, tessellation shaders and wireframe support
    if (!deviceFeatures.geometryShader || !deviceFeatures.tessellationShader || !deviceFeatures.fillModeNonSolid)
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

    const auto& valLayers = ArisenEngine::RHI::VulkanInitSettings::GetDefault().validationLayers;
    for (const char* layerName : valLayers)
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
            LOG_INFO(
                String::Format("[RHIVkInstance::CheckValidationLayerSupport]: ValidationLayer not found: %s", layerName
                ));
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

    std::cout << pCallbackData->pMessage << std::endl;

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

    m_Settings = VulkanInitSettings::GetDefault();

    if (app_info.validationLayer && !CheckValidationLayerSupport())
    {
        LOG_FATAL_AND_THROW("[RHIVkInstance::RHIVkInstance]: validation layers requested, but not available!");
    }

    m_EnableValidation = app_info.validationLayer;
    m_VulkanVersion = {app_info.variant, app_info.major, app_info.minor};

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
        createInfo.enabledLayerCount = static_cast<uint32_t>(m_Settings.validationLayers.size());
        createInfo.ppEnabledLayerNames = m_Settings.validationLayers.data();

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
            layerSettings[0] = {
                validationLayerName, "report_flags", VK_LAYER_SETTING_TYPE_STRING_EXT, 1, &reportFlagsValue
            };
            layerSettings[1] = {validationLayerName, "validate_sync", VK_LAYER_SETTING_TYPE_BOOL32_EXT, 1, &syncVal};

            settingsCreateInfo.sType = VK_STRUCTURE_TYPE_LAYER_SETTINGS_CREATE_INFO_EXT;
            settingsCreateInfo.pNext = &debugCreateInfo;
            settingsCreateInfo.settingCount = 2;
            settingsCreateInfo.pSettings = layerSettings;
            createInfo.pNext = &settingsCreateInfo;
            LOG_INFO("[RHIVkInstance::RHIVkInstance]: VK_EXT_layer_settings supported and used for configuration.");
        }
        else
        {
            LOG_INFO(
                "[RHIVkInstance::RHIVkInstance]: VK_EXT_layer_settings not supported, using standard debug messenger fallback.");
        }

        // Extensions Slot 
        for (const char* extensionName : m_Settings.instanceExtensions)
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
                    LOG_WARN(
                        String::Format("[RHIVkInstance::RHIVkInstance]: instance extension not supported: %s",
                            extensionName));
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
        for (const char* extensionName : m_Settings.instanceExtensions)
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
        LOG_FATAL_AND_THROW(
            String::Format("[RHIVkInstance::RHIVkInstance]: failed to create instance! VkResult: %d", (int)result));
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

        if (queueFamily.queueFlags & VK_QUEUE_TRANSFER_BIT)
        {
            if (!(queueFamily.queueFlags & VK_QUEUE_GRAPHICS_BIT) && !(queueFamily.queueFlags & VK_QUEUE_COMPUTE_BIT))
            {
                indices.transferFamily = i; // dedicated transfer preferred
            }
            else if (!indices.transferFamily.has_value())
            {
                indices.transferFamily = i;
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
    ArisenEngine::RHI::VkSwapChainSupportDetail details{};

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
        vkGetPhysicalDeviceSurfacePresentModesKHR(m_CurrentPhysicsDevice, surface, &presentModeCount,
                                                  details.presentModes.data());
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
        if (availableFormat.format == VK_FORMAT_B8G8R8A8_SRGB && availableFormat.colorSpace ==
            VK_COLOR_SPACE_SRGB_NONLINEAR_KHR)
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
    if (m_LogicalDevices.find(windowId) != m_LogicalDevices.end())
    {
        LOG_WARN(
            String::Format(
                "[RHIVkInstance::CreateLogicDevice]: Logical device for windowId %u already exists, skipping creation.",
                windowId));
        return;
    }

    RHISurface* rhiSurface = nullptr;
    VkSurfaceKHR vkSurface = VK_NULL_HANDLE;
    if (windowId != ~0u)
    {
        rhiSurface = &GetSurface(windowId);
        vkSurface = static_cast<VkSurfaceKHR>(rhiSurface->GetHandle());
    }

    VkQueueFamilyIndices indices = FindQueueFamilies(vkSurface);

    // Enumerate Queue Families to get real hardware limits
    uint32_t queueFamilyPropCount = 0;
    vkGetPhysicalDeviceQueueFamilyProperties(m_CurrentPhysicsDevice, &queueFamilyPropCount, nullptr);
    Containers::Vector<VkQueueFamilyProperties> queueFamilyProps(queueFamilyPropCount);
    vkGetPhysicalDeviceQueueFamilyProperties(m_CurrentPhysicsDevice, &queueFamilyPropCount, queueFamilyProps.data());

    // Queue Create Info 
    Containers::Vector<VkDeviceQueueCreateInfo> queueCreateInfos;
    Containers::Set<uint32_t> uniqueQueueFamilies = {
        indices.graphicsFamily.value(),
        indices.presentFamily.value()
    };
    if (indices.computeFamily.has_value())
    {
        uniqueQueueFamilies.insert(indices.computeFamily.value());
    }
    if (indices.transferFamily.has_value())
    {
        uniqueQueueFamilies.insert(indices.transferFamily.value());
    }

    // Keep track of how many queues we actually request per family to assign correct indices later
    Containers::Map<uint32_t, uint32_t> requestedQueueCounts;

    float queuePriority = 1.0f;
    for (uint32_t queueFamily : uniqueQueueFamilies)
    {
        uint32_t maxQueues = queueFamily < queueFamilyProps.size() ? queueFamilyProps[queueFamily].queueCount : 1;
        
        uint32_t desiredQueueCount = 0;
        if (indices.graphicsFamily.value() == queueFamily) desiredQueueCount++;
        if (indices.computeFamily.has_value() && indices.computeFamily.value() == queueFamily) desiredQueueCount++;
        if (indices.transferFamily.has_value() && indices.transferFamily.value() == queueFamily) desiredQueueCount++;
        
        // Ensure at least 1 queue if it's uniquely the present family (though present usually shares with graphics)
        if (desiredQueueCount == 0 && indices.presentFamily.value() == queueFamily) desiredQueueCount = 1;

        // Clamp to physically supported limits
        uint32_t actualQueueCount = (std::min)(desiredQueueCount, maxQueues);
        actualQueueCount = (std::max)(1u, actualQueueCount);

        requestedQueueCounts[queueFamily] = actualQueueCount;

        VkDeviceQueueCreateInfo queueCreateInfo{};
        queueCreateInfo.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
        queueCreateInfo.queueFamilyIndex = queueFamily;
        queueCreateInfo.queueCount = actualQueueCount;

        static float priorities[3] = {1.0f, 1.0f, 1.0f};
        queueCreateInfo.pQueuePriorities = priorities;
        queueCreateInfos.push_back(queueCreateInfo);
    }

    // Enumerate supported device extensions
    uint32_t extensionCount = 0;
    vkEnumerateDeviceExtensionProperties(m_CurrentPhysicsDevice, nullptr, &extensionCount, nullptr);
    Containers::Vector<VkExtensionProperties> availableExtensions(extensionCount);
    vkEnumerateDeviceExtensionProperties(m_CurrentPhysicsDevice, nullptr, &extensionCount, availableExtensions.data());

    Containers::Vector<const char*> enabledExtensions;
    auto checkAndEnable = [&](const Containers::Vector<const char*>& extensionList, bool mandatory)
    {
        for (const char* extensionName : extensionList)
        {
            // Skip swapchain if headless
            if (windowId == ~0u && strcmp(extensionName, VK_KHR_SWAPCHAIN_EXTENSION_NAME) == 0)
            {
                continue;
            }

            bool found = false;
            for (const auto& ext : availableExtensions)
            {
                if (strcmp(extensionName, ext.extensionName) == 0)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                enabledExtensions.push_back(extensionName);
            }
            else if (mandatory)
            {
        LOG_WARN(
                    String::Format("[RHIVkInstance::CreateLogicDevice]: mandatory device extension not supported: %s",
                        extensionName));
            }
            else
            {
        LOG_INFO(
                    String::Format("[RHIVkInstance::CreateLogicDevice]: optional device extension not supported: %s",
                        extensionName));
            }
        }
    };

    checkAndEnable(m_Settings.mandatoryDeviceExtensions, true);
    checkAndEnable(m_Settings.optionalDeviceExtensions, false);

    // Set Device Features
    VkPhysicalDeviceFeatures features{};
    features.samplerAnisotropy = VK_TRUE;
    features.geometryShader = VK_TRUE;
    features.tessellationShader = VK_TRUE;
    features.fillModeNonSolid = VK_TRUE;
    features.multiDrawIndirect = VK_TRUE; // Enabled Multi-Draw Indirect

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

    vulkan12Features.pNext = &vulkan13Features;
    void* pNextChain = &vulkan12Features;

    auto isExtensionEnabled = [&](const char* name)
    {
        for (const char* ext : enabledExtensions)
        {
            if (strcmp(ext, name) == 0) return true;
        }
        return false;
    };

    // Chain extensions starting from Vulkan 1.3 features
    void** lastPNext = &vulkan13Features.pNext;

    VkPhysicalDeviceMeshShaderFeaturesEXT meshShaderFeatures{};
    if (isExtensionEnabled(VK_EXT_MESH_SHADER_EXTENSION_NAME))
    {
        meshShaderFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_MESH_SHADER_FEATURES_EXT;
        meshShaderFeatures.meshShader = VK_TRUE;
        meshShaderFeatures.taskShader = VK_TRUE;
        *lastPNext = &meshShaderFeatures;
        lastPNext = &meshShaderFeatures.pNext;
    }

    VkPhysicalDeviceAccelerationStructureFeaturesKHR asFeatures{};
    if (isExtensionEnabled(VK_KHR_ACCELERATION_STRUCTURE_EXTENSION_NAME))
    {
        asFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_ACCELERATION_STRUCTURE_FEATURES_KHR;
        asFeatures.accelerationStructure = VK_TRUE;
        *lastPNext = &asFeatures;
        lastPNext = &asFeatures.pNext;
    }

    VkPhysicalDeviceRayTracingPipelineFeaturesKHR rtPipelineFeatures{};
    if (isExtensionEnabled(VK_KHR_RAY_TRACING_PIPELINE_EXTENSION_NAME))
    {
        rtPipelineFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_PIPELINE_FEATURES_KHR;
        rtPipelineFeatures.rayTracingPipeline = VK_TRUE;
        *lastPNext = &rtPipelineFeatures;
        lastPNext = &rtPipelineFeatures.pNext;
    }

    VkPhysicalDeviceRayQueryFeaturesKHR rayQueryFeatures{};
    if (isExtensionEnabled(VK_KHR_RAY_QUERY_EXTENSION_NAME))
    {
        rayQueryFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_QUERY_FEATURES_KHR;
        rayQueryFeatures.rayQuery = VK_TRUE;
        *lastPNext = &rayQueryFeatures;
        lastPNext = &rayQueryFeatures.pNext;
    }

    VkPhysicalDeviceRobustness2FeaturesEXT robustness2Features{};
    // for now in development, we don't enable this feature to prevent from hiding some issues
    // but in production, it should be enabled.
    if (false && isExtensionEnabled(VK_EXT_ROBUSTNESS_2_EXTENSION_NAME))
    {
        robustness2Features.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_ROBUSTNESS_2_FEATURES_EXT;
        robustness2Features.nullDescriptor = VK_TRUE;
        *lastPNext = &robustness2Features;
        lastPNext = &robustness2Features.pNext;
    }

    VkPhysicalDeviceFragmentShadingRateFeaturesKHR shadingRateFeatures{};
    if (isExtensionEnabled(VK_KHR_FRAGMENT_SHADING_RATE_EXTENSION_NAME))
    {
        shadingRateFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FRAGMENT_SHADING_RATE_FEATURES_KHR;
        shadingRateFeatures.attachmentFragmentShadingRate = VK_TRUE; // Enable attachment-based VRS
        shadingRateFeatures.primitiveFragmentShadingRate = VK_TRUE; // Enable primitive-based VRS
        shadingRateFeatures.pipelineFragmentShadingRate = VK_TRUE; // Enable pipeline-based VRS
        *lastPNext = &shadingRateFeatures;
        lastPNext = &shadingRateFeatures.pNext;
    }

    VkPhysicalDeviceDescriptorBufferFeaturesEXT descriptorBufferFeatures{};
    if (isExtensionEnabled(VK_EXT_DESCRIPTOR_BUFFER_EXTENSION_NAME))
    {
        descriptorBufferFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DESCRIPTOR_BUFFER_FEATURES_EXT;
        descriptorBufferFeatures.descriptorBuffer = VK_TRUE;
        *lastPNext = &descriptorBufferFeatures;
        lastPNext = &descriptorBufferFeatures.pNext;
    }

    *lastPNext = nullptr;

    // Buffer Device Address is core in Vulkan 1.2, so we enable it directly if we are targeting 1.2+
    vulkan12Features.bufferDeviceAddress = VK_TRUE;


    // Device Create Info
    VkDeviceCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;

    createInfo.pQueueCreateInfos = queueCreateInfos.data();
    createInfo.queueCreateInfoCount = static_cast<uint32_t>(queueCreateInfos.size());

    createInfo.pEnabledFeatures = &features;
    createInfo.pNext = pNextChain;

    createInfo.enabledExtensionCount = static_cast<uint32_t>(enabledExtensions.size());
    createInfo.ppEnabledExtensionNames = enabledExtensions.data();

    if (IsEnableValidation())
    {
        createInfo.enabledLayerCount = static_cast<uint32_t>(m_Settings.validationLayers.size());
        createInfo.ppEnabledLayerNames = m_Settings.validationLayers.data();
    }
    else
    {
        createInfo.enabledLayerCount = 0;
    }

    VkDevice device;
    VkResult res = vkCreateDevice(m_CurrentPhysicsDevice, &createInfo, nullptr, &device);
    if (res != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW(
            String::Format("[RHIVkInstance::CreateLogicDevice]: failed to create logical device! VkResult: %d", (int)res
            ));
    }

    Containers::Map<uint32_t, uint32_t> nextQueueIndex;
    auto getNextQueueIndex = [&](uint32_t family) -> uint32_t {
        uint32_t requested = requestedQueueCounts[family];
        uint32_t currentIndex = nextQueueIndex[family];
        uint32_t indexToUse = (std::min)(currentIndex, requested > 0 ? requested - 1 : 0);
        nextQueueIndex[family]++;
        return indexToUse;
    };

    VkQueue graphicQueue = VK_NULL_HANDLE;
    if (indices.graphicsFamily.has_value())
    {
        vkGetDeviceQueue(device, indices.graphicsFamily.value(), getNextQueueIndex(indices.graphicsFamily.value()), &graphicQueue);
    }

    VkQueue presentQueue = VK_NULL_HANDLE;
    if (windowId != ~0u && indices.presentFamily.has_value())
    {
        // For present queue, it generally shares exactly with graphics, but we just want one and any valid one usually works
        vkGetDeviceQueue(device, indices.presentFamily.value(), getNextQueueIndex(indices.presentFamily.value()), &presentQueue);
    }

    VkQueue computeQueue = VK_NULL_HANDLE;
    if (indices.computeFamily.has_value())
    {
        vkGetDeviceQueue(device, indices.computeFamily.value(), getNextQueueIndex(indices.computeFamily.value()), &computeQueue);
    }

    VkQueue transferQueue = VK_NULL_HANDLE;
    if (indices.transferFamily.has_value())
    {
        vkGetDeviceQueue(device, indices.transferFamily.value(), getNextQueueIndex(indices.transferFamily.value()), &transferQueue);
    }

    VkPhysicalDeviceMemoryProperties memoryProperties;
    vkGetPhysicalDeviceMemoryProperties(m_CurrentPhysicsDevice, &memoryProperties);

    auto logicalDevice = std::make_unique<RHIVkDevice>(this, rhiSurface, graphicQueue, presentQueue, computeQueue,
                                                       transferQueue, device, memoryProperties,
                                                       indices.graphicsFamily.value(),
                                                       indices.computeFamily.value_or(0),
                                                       indices.transferFamily.value_or(0),
                                                       indices.presentFamily.value_or(0));
    VkPhysicalDeviceProperties physicalProperties{};
    vkGetPhysicalDeviceProperties(m_CurrentPhysicsDevice, &physicalProperties);
    {
        logicalDevice->m_Capabilities.rayTracingSupported = isExtensionEnabled(VK_KHR_RAY_TRACING_PIPELINE_EXTENSION_NAME) ? 1 : 0;
        logicalDevice->m_Capabilities.supportsDynamicRendering = isExtensionEnabled(VK_KHR_DYNAMIC_RENDERING_EXTENSION_NAME) ? 1 : 0;

        logicalDevice->m_Capabilities.maxImageDimension1D = physicalProperties.limits.maxImageDimension1D;
        logicalDevice->m_Capabilities.maxImageDimension2D = physicalProperties.limits.maxImageDimension2D;
        logicalDevice->m_Capabilities.maxImageDimension3D = physicalProperties.limits.maxImageDimension3D;
        logicalDevice->m_Capabilities.maxImageDimensionCube = physicalProperties.limits.maxImageDimensionCube;
        logicalDevice->m_Capabilities.maxImageArrayLayers = physicalProperties.limits.maxImageArrayLayers;
        logicalDevice->m_Capabilities.maxTexelBufferElements = physicalProperties.limits.maxTexelBufferElements;
        logicalDevice->m_Capabilities.maxUniformBufferRange = physicalProperties.limits.maxUniformBufferRange;
        logicalDevice->m_Capabilities.maxStorageBufferRange = physicalProperties.limits.maxStorageBufferRange;
        logicalDevice->m_Capabilities.maxPushConstantsSize = physicalProperties.limits.maxPushConstantsSize;
        logicalDevice->m_Capabilities.maxMemoryAllocationCount = physicalProperties.limits.maxMemoryAllocationCount;
        logicalDevice->m_Capabilities.maxSamplerAllocationCount = physicalProperties.limits.maxSamplerAllocationCount;
        logicalDevice->m_Capabilities.bufferImageGranularity = physicalProperties.limits.bufferImageGranularity;
        logicalDevice->m_Capabilities.sparseAddressSpaceSize = physicalProperties.limits.sparseAddressSpaceSize;
        logicalDevice->m_Capabilities.maxBoundDescriptorSets = physicalProperties.limits.maxBoundDescriptorSets;
        logicalDevice->m_Capabilities.maxPerStageDescriptorSamplers = physicalProperties.limits.maxPerStageDescriptorSamplers;
        logicalDevice->m_Capabilities.maxPerStageDescriptorUniformBuffers = physicalProperties.limits.maxPerStageDescriptorUniformBuffers;
        logicalDevice->m_Capabilities.maxPerStageDescriptorStorageBuffers = physicalProperties.limits.maxPerStageDescriptorStorageBuffers;
        logicalDevice->m_Capabilities.maxPerStageDescriptorSampledImages = physicalProperties.limits.maxPerStageDescriptorSampledImages;
        logicalDevice->m_Capabilities.maxPerStageDescriptorStorageImages = physicalProperties.limits.maxPerStageDescriptorStorageImages;
        logicalDevice->m_Capabilities.maxPerStageDescriptorInputAttachments = physicalProperties.limits.maxPerStageDescriptorInputAttachments;
        logicalDevice->m_Capabilities.maxPerStageResources = physicalProperties.limits.maxPerStageResources;
        logicalDevice->m_Capabilities.maxDescriptorSetSamplers = physicalProperties.limits.maxDescriptorSetSamplers;
        logicalDevice->m_Capabilities.maxDescriptorSetUniformBuffers = physicalProperties.limits.maxDescriptorSetUniformBuffers;
        logicalDevice->m_Capabilities.maxDescriptorSetUniformBuffersDynamic = physicalProperties.limits.maxDescriptorSetUniformBuffersDynamic;
        logicalDevice->m_Capabilities.maxDescriptorSetStorageBuffers = physicalProperties.limits.maxDescriptorSetStorageBuffers;
        logicalDevice->m_Capabilities.maxDescriptorSetStorageBuffersDynamic = physicalProperties.limits.maxDescriptorSetStorageBuffersDynamic;
        logicalDevice->m_Capabilities.maxDescriptorSetSampledImages = physicalProperties.limits.maxDescriptorSetSampledImages;
        logicalDevice->m_Capabilities.maxDescriptorSetStorageImages = physicalProperties.limits.maxDescriptorSetStorageImages;
        logicalDevice->m_Capabilities.maxDescriptorSetInputAttachments = physicalProperties.limits.maxDescriptorSetInputAttachments;
        logicalDevice->m_Capabilities.maxVertexInputAttributes = physicalProperties.limits.maxVertexInputAttributes;
        logicalDevice->m_Capabilities.maxVertexInputBindings = physicalProperties.limits.maxVertexInputBindings;
        logicalDevice->m_Capabilities.maxVertexInputAttributeOffset = physicalProperties.limits.maxVertexInputAttributeOffset;
        logicalDevice->m_Capabilities.maxVertexInputBindingStride = physicalProperties.limits.maxVertexInputBindingStride;
        logicalDevice->m_Capabilities.maxVertexOutputComponents = physicalProperties.limits.maxVertexOutputComponents;
        logicalDevice->m_Capabilities.maxTessellationGenerationLevel = physicalProperties.limits.maxTessellationGenerationLevel;
        logicalDevice->m_Capabilities.maxTessellationPatchSize = physicalProperties.limits.maxTessellationPatchSize;
        logicalDevice->m_Capabilities.maxTessellationControlPerVertexInputComponents = physicalProperties.limits.maxTessellationControlPerVertexInputComponents;
        logicalDevice->m_Capabilities.maxTessellationControlPerVertexOutputComponents = physicalProperties.limits.maxTessellationControlPerVertexOutputComponents;
        logicalDevice->m_Capabilities.maxTessellationControlPerPatchOutputComponents = physicalProperties.limits.maxTessellationControlPerPatchOutputComponents;
        logicalDevice->m_Capabilities.maxTessellationControlTotalOutputComponents = physicalProperties.limits.maxTessellationControlTotalOutputComponents;
        logicalDevice->m_Capabilities.maxTessellationEvaluationInputComponents = physicalProperties.limits.maxTessellationEvaluationInputComponents;
        logicalDevice->m_Capabilities.maxTessellationEvaluationOutputComponents = physicalProperties.limits.maxTessellationEvaluationOutputComponents;
        logicalDevice->m_Capabilities.maxGeometryShaderInvocations = physicalProperties.limits.maxGeometryShaderInvocations;
        logicalDevice->m_Capabilities.maxGeometryInputComponents = physicalProperties.limits.maxGeometryInputComponents;
        logicalDevice->m_Capabilities.maxGeometryOutputComponents = physicalProperties.limits.maxGeometryOutputComponents;
        logicalDevice->m_Capabilities.maxGeometryOutputVertices = physicalProperties.limits.maxGeometryOutputVertices;
        logicalDevice->m_Capabilities.maxGeometryTotalOutputComponents = physicalProperties.limits.maxGeometryTotalOutputComponents;
        logicalDevice->m_Capabilities.maxFragmentInputComponents = physicalProperties.limits.maxFragmentInputComponents;
        logicalDevice->m_Capabilities.maxFragmentOutputAttachments = physicalProperties.limits.maxFragmentOutputAttachments;
        logicalDevice->m_Capabilities.maxFragmentDualSrcAttachments = physicalProperties.limits.maxFragmentDualSrcAttachments;
        logicalDevice->m_Capabilities.maxFragmentCombinedOutputResources = physicalProperties.limits.maxFragmentCombinedOutputResources;
        logicalDevice->m_Capabilities.maxComputeSharedMemorySize = physicalProperties.limits.maxComputeSharedMemorySize;
        logicalDevice->m_Capabilities.maxComputeWorkGroupCountX = physicalProperties.limits.maxComputeWorkGroupCount[0];
        logicalDevice->m_Capabilities.maxComputeWorkGroupCountY = physicalProperties.limits.maxComputeWorkGroupCount[1];
        logicalDevice->m_Capabilities.maxComputeWorkGroupCountZ = physicalProperties.limits.maxComputeWorkGroupCount[2];
        logicalDevice->m_Capabilities.maxComputeWorkGroupInvocations = physicalProperties.limits.maxComputeWorkGroupInvocations;
        logicalDevice->m_Capabilities.maxComputeWorkGroupSizeX = physicalProperties.limits.maxComputeWorkGroupSize[0];
        logicalDevice->m_Capabilities.maxComputeWorkGroupSizeY = physicalProperties.limits.maxComputeWorkGroupSize[1];
        logicalDevice->m_Capabilities.maxComputeWorkGroupSizeZ = physicalProperties.limits.maxComputeWorkGroupSize[2];
        logicalDevice->m_Capabilities.subPixelPrecisionBits = physicalProperties.limits.subPixelPrecisionBits;
        logicalDevice->m_Capabilities.subTexelPrecisionBits = physicalProperties.limits.subTexelPrecisionBits;
        logicalDevice->m_Capabilities.mipmapPrecisionBits = physicalProperties.limits.mipmapPrecisionBits;
        logicalDevice->m_Capabilities.maxDrawIndexedIndexValue = physicalProperties.limits.maxDrawIndexedIndexValue;
        logicalDevice->m_Capabilities.maxDrawIndirectCount = physicalProperties.limits.maxDrawIndirectCount;
        logicalDevice->m_Capabilities.maxSamplerLodBias = physicalProperties.limits.maxSamplerLodBias;
        logicalDevice->m_Capabilities.maxSamplerAnisotropy = physicalProperties.limits.maxSamplerAnisotropy;
        logicalDevice->m_Capabilities.maxViewports = physicalProperties.limits.maxViewports;
        logicalDevice->m_Capabilities.maxViewportDimensionsX = physicalProperties.limits.maxViewportDimensions[0];
        logicalDevice->m_Capabilities.maxViewportDimensionsY = physicalProperties.limits.maxViewportDimensions[1];
        logicalDevice->m_Capabilities.viewportBoundsRangeMin = physicalProperties.limits.viewportBoundsRange[0];
        logicalDevice->m_Capabilities.viewportBoundsRangeMax = physicalProperties.limits.viewportBoundsRange[1];
        logicalDevice->m_Capabilities.viewportSubPixelBits = physicalProperties.limits.viewportSubPixelBits;
        logicalDevice->m_Capabilities.minMemoryMapAlignment = physicalProperties.limits.minMemoryMapAlignment;
        logicalDevice->m_Capabilities.minTexelBufferOffsetAlignment = physicalProperties.limits.minTexelBufferOffsetAlignment;
        logicalDevice->m_Capabilities.minUniformBufferOffsetAlignment = physicalProperties.limits.minUniformBufferOffsetAlignment;
        logicalDevice->m_Capabilities.minStorageBufferOffsetAlignment = physicalProperties.limits.minStorageBufferOffsetAlignment;
        logicalDevice->m_Capabilities.minTexelOffset = physicalProperties.limits.minTexelOffset;
        logicalDevice->m_Capabilities.maxTexelOffset = physicalProperties.limits.maxTexelOffset;
        logicalDevice->m_Capabilities.minTexelGatherOffset = physicalProperties.limits.minTexelGatherOffset;
        logicalDevice->m_Capabilities.maxTexelGatherOffset = physicalProperties.limits.maxTexelGatherOffset;
        logicalDevice->m_Capabilities.minInterpolationOffset = physicalProperties.limits.minInterpolationOffset;
        logicalDevice->m_Capabilities.maxInterpolationOffset = physicalProperties.limits.maxInterpolationOffset;
        logicalDevice->m_Capabilities.subPixelInterpolationOffsetBits = physicalProperties.limits.subPixelInterpolationOffsetBits;
        logicalDevice->m_Capabilities.maxFramebufferWidth = physicalProperties.limits.maxFramebufferWidth;
        logicalDevice->m_Capabilities.maxFramebufferHeight = physicalProperties.limits.maxFramebufferHeight;
        logicalDevice->m_Capabilities.maxFramebufferLayers = physicalProperties.limits.maxFramebufferLayers;
        logicalDevice->m_Capabilities.framebufferColorSampleCounts = physicalProperties.limits.framebufferColorSampleCounts;
        logicalDevice->m_Capabilities.framebufferDepthSampleCounts = physicalProperties.limits.framebufferDepthSampleCounts;
        logicalDevice->m_Capabilities.framebufferStencilSampleCounts = physicalProperties.limits.framebufferStencilSampleCounts;
        logicalDevice->m_Capabilities.framebufferNoAttachmentsSampleCounts = physicalProperties.limits.framebufferNoAttachmentsSampleCounts;
        logicalDevice->m_Capabilities.maxColorAttachments = physicalProperties.limits.maxColorAttachments;
        logicalDevice->m_Capabilities.sampledImageColorSampleCounts = physicalProperties.limits.sampledImageColorSampleCounts;
        logicalDevice->m_Capabilities.sampledImageIntegerSampleCounts = physicalProperties.limits.sampledImageIntegerSampleCounts;
        logicalDevice->m_Capabilities.sampledImageDepthSampleCounts = physicalProperties.limits.sampledImageDepthSampleCounts;
        logicalDevice->m_Capabilities.sampledImageStencilSampleCounts = physicalProperties.limits.sampledImageStencilSampleCounts;
        logicalDevice->m_Capabilities.storageImageSampleCounts = physicalProperties.limits.storageImageSampleCounts;
        logicalDevice->m_Capabilities.maxSampleMaskWords = physicalProperties.limits.maxSampleMaskWords;
        logicalDevice->m_Capabilities.timestampComputeAndGraphics = physicalProperties.limits.timestampComputeAndGraphics;
        logicalDevice->m_Capabilities.timestampPeriod = physicalProperties.limits.timestampPeriod;
        logicalDevice->m_Capabilities.maxClipDistances = physicalProperties.limits.maxClipDistances;
        logicalDevice->m_Capabilities.maxCullDistances = physicalProperties.limits.maxCullDistances;
        logicalDevice->m_Capabilities.maxCombinedClipAndCullDistances = physicalProperties.limits.maxCombinedClipAndCullDistances;
        logicalDevice->m_Capabilities.discreteQueuePriorities = physicalProperties.limits.discreteQueuePriorities;
        logicalDevice->m_Capabilities.pointSizeRangeMin = physicalProperties.limits.pointSizeRange[0];
        logicalDevice->m_Capabilities.pointSizeRangeMax = physicalProperties.limits.pointSizeRange[1];
        logicalDevice->m_Capabilities.lineWidthRangeMin = physicalProperties.limits.lineWidthRange[0];
        logicalDevice->m_Capabilities.lineWidthRangeMax = physicalProperties.limits.lineWidthRange[1];
        logicalDevice->m_Capabilities.pointSizeGranularity = physicalProperties.limits.pointSizeGranularity;
        logicalDevice->m_Capabilities.lineWidthGranularity = physicalProperties.limits.lineWidthGranularity;
        logicalDevice->m_Capabilities.strictLines = physicalProperties.limits.strictLines;
        logicalDevice->m_Capabilities.standardSampleLocations = physicalProperties.limits.standardSampleLocations;
        logicalDevice->m_Capabilities.optimalBufferCopyOffsetAlignment = physicalProperties.limits.optimalBufferCopyOffsetAlignment;
        logicalDevice->m_Capabilities.optimalBufferCopyRowPitchAlignment = physicalProperties.limits.optimalBufferCopyRowPitchAlignment;
        logicalDevice->m_Capabilities.nonCoherentAtomSize = physicalProperties.limits.nonCoherentAtomSize;
        logicalDevice->m_Capabilities.supportDescriptorBuffer = isExtensionEnabled(VK_EXT_DESCRIPTOR_BUFFER_EXTENSION_NAME) ? 1 : 0;
    }

    LOG_INFO(String::Format("[RHIVkInstance::CreateLogicDevice]: Create Logical Device for surface %d", windowId));
    m_LogicalDevices.insert(
        {
            windowId,
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
            LOG_INFO(
                String::Format("[RHIVkInstance::~RHIVkInstance]: Waiting for Logical Device (surface %d) to idle", pair.
                    first));
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
        LOG_FATAL_AND_THROW(
            "[RHIVkInstance::InitLogicDevices]: Should pick a physical device first before init logical devices");
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

    LOG_DEBUG(
        String::Format("[RHIVkDevice::PickPhysicalDevice]: Picked gpu device : %s", m_DeviceProperties.deviceName));


    // initialize limit info
    {
        // sampler 
        m_Capabilities.maxSamplerAnisotropy = m_DeviceProperties.limits.maxSamplerAnisotropy;
    }
    // TODO: configurable physical device
    // TODO: if current physical device not adequate suitable swap chain, should repick one
    CheckSwapChainCapabilities();
}
