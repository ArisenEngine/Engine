#pragma once
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE

template<typename ScalarT>
class Range
{
public:
    Range() = default;
    Range(ScalarT start, ScalarT end)
        : m_start(start)
        , m_end(end)
    {
        assert(m_start <= m_end && "range start must be less of equal than end");
    }

    Range(std::initializer_list<ScalarT> init) // NOSONAR - initializer list constructor is not explicit intentionally
        : Range(*init.begin(), *(init.begin() + 1))
    { }

    [[nodiscard]] friend bool operator==(const Range& left, const Range& right) noexcept = default;

    [[nodiscard]] friend bool operator< (const Range& left, const Range& right) noexcept // NOSONAR - can not define <=>
    {
        return left.m_end  <= right.m_start;
    }

    [[nodiscard]] friend bool operator> (const Range& left, const Range& right) noexcept // NOSONAR - can not define <=>
    {
        return left.m_start > right.m_end;
    }

    [[nodiscard]] ScalarT GetStart() const noexcept  { return m_start; }
    [[nodiscard]] ScalarT GetEnd() const noexcept    { return m_end; }
    [[nodiscard]] ScalarT GetMin() const noexcept    { return m_start; }
    [[nodiscard]] ScalarT GetMax() const noexcept    { return m_end; }
    [[nodiscard]] ScalarT GetLength() const noexcept { return m_end - m_start; }
    [[nodiscard]] bool    IsEmpty() const noexcept   { return m_start == m_end; }

    [[nodiscard]] bool    IsAdjacent(const Range& other) const noexcept       { return m_start == other.m_end   || other.m_start == m_end; }
    [[nodiscard]] bool    IsOverlapping(const Range& other) const noexcept    { return m_start <  other.m_end   && other.m_start <  m_end; }
    [[nodiscard]] bool    IsMergeable(const Range& other) const noexcept      { return m_start <= other.m_end   && other.m_start <= m_end; }
    [[nodiscard]] bool    Contains(const Range& other) const noexcept         { return m_start <= other.m_start && other.m_end   <= m_end; }

    [[nodiscard]]
    friend Range operator+(const Range& left, const Range& right) // merge
    {
        assert(left.IsMergeable(right) && "can not merge ranges which are not overlapping or adjacent");
        return Range(std::min(left.m_start, right.m_start), std::max(left.m_end, right.m_end));
    }

    friend Range operator%(const Range& left, const Range& right) // intersect
    {
        assert(left.IsMergeable(right) && "can not intersect ranges which are not overlapping or adjacent");
        return Range(std::max(left.m_start, right.m_start), std::min(left.m_end, right.m_end));
    }

    [[nodiscard]]
    friend Range operator-(const Range& left, const Range& right) // subtract
    {
        assert(left.IsOverlapping(right) && "can not subtract ranges which are not overlapping");
        assert(!left.Contains(right) && !right.Contains(left) && "can not subtract ranges containing one another");
        return (left.m_start <= right.m_start) ? Range(left.m_start, right.m_start) : Range(right.m_end, left.m_end);
    }

    [[nodiscard]] explicit operator bool() const noexcept        { return !IsEmpty(); }
    [[nodiscard]] explicit operator std::string() const noexcept { return std::format("[{}, {})", m_start, m_end); }

private:
    ScalarT m_start{};
    ScalarT m_end  {};
};

template<typename ScalarT>
class RangeSet
{
public:
    using BaseSet  = std::set<Range<ScalarT>>;
    using Iterator = typename BaseSet::iterator;
    using ConstIterator = typename BaseSet::const_iterator;

    RangeSet() = default;
    RangeSet(std::initializer_list<Range<ScalarT>> init) noexcept : m_container(init) { } //NOSONAR - initializer list constructor is not explicit intentionally

    [[nodiscard]] friend bool operator==(const RangeSet&, const RangeSet&) noexcept = default;

    [[nodiscard]] friend bool operator==(const RangeSet& left, const BaseSet& right) noexcept
    {
        return left.m_container == right;
    }

