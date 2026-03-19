#pragma once

// ============================================================================
// Arisen Engine Binding Annotation Macros
// ============================================================================
//
// These macros expand to NOTHING at compile time — zero impact on C++ builds.
// They serve as TEXT MARKERS for the BindingGenerator tool to scan and produce
// clean C# P/Invoke code without any post-processing.
//
// Usage:
//   // At file scope — declare which DLL and C# namespace this file maps to
//   ARISEN_BIND_MODULE("Core.RHI.dll")
//   ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")
//
//   // Before an enum declaration
//   ARISEN_BIND_ENUM(EFormat)
//   enum class EFormat : uint32_t { UNDEFINED = 0, ... };
//
//   // Before a blittable POD struct
//   ARISEN_BIND_STRUCT(RHIBufferDescriptor)
//   struct RHIBufferDescriptor { uint32_t createFlagBits; ... };
//
//   // Around extern "C" bridge functions
//   ARISEN_BIND_BEGIN_BRIDGE("RHIDevice", "Core.RHI.dll", "Arisen.Native.RHI")
//   extern "C" {
//       RHI_DLL void RHIDevice_DeviceWaitIdle(RHIDevice* device);
//   }
//   ARISEN_BIND_END_BRIDGE()
//
// ============================================================================

/// Specifies the native DLL name for [DllImport] in generated C# code.
/// Place once per header file, before any bindable declarations.
#define ARISEN_BIND_MODULE(dll_name)

/// Specifies the target Package ID for generated code.
/// The BindingGenerator groups generated files into Packages/{package_id}/Generated.
/// Place once per header file, typically alongside ARISEN_BIND_MODULE.
#define ARISEN_BIND_PACKAGE(package_id)

/// Specifies the C# namespace for generated code.
/// Place once per header file, after ARISEN_BIND_MODULE.
#define ARISEN_BIND_NAMESPACE(cs_namespace)

/// Marks an enum for C# code generation.
/// Place immediately before the enum declaration.
#define ARISEN_BIND_ENUM(name)

/// Marks a blittable POD struct for C# code generation.
/// The struct must be LayoutKind.Sequential-compatible (no pointers to managed types).
/// Place immediately before the struct declaration.
#define ARISEN_BIND_STRUCT(name)

/// Marks a typed RHI handle struct for C# code generation.
/// Generates an 8-byte blittable struct with Index, Generation, IsValid, and Invalid sentinel.
/// Place immediately before the typedef or using declaration.
#define ARISEN_BIND_HANDLE(name)

/// Marks the beginning of a block of extern "C" bridge functions.
/// @param class_name   Suffix for the generated C# static class (e.g. "RHIDevice" -> RHIDeviceAPI)
/// @param dll_name     DLL name for [DllImport]
/// @param cs_namespace C# namespace
#define ARISEN_BIND_BEGIN_BRIDGE(class_name, dll_name, cs_namespace)

/// Marks the end of a bridge function block.
#define ARISEN_BIND_END_BRIDGE()
