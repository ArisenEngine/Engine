using System.Runtime.InteropServices;

namespace ArisenEngine.ShaderLab
{
    public static class ShaderCompiler
    {
        public sealed class CompileOptions
        {
            public string Entry = "main";
            public string ShaderModel = "6_4";
            public string Target = "-spirv";
            public string TargetEnv = string.Empty;
            public string OptimizeLevel = "0";
            public IReadOnlyList<string> Defines = Array.Empty<string>();
            public IReadOnlyList<string> Includes = Array.Empty<string>();
            public bool? UseDXLayout = null;
            public string OutputPath = string.Empty;
        }

        public sealed class CompileResult
        {
            public bool Success;
            public byte[] Code = Array.Empty<byte>();
            public string Message = string.Empty;
            public string OutputPath = string.Empty;
        }

        public static CompileResult Compile(
            string inputPath,
            NativeRHI.EProgramStage stage,
            CompileOptions options)
        {
            unsafe
            {
                if (string.IsNullOrWhiteSpace(inputPath))
                    throw new ArgumentException("inputPath is required", nameof(inputPath));
                if (options == null)
                    throw new ArgumentNullException(nameof(options));

                EnsureDxcInitialized();

                string outputPath = string.IsNullOrWhiteSpace(options.OutputPath)
                    ? Path.ChangeExtension(Path.GetTempFileName(), ".spv")
                    : options.OutputPath;

                var defines = options.Defines ?? Array.Empty<string>();
                var includes = options.Includes ?? Array.Empty<string>();

                // Build wchar_t** arrays for defines/includes
                IntPtr[] defAlloc = Array.Empty<IntPtr>();
                IntPtr[] incAlloc = Array.Empty<IntPtr>();

                try
                {
                    if (defines.Count > 0)
                    {
                        defAlloc = new IntPtr[defines.Count];
                        for (int i = 0; i < defines.Count; i++)
                        {
                            var s = defines[i];
                            if (string.IsNullOrWhiteSpace(s)) continue;
                            defAlloc[i] = Marshal.StringToHGlobalUni(s);
                        }
                    }

                    if (includes.Count > 0)
                    {
                        incAlloc = new IntPtr[includes.Count];
                        for (int i = 0; i < includes.Count; i++)
                        {
                            var s = includes[i];
                            if (string.IsNullOrWhiteSpace(s)) continue;
                            incAlloc[i] = Marshal.StringToHGlobalUni(s);
                        }
                    }

                    bool ok;
                    if (defAlloc.Length == 0 && incAlloc.Length == 0)
                    {
                        ok = Arisen.Native.ShaderCompiler.ShaderCompilerAPI.CompileShaderFromFileSimple(
                            inputPath,
                            stage,
                            options.Entry ?? "main",
                            options.ShaderModel ?? "6_4",
                            options.Target ?? "-spirv",
                            options.TargetEnv ?? string.Empty,
                            options.OptimizeLevel ?? "0",
                            IntPtr.Zero, 0,
                            IntPtr.Zero, 0,
                            string.IsNullOrWhiteSpace(outputPath) ? null : outputPath,
                            options.UseDXLayout.HasValue && options.UseDXLayout.Value);
                    }
                    else
                    {
                        fixed (IntPtr* pDef = defAlloc.Length > 0 ? defAlloc : new IntPtr[1])
                        fixed (IntPtr* pInc = incAlloc.Length > 0 ? incAlloc : new IntPtr[1])
                        {
                            ok = Arisen.Native.ShaderCompiler.ShaderCompilerAPI.CompileShaderFromFileSimple(
                                inputPath,
                                stage,
                                options.Entry ?? "main",
                                options.ShaderModel ?? "6_4",
                                options.Target ?? "-spirv",
                                options.TargetEnv ?? string.Empty,
                                options.OptimizeLevel ?? "0",
                                defAlloc.Length > 0 ? (IntPtr)pDef : IntPtr.Zero, defines.Count,
                                incAlloc.Length > 0 ? (IntPtr)pInc : IntPtr.Zero, includes.Count,
                                string.IsNullOrWhiteSpace(outputPath) ? null : outputPath,
                                options.UseDXLayout.HasValue && options.UseDXLayout.Value);
                        }
                    }

                    var result = new CompileResult { Success = ok, OutputPath = outputPath };
                    if (!ok) return result;

                    try
                    {
                        if (File.Exists(outputPath))
                            result.Code = File.ReadAllBytes(outputPath);
                    }
                    catch
                    {
                    }

                    return result;
                }
                finally
                {
                    // free unmanaged string buffers
                    if (defAlloc.Length > 0)
                    {
                        for (int i = 0; i < defAlloc.Length; i++)
                            if (defAlloc[i] != IntPtr.Zero)
                                Marshal.FreeHGlobal(defAlloc[i]);
                    }

                    if (incAlloc.Length > 0)
                    {
                        for (int i = 0; i < incAlloc.Length; i++)
                            if (incAlloc[i] != IntPtr.Zero)
                                Marshal.FreeHGlobal(incAlloc[i]);
                    }
                }
            }
        }

        private static string[] ToStringArray(IReadOnlyList<string> list)
        {
            if (list == null || list.Count == 0) return Array.Empty<string>();
            var arr = new string[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i];
            return arr;
        }

        private static bool s_DxcInitialized;

        private static void EnsureDxcInitialized()
        {
            if (s_DxcInitialized) return;
            Arisen.Native.ShaderCompiler.ShaderCompilerAPI.InitDXC();
            s_DxcInitialized = true;
        }

        public static void ReleaseDXC()
        {
            if (!s_DxcInitialized) return;
            Arisen.Native.ShaderCompiler.ShaderCompilerAPI.ReleaseDXC();
            s_DxcInitialized = false;
        }
    }
}