#pragma once
#include "SynchObject.h"

namespace ArisenEngine::Threadable
{
class ScopeLock
{
public:

	/**
	 * Constructor that performs a lock on the synchronization object
	 *
	 * @param InSynchObject The synchronization object to manage
	 */
	[[nodiscard]] ScopeLock(SynchObject* InSynchObject )
		: SynchObject(InSynchObject)
	{
		assert(SynchObject);
		SynchObject->Lock();
	}

	/** Destructor that performs a release on the synchronization object. */
	~ScopeLock()
	{
		Unlock();
	}

	void Unlock()
	{
		if(SynchObject)
		{
			SynchObject->Unlock();
			SynchObject = nullptr;
		}
	}

private:

	/** Default constructor (hidden on purpose). */
	ScopeLock();

	/** Copy constructor( hidden on purpose). */
	ScopeLock(const ScopeLock& InScopeLock);

	/** Assignment operator (hidden on purpose). */
	ScopeLock& operator=( ScopeLock& InScopeLock )
	{
		return *this;
	}

private:

	// Holds the synchronization object to aggregate and scope manage.
	SynchObject* SynchObject;
};
	
class ScopeUnlock
{
public:

	/**
	 * Constructor that performs a unlock on the synchronization object
	 *
	 * @param InSynchObject The synchronization object to manage, can be null.
	 */
	[[nodiscard]] ScopeUnlock(SynchObject* InSynchObject)
		: SynchObject(InSynchObject)
	{
		if (InSynchObject)
		{
			InSynchObject->Unlock();
		}
	}

	/** Destructor that performs a lock on the synchronization object. */
	~ScopeUnlock()
	{
		if (SynchObject)
		{
			SynchObject->Lock();
		}
	}
private:

	/** Default constructor (hidden on purpose). */
	ScopeUnlock();

	/** Copy constructor( hidden on purpose). */
	ScopeUnlock(const ScopeUnlock& InScopeLock);

	/** Assignment operator (hidden on purpose). */
	ScopeUnlock& operator=(ScopeUnlock& InScopeLock)
	{
		return *this;
	}

private:

	// Holds the synchronization object to aggregate and scope manage.
	SynchObject* SynchObject;
};
    
}
