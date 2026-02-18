#pragma once
#include "RHISwapChain.h"
#include "Base/FoundationMinimal.h"
#include "../Core/RHIInstance.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI 
{
	class RHI_DLL RHISurface 
	{
	public:
		NO_COPY_NO_MOVE_NO_DEFAULT(RHISurface)
		virtual ~RHISurface() noexcept;
		explicit RHISurface(UInt32&& id, RHIInstance* instance);
		// TODO(CppSharp-P0): GetHandle() \u8fd4\u56de void*\uff0c\u6cc4\u6f0f VkSurfaceKHR\u3002\u5e94\u79fb\u81f3\u540e\u7aef\u6216\u5220\u9664\u3002\r\n		virtual void* GetHandle() const = 0;
		virtual void InitSwapChain() = 0;

		virtual RHISwapChain* GetSwapChain() = 0;
	protected:
		UInt32 m_RenderWindowId;
		RHIInstance* m_Instance;
	
	};
}

