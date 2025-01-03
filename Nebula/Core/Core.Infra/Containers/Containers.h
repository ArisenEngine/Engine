#pragma once

#define USE_STL_VECTOR 1
#define USE_STL_MAP 1
#define USE_STL_SET 1
#define USE_STL_UNORDER_SET 1
#include <unordered_map>

#if USE_STL_VECTOR

#include<vector>
namespace NebulaEngine::Containers
{
	template<class TVector>
	using Vector = std::vector<TVector>;
}

#endif


#if USE_STL_MAP

#include<map>

namespace NebulaEngine::Containers
{
	template<class TMapKey, class TMapValue>
	using Map = std::map<TMapKey, TMapValue>;

	template<class TMapKey, class TMapValue>
	using Multimap = std::multimap<TMapKey, TMapValue>;

	template<class TMapKey, class TMapValue>
	using UnorderedMap = std::unordered_map<TMapKey, TMapValue>;
}

#endif;


#if USE_STL_SET

#include<set>
namespace NebulaEngine::Containers
{
	template<class TSet>
	using Set = std::set<TSet>;
}

#endif


#if USE_STL_UNORDER_SET
#include <unordered_set>

namespace NebulaEngine::Containers
{
	template<class TSet>
	using UnorderSet = std::unordered_set<TSet>;
}


#endif

// todo: implement my own verison of containers
namespace NebulaEngine::Containers
{
	

	
}