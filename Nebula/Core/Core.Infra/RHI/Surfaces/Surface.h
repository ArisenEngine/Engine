#pragma once
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI 
{
	class Surface 
	{

	protected:
		u32 m_RenderSurfaceId;
	public:
		NO_COPY_NO_MOVE_NO_DEFAULT(Surface)
		VIRTUAL_DECONSTRUCTOR(Surface)
		explicit Surface(const u32 id): m_RenderSurfaceId(id) { };
	};
}