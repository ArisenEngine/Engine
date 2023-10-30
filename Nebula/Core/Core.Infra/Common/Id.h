#pragma once

#include "CommandHeaders.h"
namespace NebulaEngine::ID
{
	using IdType = u32;

	namespace Internal {

		constexpr u32 GenerationBits{ 8 };
		constexpr u32 IndexBits{ sizeof(IdType) * 8 - GenerationBits };
		constexpr IdType IndexMask{ (IdType{1u} << IndexBits) - 1 };
		constexpr IdType GenerationMask{ (IdType{1u} << GenerationBits) - 1 };

	}

	constexpr IdType InvalidID{ IdType{static_cast <IdType>( - 1)}};
	  
	using GenerationType = std::conditional_t< Internal::GenerationBits <= 16, std::conditional_t< Internal::GenerationBits <= 8, u8, u16>, u32>;
	static_assert(sizeof(GenerationType) * 8 >= Internal:: GenerationBits);
	static_assert(sizeof(IdType) - sizeof(GenerationType) > 0);


	inline bool IsValid(IdType id)
	{
		return id != InvalidID;
	}

	inline IdType Index(IdType id)
	{
		IdType index = id & Internal::IndexMask;
		assert(index != Internal::IndexMask);
		return index;
	}

	inline IdType Generation(IdType id)
	{
		return (id >> Internal::IndexBits) & Internal::GenerationMask;
	}

	inline IdType NewGeneration(IdType id)
	{
		const IdType generation{ ID::Generation(id) + 1 };
		assert(generation < ((u64)1 << Internal::GenerationBits) - 1); // eg: 255
		return ID::Index(id) | (generation << Internal::IndexBits);
	}

#if _DEBUG

	namespace Internal
	{
		struct IdBase
		{
			constexpr explicit IdBase(IdType id) : m_Id{id}{}
			constexpr operator IdType() const { return m_Id; }
		private:
			IdType m_Id;
		};
	}

#define DEFINE_TYPED_ID(name)                              \
          struct name final : ID::Internal::IdBase         \
          {                                                \
               constexpr explicit name(ID::IdType id)      \
                      : IdBase { id } {}                   \
               constexpr name() : IdBase { 0 } {} \
	      };                                               \


#else
#define DEFINE_TYPED_ID(name) using name = ID::IdType;
#endif

}