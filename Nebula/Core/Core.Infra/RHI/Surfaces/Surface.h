#pragma once
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI 
{
	class Surface 
	{

	public:
		NO_COPY_NO_MOVE_NO_DEFAULT(Surface)
		VIRTUAL_DECONSTRUCTOR(Surface)
		explicit Surface(u32&& id, std::shared_ptr<Instance> instance): m_RenderSurfaceId(id), m_Instance(instance) { };
		
	protected:
		u32 m_RenderSurfaceId;
		std::shared_ptr<Instance> m_Instance;
	
	};
}