    RangeSet<ScalarT>& operator=(std::initializer_list<Range<ScalarT>> init) noexcept
    {
        for (const Range<ScalarT>& range : init)
            Add(range);
        return *this;
    }

    [[nodiscard]] size_t Size() const noexcept              { return m_container.size();  }
    [[nodiscard]] bool   IsEmpty() const noexcept           { return m_container.empty(); }
    [[nodiscard]] const BaseSet& GetRanges() const noexcept { return *this; }
    [[nodiscard]] ConstIterator begin() const noexcept      { return m_container.begin(); }
    [[nodiscard]] ConstIterator end() const noexcept        { return m_container.end(); }

    void Clear() noexcept
    {
        m_container.clear();
    }

    void Add(const Range<ScalarT>& range)
    {
        Range<ScalarT> merged_range(range);
        const RangeOfRanges ranges = GetMergeableRanges(range);

        Ranges remove_ranges;
        for (auto range_it = ranges.first; range_it != ranges.second; ++range_it)
        {
            merged_range = merged_range + *range_it;
            remove_ranges.emplace_back(*range_it);
        }

        RemoveRanges(remove_ranges);
        m_container.insert(merged_range);
    }

    void Remove(const Range<ScalarT>& range)
    {
        Ranges remove_ranges;
        Ranges add_ranges;
        RangeOfRanges ranges = GetMergeableRanges(range);
        for (auto range_it = ranges.first; range_it != ranges.second; ++range_it)
        {
            if (!range.IsOverlapping(*range_it))
                continue;

            remove_ranges.push_back(*range_it);

            if (range.Contains(*range_it))
                continue;

            if (range_it->Contains(range))
            {
                if (const Range<ScalarT> left_sub_range(range_it->GetStart(), range.GetStart());
                    !left_sub_range.IsEmpty())
                {
                    add_ranges.emplace_back(left_sub_range);
                }

                if (const Range<ScalarT> right_sub_range(range.GetEnd(), range_it->GetEnd());
                    !right_sub_range.IsEmpty())
                {
                    add_ranges.emplace_back(right_sub_range);
                }
            }
            else if (Range<ScalarT> trimmed_range = *range_it - range;
                    !trimmed_range.IsEmpty())
            {
                add_ranges.emplace_back(trimmed_range);
            }
        }

        RemoveRanges(remove_ranges);
        AddRanges(add_ranges);
    }

private:
    using RangeOfRanges = std::pair<ConstIterator, ConstIterator>;

    [[nodiscard]]
    RangeOfRanges GetMergeableRanges(const Range<ScalarT>& range)
    {
        if (m_container.empty())
        {
            return RangeOfRanges{ m_container.end(), m_container.end() };
        }

        RangeOfRanges mergeable_ranges{
            m_container.lower_bound(Range<ScalarT>(range.GetStart(), range.GetStart())),
            m_container.upper_bound(range)
        };

        if (mergeable_ranges.first != m_container.begin())
            mergeable_ranges.first--;

        while (mergeable_ranges.first != m_container.end() && !range.IsMergeable(*mergeable_ranges.first))
            mergeable_ranges.first++;

        if (mergeable_ranges.first == m_container.end())
            return RangeOfRanges(m_container.end(), m_container.end());

        while (mergeable_ranges.second != mergeable_ranges.first &&
              (mergeable_ranges.second == m_container.end() || !range.IsMergeable(*mergeable_ranges.second)))
        {
            mergeable_ranges.second--;
        }
        mergeable_ranges.second++;

        return mergeable_ranges;
    }

    using Ranges = std::vector<Range<ScalarT>>;
    inline void RemoveRanges(const Ranges& delete_ranges) noexcept
    {
        for (const Range<ScalarT>& delete_range : delete_ranges)
        {
            m_container.erase(delete_range);
        }
    }

    inline void AddRanges(const Ranges& add_ranges)
    {
        for(const Range<ScalarT>& add_range : add_ranges)
        {
            m_container.insert(add_range);
        }
    }

    std::set<Range<ScalarT>> m_container;
};
ARISENRHI_END_NAMESPACE
