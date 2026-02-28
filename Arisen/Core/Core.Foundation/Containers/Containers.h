#pragma once
#include "../Base/StandardHeaders.h"

namespace ArisenEngine::Containers
{
    template <class TVector>
    using Vector = std::vector<TVector>;

    template <class TMapKey, class TMapValue>
    using Map = std::map<TMapKey, TMapValue>;

    template <class TMapKey, class TMapValue>
    using Multimap = std::multimap<TMapKey, TMapValue>;

    template <class TMapKey, class TMapValue>
    using UnorderedMap = std::unordered_map<TMapKey, TMapValue>;

    template <class TSet>
    using Set = std::set<TSet>;

    template <class TSet>
    using UnorderSet = std::unordered_set<TSet>;
}
