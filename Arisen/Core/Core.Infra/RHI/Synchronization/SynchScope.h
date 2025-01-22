#pragma once
#include "SynchObject.h"

namespace ArisenEngine::RHI
{
    /**
 * Implements a scope lock.
 *
 * This is a utility class that handles scope level locking. It's very useful
 * to keep from causing deadlocks due to exceptions being caught and knowing
 * about the number of locks a given thread has on a resource. Example:
 *
 * <code>
 *	{
 *		// Synchronize thread access to the following data
 *		FScopeLock ScopeLock(SynchObject);
 *		// Access data that is shared among multiple threads
 *		...
 *		// When ScopeLock goes out of scope, other threads can access data
 *	}
 * </code>
 */
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

/**
 * Implements a scope unlock.
 *
 * This is a utility class that handles scope level unlocking. It's very useful
 * to allow access to a protected object when you are sure it can happen.
 * Example:
 *
 * <code>
 *	{
 *		// Access data that is shared among multiple threads
 *		FScopeUnlock ScopeUnlock(SynchObject);
 *		...
 *		// When ScopeUnlock goes out of scope, other threads can no longer access data
 *	}
 * </code>
 */
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
