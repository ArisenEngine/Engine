#pragma once

#define USE_STL_VECTOR 1;
#define USE_STL_MAP 1;

#if USE_STL_VECTOR

#include<vector>
namespace NebulaEngine::Containers
{
	template<class TVector>
	using vector = std::vector<TVector>;
}

#endif


#if USE_STL_MAP

#include<map>

namespace NebulaEngine::Containers
{
	template<class TMapKey, class TMapValue>
	using map = std::map<TMapKey, TMapValue>;
}

#endif;


// todo: implement my own verison of containers
namespace NebulaEngine::Containers
{
	

	
}