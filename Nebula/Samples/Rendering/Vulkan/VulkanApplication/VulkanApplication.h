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
	VkInstance instance;
	
	static std::vector<const char*> ExtensionNames;
	static std::vector<const char*> ValidationLayers;
	static VKAPI_ATTR VkBool32 VKAPI_CALL DebugCallback(
		VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity,
		VkDebugUtilsMessageTypeFlagsEXT messageType,
		const VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
		void* pUserData);
};

