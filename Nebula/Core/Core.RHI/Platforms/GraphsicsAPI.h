#pragma once
namespace RHI
{
	enum class GraphsicsAPI
	{
		Vulkan							 = 0,
		DirectX12						 = 1,
		Metal							 = 2,
		OpenGL							 = 3,
		Mali_Simulator					 = 4,
		PowerVR_Simulator				 = 5
	};
}