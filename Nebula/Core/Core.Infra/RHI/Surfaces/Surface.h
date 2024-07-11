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
		explicit Surface(u32&& id, Instance* instance): m_RenderSurfaceId(id), m_Instance(instance) { };
		virtual void* GetHandle() const = 0;
	protected:
		u32 m_RenderSurfaceId;
		Instance* m_Instance;
	
	};
}
