#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Instance.h"

namespace NebulaEngine::RHI 
{
	class Surface 
	{
	public:
		NO_COPY_NO_MOVE_NO_DEFAULT(Surface)
		VIRTUAL_DECONSTRUCTOR(Surface)
		explicit Surface(u32&& id, Instance* instance): m_RenderWindowId(id), m_Instance(instance) { };
		virtual void* GetHandle() const = 0;
		virtual void InitSwapChain() = 0;
	protected:
		u32 m_RenderWindowId;
		Instance* m_Instance;
	
	};
}
