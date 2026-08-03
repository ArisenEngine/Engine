using System.Text.RegularExpressions;
using static Com.Arisen.Rendering.Tests.CppSourceContractScanner;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RhiAbiBoundarySourceContractTests
{
    private const string BridgeDirectory =
        "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.RHI/Bridges";

    private static readonly HashSet<string> s_OwnerTypes = new(StringComparer.Ordinal)
    {
        "RHIInstance",
        "RHIDevice",
        "RHIFactory",
        "RHISurface",
        "RHISwapChain",
        "SwapChain",
        "RHIQueue",
        "RHICommandBufferPool",
        "RHICommandBuffer",
        "RHIPipelineCache",
        "RHIPipelineState",
        "RHIDescriptorPool"
    };

    private static readonly HashSet<string> s_NonEnumIntegerParameters = new(StringComparer.Ordinal)
    {
        "RHICommandBuffer_DrawIndexed:vertexOffset",
        "RHIFactory_CreateSampler:anisotropyEnable",
        "RHIFactory_CreateSampler:compareEnable",
        "RHIInstance_PickPhysicalDevice:considerSurface",
        "RHILoader_CreateInstance:validationLayer",
        "RHIPipelineState_SetColorBlendState:blendEnable",
        "RHIPipelineState_SetDepthStencilState:depthTestEnable",
        "RHIPipelineState_SetDepthStencilState:depthWriteEnable",
        "RHIPipelineState_SetInputAssemblyState:primitiveRestart"
    };

    private static readonly HashSet<string> s_ExplicitFlagParameters = new(StringComparer.Ordinal)
    {
        "RHICommandBuffer_CopyImageToBuffer2D:srcImageAspect",
        "RHICommandBuffer_PipelineBarrier:dependency",
        "RHICommandBuffer_PushConstants:stageFlags",
        "RHIFactory_CreateBuffer:createFlagBits",
        "RHIFactory_CreateBuffer:usage",
        "RHIFactory_CreateImage:usage",
        "RHIFactory_CreateImageView:aspectMask",
        "RHIPipelineState_SetDynamicStateMask:mask"
    };

    private static readonly HashSet<string> s_EnumArrayParameters = new(StringComparer.Ordinal)
    {
        "RHIDescriptorPool_AddPool:types",
        "RHIPipelineState_SetRenderingFormats:colorFormats"
    };

    [Fact]
    public void EveryHandwrittenExportUsesOneGuardAndMatchingCatchBoundary()
    {
        string bridgeDirectory = Path.Combine(FindRepoRoot(), BridgeDirectory);
        string[] bridgeFiles = Directory.GetFiles(
                bridgeDirectory,
                "*.cpp",
                SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(bridgeFiles);

        var violations = new List<string>();
        int exportCount = 0;

        foreach (string bridgeFile in bridgeFiles)
        {
            string source = MaskCommentsAndLiterals(File.ReadAllText(bridgeFile));
            IReadOnlyList<ExportFunction> exports = ParseExports(source, bridgeFile);
            exportCount += exports.Count;

            foreach (ExportFunction export in exports)
            {
                string body = source[export.BodyStart..(export.BodyEnd + 1)];
                int guardCount = CountInvocations(body, "RHI_ABI_GUARD");
                int voidCatchCount = CountInvocations(body, "RHI_ABI_CATCH_VOID");
                int returnCatchCount = CountInvocations(body, "RHI_ABI_CATCH_RETURN");
                bool returnsVoid = export.ReturnType == "void";
                int matchingCatchCount = returnsVoid ? voidCatchCount : returnCatchCount;
                int wrongCatchCount = returnsVoid ? returnCatchCount : voidCatchCount;

                if (guardCount != 1 || matchingCatchCount != 1 || wrongCatchCount != 0)
                {
                    string expectedCatch = returnsVoid
                        ? "RHI_ABI_CATCH_VOID"
                        : "RHI_ABI_CATCH_RETURN";
                    violations.Add(
                        $"{Path.GetFileName(bridgeFile)}:{export.Line} {export.Name} " +
                        $"requires one RHI_ABI_GUARD and one {expectedCatch}; found " +
                        $"guard={guardCount}, voidCatch={voidCatchCount}, " +
                        $"returnCatch={returnCatchCount}.");
                }
            }
        }

        Assert.True(exportCount > 0, $"No RHI_DLL exports were found under '{bridgeDirectory}'.");
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ExportPolicyInventoryExactlyMatchesTheHandwrittenAbiSurface()
    {
        IReadOnlyList<ParsedExport> exports = ReadExports();
        var violations = new List<string>();
        var actualNames = exports
            .Select(static item => item.Function.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (ParsedExport export in exports)
        {
            if (!RhiAbiExportPolicyInventory.All.TryGetValue(export.Function.Name, out var policy))
            {
                violations.Add(
                    $"{Path.GetFileName(export.Path)}:{export.Function.Line} " +
                    $"{export.Function.Name} has no ABI input policy.");
                continue;
            }

            if (!string.Equals(policy.FileName, Path.GetFileName(export.Path), StringComparison.Ordinal))
            {
                violations.Add(
                    $"{export.Function.Name} is implemented by '{Path.GetFileName(export.Path)}' " +
                    $"but its ABI policy declares '{policy.FileName}'.");
            }
        }

        foreach (RhiAbiExportPolicy policy in RhiAbiExportPolicyInventory.All.Values)
        {
            if (!actualNames.Contains(policy.ExportName))
            {
                violations.Add(
                    $"ABI input policy '{policy.ExportName}' has no handwritten export.");
            }
        }

        Assert.Equal(160, exports.Count);
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryOwnerAndRawPointerParameterHasAnExplicitNullPolicy()
    {
        var violations = new List<string>();

        foreach (ParsedExport export in ReadExports())
        {
            RhiAbiExportPolicy policy = RhiAbiExportPolicyInventory.All[export.Function.Name];
            string body = export.Source[export.Function.BodyStart..(export.Function.BodyEnd + 1)];
            var pointerNames = export.Function.Parameters
                .Where(static parameter => parameter.IsPointerLike)
                .Select(static parameter => parameter.Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string optionalPointer in policy.OptionalPointers)
            {
                if (!pointerNames.Contains(optionalPointer))
                {
                    violations.Add(
                        $"{policy.ExportName} declares unknown optional pointer '{optionalPointer}'.");
                }
            }

            foreach (ExportParameter parameter in export.Function.Parameters.Where(
                         static parameter => parameter.IsPointerLike))
            {
                string typeName = NormalizePointerType(parameter.Type);
                bool isOwner = s_OwnerTypes.Contains(typeName);
                bool isOptional = policy.OptionalPointers.Contains(parameter.Name);
                bool hasPointerCheck = HasMacroArgument(
                    body,
                    "RHI_ABI_REQUIRE_POINTER",
                    parameter.Name);
                bool hasArrayCheck = HasMacroArgument(
                    body,
                    "RHI_ABI_REQUIRE_ARRAY",
                    parameter.Name);

                if (isOwner && !hasPointerCheck)
                {
                    violations.Add(
                        $"{Path.GetFileName(export.Path)}:{export.Function.Line} " +
                        $"{export.Function.Name} must resolve owner '{parameter.Name}' directly.");
                }
                else if (!isOwner && !isOptional && !hasPointerCheck && !hasArrayCheck)
                {
                    violations.Add(
                        $"{Path.GetFileName(export.Path)}:{export.Function.Line} " +
                        $"{export.Function.Name} has no required/conditional null check for " +
                        $"'{parameter.Name}'.");
                }
                else if (isOptional && (hasPointerCheck || hasArrayCheck))
                {
                    violations.Add(
                        $"{export.Function.Name} declares '{parameter.Name}' optional but " +
                        "unconditionally validates it as required.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EverySemanticInputPolicyHasValidationEvidenceBeforeDispatch()
    {
        var violations = new List<string>();

        foreach (ParsedExport export in ReadExports())
        {
            RhiAbiExportPolicy policy = RhiAbiExportPolicyInventory.All[export.Function.Name];
            string body = export.Source[export.Function.BodyStart..(export.Function.BodyEnd + 1)];

            RequireEvidence(
                policy,
                RhiAbiInputPolicy.Handle,
                body,
                @"\b(?:Require\w*Handle|RequireBufferOffset|RequireDescriptorPool|" +
                @"RequireDescriptorSet|ThrowInvalidHandle|IsAlive)\s*\(",
                "generation-qualified handle",
                export,
                violations);
            RequireEvidence(
                policy,
                RhiAbiInputPolicy.EnumOrFlags,
                body,
                @"\b(?:ABI::RequireEnum|ABI::RequireFlags|ABI::RequireMask)\s*(?:<[^>]+>)?\s*\(|" +
                @"\b(?:GraphicsAPI|RHIQueueType)::",
                "enum/flag",
                export,
                violations);
            RequireEvidence(
                policy,
                RhiAbiInputPolicy.RangeOrIdentity,
                body,
                @"\b(?:ThrowInvalidParameter|ThrowInvalidState|RequireSubmittedTicket|RequirePool|" +
                @"RequireBufferOffset|RequireDescriptorPool|RequireDescriptorSet|RequireFinite|" +
                @"ResolveCompatiblePipelineState|IsBufferRangeValid|IsPushConstantRangeValid)\s*\(",
                "range/identity",
                export,
                violations);
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void TypedAndSplitGenerationHandlesHaveParameterSpecificPolicy()
    {
        var violations = new List<string>();

        foreach (ParsedExport export in ReadExports())
        {
            RhiAbiExportPolicy policy = RhiAbiExportPolicyInventory.All[export.Function.Name];
            string body = export.Source[export.Function.BodyStart..(export.Function.BodyEnd + 1)];

            foreach (ExportParameter parameter in export.Function.Parameters)
            {
                bool isTypedHandle = !parameter.IsPointerLike &&
                    parameter.Type.EndsWith("Handle", StringComparison.Ordinal);
                bool isSplitGeneration = !parameter.IsPointerLike &&
                    (parameter.Name.EndsWith("Generation", StringComparison.Ordinal) ||
                     parameter.Name.EndsWith("Gen", StringComparison.Ordinal));

                if ((isTypedHandle || isSplitGeneration) &&
                    !policy.InputPolicy.HasFlag(RhiAbiInputPolicy.Handle))
                {
                    violations.Add(
                        $"{export.Function.Name}:{parameter.Name} is generation-qualified but " +
                        "the export policy is not marked H.");
                }

                if (!isTypedHandle)
                {
                    continue;
                }

                string pattern =
                    $@"\b(?:Require\w*Handle|RequireBufferOffset|RequireDescriptorPool|" +
                    $@"RequireDescriptorSet)\s*\([^;]*\b{Regex.Escape(parameter.Name)}\b";
                if (!Regex.IsMatch(body, pattern, RegexOptions.CultureInvariant))
                {
                    violations.Add(
                        $"{Path.GetFileName(export.Path)}:{export.Function.Line} " +
                        $"{export.Function.Name} does not validate typed handle '{parameter.Name}'.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryScalarEnumAndFlagParameterIsValidatedByName()
    {
        var violations = new List<string>();

        foreach (ParsedExport export in ReadExports())
        {
            RhiAbiExportPolicy policy = RhiAbiExportPolicyInventory.All[export.Function.Name];
            string body = export.Source[export.Function.BodyStart..(export.Function.BodyEnd + 1)];

            foreach (ExportParameter parameter in export.Function.Parameters)
            {
                string key = $"{export.Function.Name}:{parameter.Name}";
                bool isAbiIntegerEnum = parameter.Type == "int" &&
                    !s_NonEnumIntegerParameters.Contains(key);
                bool isExplicitFlag = s_ExplicitFlagParameters.Contains(key);
                bool isEnumArray = s_EnumArrayParameters.Contains(key);
                if (!isAbiIntegerEnum && !isExplicitFlag && !isEnumArray)
                {
                    continue;
                }

                if (!policy.InputPolicy.HasFlag(RhiAbiInputPolicy.EnumOrFlags))
                {
                    violations.Add(
                        $"{key} is an enum/flag input but the export policy is not marked E.");
                    continue;
                }

                string validatorPattern =
                    $@"\b(?:ABI::RequireEnum|ABI::RequireFlags|ABI::RequireMask)" +
                    $@"\s*(?:<[^>]+>)?\s*\([^;]*\b{Regex.Escape(parameter.Name)}\b";
                bool usesValidator = Regex.IsMatch(
                    body,
                    validatorPattern,
                    RegexOptions.CultureInvariant);
                bool usesExplicitEnumBounds = body.Contains(parameter.Name, StringComparison.Ordinal) &&
                    (body.Contains("GraphicsAPI::", StringComparison.Ordinal) ||
                     body.Contains("RHIQueueType::", StringComparison.Ordinal));
                if (!usesValidator && !usesExplicitEnumBounds)
                {
                    violations.Add(
                        $"{Path.GetFileName(export.Path)}:{export.Function.Line} " +
                        $"{export.Function.Name} does not validate enum/flag '{parameter.Name}'.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static IReadOnlyList<ParsedExport> ReadExports()
    {
        string bridgeDirectory = Path.Combine(FindRepoRoot(), BridgeDirectory);
        var exports = new List<ParsedExport>();

        foreach (string bridgeFile in Directory.GetFiles(
                     bridgeDirectory,
                     "*.cpp",
                     SearchOption.TopDirectoryOnly).OrderBy(static path => path, StringComparer.Ordinal))
        {
            string source = MaskCommentsAndLiterals(File.ReadAllText(bridgeFile));
            exports.AddRange(ParseExports(source, bridgeFile).Select(
                function => new ParsedExport(bridgeFile, source, function)));
        }

        return exports;
    }

    private static bool HasMacroArgument(string body, string macro, string parameter)
    {
        return Regex.IsMatch(
            body,
            $@"\b{Regex.Escape(macro)}\s*\(\s*{Regex.Escape(parameter)}\s*(?:,|\))",
            RegexOptions.CultureInvariant);
    }

    private static string NormalizePointerType(string type)
    {
        return type
            .Replace("const", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("[]", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static void RequireEvidence(
        RhiAbiExportPolicy policy,
        RhiAbiInputPolicy requiredPolicy,
        string body,
        string evidencePattern,
        string evidenceName,
        ParsedExport export,
        List<string> violations)
    {
        if (!policy.InputPolicy.HasFlag(requiredPolicy) ||
            Regex.IsMatch(body, evidencePattern, RegexOptions.CultureInvariant))
        {
            return;
        }

        violations.Add(
            $"{Path.GetFileName(export.Path)}:{export.Function.Line} " +
            $"{export.Function.Name} declares {evidenceName} input but has no validation evidence.");
    }

    private sealed record ParsedExport(
        string Path,
        string Source,
        ExportFunction Function);

}
