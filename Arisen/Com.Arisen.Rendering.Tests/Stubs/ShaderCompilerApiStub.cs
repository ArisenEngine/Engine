using System.Runtime.InteropServices;

namespace Arisen.Native.ShaderCompiler;

public static class ShaderCompilerAPI
{
    public static void InitDXC()
    {
    }

    public static void ReleaseDXC()
    {
    }

    public static bool CompileShaderFromFileSimple(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputPath,
        Arisen.Native.RHI.EProgramStage stage,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string entry,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string shaderModel,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string target,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string targetEnv,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string optimizeLevel,
        IntPtr defines,
        int defineCount,
        IntPtr includes,
        int includeCount,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? outputPath,
        bool useDxLayout)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, new byte[] { 0x03, 0x02, 0x23, 0x07 });
        }

        return true;
    }
}
