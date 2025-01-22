#pragma once
#include "SwapChain.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Instance.h"

namespace ArisenEngine::RHI 
{
	class Surface 
	{
	public:
		NO_COPY_NO_MOVE_NO_DEFAULT(Surface)
		virtual ~Surface() noexcept
		{
			m_RenderWindowId = InvalidID;
			m_Instance = nullptr;
		}
		explicit Surface(UInt32&& id, Instance* instance): m_RenderWindowId(id), m_Instance(instance) { };
		virtual void* GetHandle() const = 0;
		virtual void InitSwapChain() = 0;

		virtual SwapChain* GetSwapChain() = 0;
	protected:
		UInt32 m_RenderWindowId;
		Instance* m_Instance;
	
	};
}
