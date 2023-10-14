#pragma once
#include <vulkan/vulkan.h>
#include "IContext.h"
#include <iostream>
#include <vector>

class VulkanApplication : public IContext
{
public:

	VulkanApplication(int width, int height);
	void OnInit() override;
	void OnUpdate() override;
	void OnRender() override;
	void OnDestroy() override;
	void OnResize() override;

protected:

private:
	void CreateVkInstance();
	bool CheckValidationLayerSupport();
	void InitDebugMessenger();
	VkInstance m_Instance;
	VkDebugUtilsMessengerEXT m_DebugMessenger;
	
	static std::vector<const char*> ExtensionNames;
	static std::vector<const char*> ValidationLayers;

	static VKAPI_ATTR VkBool32 VKAPI_CALL DebugCallback(
		VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity,
		VkDebugUtilsMessageTypeFlagsEXT messageType,
		const VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
		void* pUserData);
	
	static void PopulateDebugMessengerCreateInfo(VkDebugUtilsMessengerCreateInfoEXT& createInfo);

	static VkResult CreateDebugUtilsMessengerEXT(
		VkInstance instance,
		const VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo,
		const VkAllocationCallbacks* pAllocator,
		VkDebugUtilsMessengerEXT* pDebugMessenger);

	static void DestroyDebugUtilsMessengerEXT(
		VkInstance instance,
		VkDebugUtilsMessengerEXT debugMessenger,
		const VkAllocationCallbacks* pAllocator);
};

