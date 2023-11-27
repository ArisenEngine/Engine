#pragma once

namespace NebulaEngine::RHI
{
	 enum class GraphsicsAPI
	{
	 	None                             = 0,
		Vulkan							 ,
		DirectX12						 ,
		Metal							 ,
		OpenGL							 ,
		Mali_Simulator					 ,
		PowerVR_Simulator				 
	};
}