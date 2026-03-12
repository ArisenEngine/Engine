# Interop & Bindings Spec

## Binding Generator
`BindingGenerator` is a separate executable tool that parses the Arisen Engine `Core` headers (C++) looking for specific macro tags (e.g. `ARISEN_CLASS`, `ARISEN_PROPERTY`) and automatically generates the corresponding C# `[DllImport]` or `LibraryImport` wrappers in the `AutoBinding` directory.

- **NEVER** edit files located in the `AutoBinding` directory by hand. They will be overwritten on the next build.
- If you need a C++ function exposed to C#, modify the C++ header file, add the necessary macros, and rerun the binding generator.

## Struct Packing and ABI
When writing types that cross the boundary:
- All structures passed between C++ and C# **MUST** be 100% blittable. Do not use strings, arrays, or objects directly.
- On the C# side, decorate structs with `[StructLayout(LayoutKind.Sequential)]`.
- For strings, pass memory pointers (e.g., UTF8 `byte*`) and use `Marshal` or `Utf8StringMarshaller` depending on context.

## Batch Processing
The C#/C++ boundary has a cost.
- Do **NOT** PInvoke functions per-entity (e.g., `Transform.SetPosition(x, y)` inside a loop of 10,000 entities). 
- Instead, fill up a NativeArray or `Span<T>` in C#, pin it, and pass a pointer to C++ to process the entire chunk of entities at once.
