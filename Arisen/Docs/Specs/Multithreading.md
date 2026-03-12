# Multithreading & Job System Spec

Arisen Engine is fully multithreaded and follows a "Jobified" execution model to saturate high-core-count CPUs.

## 1. The Job System
The Engine uses a Job System involving a Directed Acyclic Graph (DAG) for scheduling work units.

- **Workers**: A fixed pool of worker threads (typically `Environment.ProcessorCount - 1`).
- **Dependencies**: Systems declare their Read/Write dependencies on ComponentPools. The scheduler uses this to determine which jobs can run in parallel safely.

## 2. Thread Safety Rules
In a Data-Oriented simulation, thread safety is achieved via **Data Partitioning** rather than traditional locks.

- **Read Access**: Multiple systems can read the same component data concurrently.
- **Write Access**: Only one system can write to a specific component type at a time, OR systems must operate on disjoint sets of entities (indices).
- **Global State**: **NEVER** access or mutate static global variables from within a Job. All data must be passed via the Job structure or Component Pools.

## 3. Sync Points
When a system needs to perform an operation that structuraly changes the world (e.g., `CreateEntity`, `DestroyEntity`, `AddComponent`), it **MUST** record these as commands (Buffer) to be executed at a deterministic "Sync Point" (usually at the end of a frame or between major phases).
