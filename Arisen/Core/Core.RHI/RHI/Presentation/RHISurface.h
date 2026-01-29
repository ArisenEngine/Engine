#pragma once
#include "RHISwapChain.h"
#include "Base/FoundationMinimal.h"
#include "../Core/RHIInstance.h"

namespace ArisenEngine::RHI 
{
	class RHISurface 
	{
	public:
		NO_COPY_NO_MOVE_NO_DEFAULT(RHISurface)
		virtual ~RHISurface() noexcept
		{
			m_RenderWindowId = InvalidID;
			m_Instance = nullptr;
		}
		explicit RHISurface(UInt32&& id, RHIInstance* instance): m_RenderWindowId(id), m_Instance(instance) { };
		virtual void* GetHandle() const = 0;
		virtual void InitSwapChain() = 0;

		virtual RHISwapChain* GetSwapChain() = 0;
	protected:
		UInt32 m_RenderWindowId;
		RHIInstance* m_Instance;
	
	};
}

