using ArisenEngine.ShaderLab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StageEnum = ArisenBinding.NativePlatform.ArisenEngine.RHI.ProgramStage;

namespace ArisenEngineTest.Shaders;

public class ShaderProcessor
{
    public static void ParseShader(string path, string fileName)
    {
        var fullPath = Path.Combine(path, fileName);
        if (File.Exists(fullPath))
        {
            var shaderContent = File.ReadAllText(fullPath);
            var shaderLabParser = new ShaderLabParser(shaderContent, Path.GetDirectoryName(fullPath) ?? path);
            var shaderLabShader = shaderLabParser.ParseGraphicsShader();
            var metaPath = Path.Combine(path, fileName + ".yaml");
            var shaderDir = Path.GetDirectoryName(fullPath) ?? path;
            // Serialize after parser's internal rewrite so YAML contains absolute content root
            Serialization.SerializationUtil.Serialize(shaderLabShader, metaPath);
            Console.WriteLine($"Parse shader:{fileName}, output:{metaPath}");
            for (int si = 0; si < shaderLabShader.subShaders.Count; si++)
            {
                var sub = shaderLabShader.subShaders[si];
                for (int pi = 0; pi < sub.passes.Count; pi++)
                {
                    var pass = sub.passes[pi];
                    if (string.IsNullOrWhiteSpace(pass.hlslCode))
                        continue;

                    // Determine shader model from target like "ps_6_8" → "6_8" (fallback "6_4")
                    string shaderModel = "6_4";
                    if (!string.IsNullOrWhiteSpace(pass.target) && pass.target.Contains('_'))
                    {
                        var idx = pass.target.LastIndexOf('_');
                        if (idx >= 0 && idx + 1 < pass.target.Length)
                            shaderModel = pass.target.Substring(idx + 1);
                    }

                    // Build include directories
                    var includeDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool hasRoot = false;
                    foreach (var inc in pass.includedHLSLs)
                    {
                        try
                        {
                            var abs = Path.IsPathRooted(inc) ? inc : Path.GetFullPath(Path.Combine(shaderDir, inc));
                            if (!string.IsNullOrWhiteSpace(abs)) { includeDirs.Add(abs); hasRoot = true; }
                        }
                        catch { }
                    }
                    if (!hasRoot)
                    {
                        includeDirs.Add(shaderDir);
                    }

                    // Create an HLSL file alongside the original shader to keep relative includes stable
                    var tempHlsl = Path.Combine(shaderDir, $"{Path.GetFileNameWithoutExtension(fileName)}_sub{si}_pass{pi}.hlsl");
                    File.WriteAllText(tempHlsl, pass.hlslCode);

                    // Stages to compile for this pass
                    var stageToEntry = new List<(StageEnum stage, string entry, string tag)>();
                    void addIf(string? entry, StageEnum stage, string tag)
                    {
                        if (!string.IsNullOrWhiteSpace(entry)) stageToEntry.Add((stage, entry!, tag));
                    }
                    // Prefer explicit entries; also check parsed passStages map
                    addIf(pass.vertexEntry, StageEnum.Vertex, "vs");
                    addIf(pass.fragmentEntry, StageEnum.Fragment, "ps");
                    addIf(pass.geometryEntry, StageEnum.Geometry, "gs");
                    addIf(pass.hullEntry, StageEnum.Hull, "hs");
                    addIf(pass.domainEntry, StageEnum.Domain, "ds");

                    foreach (var kv in pass.passStages)
                    {
                        switch (kv.Key)
                        {
                            case PassStage.Vertex: addIf(kv.Value, StageEnum.Vertex, "vs"); break;
                            case PassStage.Fragment: addIf(kv.Value, StageEnum.Fragment, "ps"); break;
                            case PassStage.Geometry: addIf(kv.Value, StageEnum.Geometry, "gs"); break;
                            case PassStage.Hull: addIf(kv.Value, StageEnum.Hull, "hs"); break;
                            case PassStage.Domain: addIf(kv.Value, StageEnum.Domain, "ds"); break;
                        }
                    }

                    // Deduplicate by stage
                    stageToEntry = stageToEntry
                        .GroupBy(x => x.stage)
                        .Select(g => g.First())
                        .ToList();

                    foreach (var (stage, entry, tag) in stageToEntry)
                    {
                        var outSpv = Path.Combine(shaderDir, $"{Path.GetFileNameWithoutExtension(fileName)}_sub{si}_pass{pi}_{tag}.spv");
                        var options = new ShaderCompiler.CompileOptions
                        {
                            Entry = entry,
                            ShaderModel = shaderModel,
                            Target = "-spirv",
                            TargetEnv = "vulkan1.3",
                            OptimizeLevel = "3",
                            Includes = includeDirs.ToArray(),
                            Defines = new[] { "SHADER_API_VULKAN" },
                            OutputPath = outSpv,
                            UseDXLayout = true
                        };

                        var result = ShaderCompiler.Compile(tempHlsl, stage, options);
                        Console.WriteLine(result.Success
                            ? $"Compiled: {outSpv} ({result.Code?.Length ?? 0} bytes)"
                            : $"Compile failed for {fileName} [{tag}] : {result.Message}");
                    }
                }
            }
        }
    }
}