using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class AssetReferenceGeneratorService
{
    private static readonly HashSet<string> s_IgnoredSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".axaml",
        ".csproj",
        ".sln",
        ".props",
        ".targets",
        ".meta"
    };

    public static void Generate(string csprojDir, string projectName, PackageInfo package)
    {
        string generatedDir = Path.Combine(csprojDir, "Generated");
        string className = GetAssetRefsClassName(projectName, package);
        string outputPath = Path.Combine(generatedDir, $"{className}.g.cs");
        var assets = DiscoverAssets(package.DirectoryPath);

        if (assets.Count == 0)
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            return;
        }

        Directory.CreateDirectory(generatedDir);
        string namespaceName = GetGeneratedNamespace(projectName, package);
        bool emitTypedRefs = CanReferenceCoreAssets(package.Manifest);
        string source = RenderSource(namespaceName, className, package.Manifest.Id, assets, emitTypedRefs);
        File.WriteAllText(outputPath, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Logger.Info($"Generated AssetRefs: {outputPath}");
    }

    private static List<GeneratedAssetRef> DiscoverAssets(string packageDir)
    {
        string assetsDir = Path.Combine(packageDir, "Assets");
        if (!Directory.Exists(assetsDir))
        {
            return new List<GeneratedAssetRef>();
        }

        var metas = Directory
            .EnumerateFiles(assetsDir, "*.meta", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(assetsDir, path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var discovered = new List<DiscoveredAsset>(metas.Count);
        var seenGuids = new Dictionary<Guid, string>();

        foreach (string metaPath in metas)
        {
            string sourcePath = metaPath[..^".meta".Length];
            string sourceExtension = Path.GetExtension(sourcePath);
            if (s_IgnoredSourceExtensions.Contains(sourceExtension))
            {
                continue;
            }

            var metadata = ReadSimpleMetadata(metaPath);
            if (!metadata.TryGetValue("Guid", out string? guidValue) ||
                !Guid.TryParse(guidValue, out Guid guid) ||
                guid == Guid.Empty)
            {
                throw new InvalidOperationException($"Asset meta file '{metaPath}' is missing a valid Guid.");
            }

            string relativeSourcePath = Path.GetRelativePath(assetsDir, sourcePath).Replace('\\', '/');
            if (seenGuids.TryGetValue(guid, out string? existingPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate asset Guid '{guid}' in package assets: '{existingPath}' and '{relativeSourcePath}'.");
            }

            seenGuids.Add(guid, relativeSourcePath);
            metadata.TryGetValue("AssetType", out string? assetType);
            metadata.TryGetValue("Importer", out string? importer);
            if (IsDependencyOnlyAsset(assetType ?? string.Empty, importer ?? string.Empty))
            {
                continue;
            }

            discovered.Add(new DiscoveredAsset(
                guid,
                sourcePath,
                relativeSourcePath,
                Path.GetFileNameWithoutExtension(sourcePath),
                assetType ?? string.Empty,
                importer ?? string.Empty));
        }

        var assetsByGuid = discovered.ToDictionary(asset => asset.Guid);
        var parsed = new List<GeneratedAssetRef>(discovered.Count);
        foreach (var asset in discovered)
        {
            var materialRefs = string.Equals(asset.AssetType, "Material", StringComparison.OrdinalIgnoreCase)
                ? DiscoverMaterialRefs(asset.SourcePath, assetsByGuid)
                : GeneratedMaterialRefs.Empty;
            parsed.Add(new GeneratedAssetRef(
                asset.Guid,
                asset.RelativeSourcePath,
                asset.BaseName,
                asset.AssetType,
                asset.Importer,
                materialRefs));
        }

        return AssignConstantNames(parsed);
    }

    private static bool IsDependencyOnlyAsset(string assetType, string importer)
    {
        return string.Equals(assetType, "AssetDependency", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(importer, "GltfBufferDependency", StringComparison.OrdinalIgnoreCase);
    }

    private static GeneratedMaterialRefs DiscoverMaterialRefs(
        string sourcePath,
        IReadOnlyDictionary<Guid, DiscoveredAsset> assetsByGuid)
    {
        if (!File.Exists(sourcePath))
        {
            return GeneratedMaterialRefs.Empty;
        }

        var textureSlots = new List<string>();
        var scalarProperties = new List<string>();
        var vector4Properties = new List<string>();
        string section = string.Empty;
        bool pendingNamedItem = false;
        Guid shaderGuid = Guid.Empty;

        foreach (string rawLine in File.ReadLines(sourcePath))
        {
            string lineWithoutComment = StripComment(rawLine);
            string trimmed = lineWithoutComment.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!char.IsWhiteSpace(lineWithoutComment[0]) && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                section = trimmed[..^1].Trim();
                pendingNamedItem = false;
                continue;
            }

            if (string.Equals(section, "Shader", StringComparison.OrdinalIgnoreCase) &&
                TryReadGuidYamlValue(trimmed, out Guid parsedShaderGuid))
            {
                shaderGuid = parsedShaderGuid;
                continue;
            }

            if (!IsMaterialRefSection(section))
            {
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                pendingNamedItem = true;
                string inline = trimmed[2..].Trim();
                if (TryReadNamedYamlValue(inline, out string inlineName))
                {
                    AddMaterialRef(section, inlineName, textureSlots, scalarProperties, vector4Properties);
                    pendingNamedItem = false;
                }

                continue;
            }

            if (pendingNamedItem && TryReadNamedYamlValue(trimmed, out string name))
            {
                AddMaterialRef(section, name, textureSlots, scalarProperties, vector4Properties);
                pendingNamedItem = false;
            }
        }

        if (shaderGuid != Guid.Empty &&
            assetsByGuid.TryGetValue(shaderGuid, out var shaderAsset) &&
            string.Equals(shaderAsset.AssetType, "ShaderSource", StringComparison.OrdinalIgnoreCase))
        {
            DiscoverShaderMaterialContractAnnotations(
                shaderAsset.SourcePath,
                textureSlots,
                scalarProperties,
                vector4Properties);
        }

        return new GeneratedMaterialRefs(
            Deduplicate(textureSlots),
            Deduplicate(scalarProperties),
            Deduplicate(vector4Properties));
    }

    private static bool IsMaterialRefSection(string section)
    {
        return string.Equals(section, "Texture2DRefs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "ScalarProperties", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "Vector4Properties", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadNamedYamlValue(string line, out string value)
    {
        value = string.Empty;
        int separator = line.IndexOf(':');
        if (separator <= 0)
        {
            return false;
        }

        string key = line[..separator].Trim();
        if (!string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = line[(separator + 1)..].Trim().Trim('"', '\'');
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadGuidYamlValue(string line, out Guid value)
    {
        value = Guid.Empty;
        int separator = line.IndexOf(':');
        if (separator <= 0)
        {
            return false;
        }

        string key = line[..separator].Trim();
        if (!string.Equals(key, "Guid", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rawValue = line[(separator + 1)..].Trim().Trim('"', '\'');
        return Guid.TryParse(rawValue, out value) && value != Guid.Empty;
    }

    private static void DiscoverShaderMaterialContractAnnotations(
        string shaderSourcePath,
        List<string> textureSlots,
        List<string> scalarProperties,
        List<string> vector4Properties)
    {
        if (!File.Exists(shaderSourcePath))
        {
            return;
        }

        string source = File.ReadAllText(shaderSourcePath);
        int lineNumber = 0;
        using var reader = new StringReader(source);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            DiscoverShaderMaterialContractAnnotation(
                shaderSourcePath,
                lineNumber,
                line,
                textureSlots,
                scalarProperties,
                vector4Properties);
        }

        DiscoverShaderLabMaterialContractBlock(
            shaderSourcePath,
            source,
            textureSlots,
            scalarProperties,
            vector4Properties);
    }

    private static void DiscoverShaderMaterialContractAnnotation(
        string shaderSourcePath,
        int lineNumber,
        string line,
        List<string> textureSlots,
        List<string> scalarProperties,
        List<string> vector4Properties)
    {
        const string annotationPrefix = "@arisen.material.";
        int annotationIndex = line.IndexOf(annotationPrefix, StringComparison.OrdinalIgnoreCase);
        if (annotationIndex < 0)
        {
            return;
        }

        string annotation = line[annotationIndex..].Trim();
        int separator = annotation.IndexOfAny(new[] { ' ', '\t', ':', '=' });
        if (separator <= annotationPrefix.Length)
        {
            throw new InvalidOperationException(
                $"Shader material contract annotation in '{shaderSourcePath}' line {lineNumber} is incomplete.");
        }

        string kind = annotation[annotationPrefix.Length..separator].Trim();
        string name = annotation[(separator + 1)..].Trim().Trim('"', '\'');
        int trailingSeparator = name.IndexOfAny(new[] { ' ', '\t', '/', '*' });
        if (trailingSeparator >= 0)
        {
            name = name[..trailingSeparator].Trim();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                $"Shader material contract annotation in '{shaderSourcePath}' line {lineNumber} is missing a binding name.");
        }

        if (string.Equals(kind, "texture2d", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "texture", StringComparison.OrdinalIgnoreCase))
        {
            textureSlots.Add(name);
            return;
        }

        if (string.Equals(kind, "scalar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "float", StringComparison.OrdinalIgnoreCase))
        {
            scalarProperties.Add(name);
            return;
        }

        if (string.Equals(kind, "vector4", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "float4", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "color", StringComparison.OrdinalIgnoreCase))
        {
            vector4Properties.Add(name);
            return;
        }

        throw new InvalidOperationException(
            $"Shader material contract annotation in '{shaderSourcePath}' line {lineNumber} uses unsupported kind '{kind}'.");
    }

    private static void DiscoverShaderLabMaterialContractBlock(
        string shaderSourcePath,
        string source,
        List<string> textureSlots,
        List<string> scalarProperties,
        List<string> vector4Properties)
    {
        int keywordIndex = source.IndexOf("MaterialContract", StringComparison.OrdinalIgnoreCase);
        if (keywordIndex < 0)
        {
            return;
        }

        int blockStart = source.IndexOf('{', keywordIndex);
        if (blockStart < 0)
        {
            throw new InvalidOperationException(
                $"ShaderLab material contract block in '{shaderSourcePath}' is missing '{{'.");
        }

        int blockEnd = FindMatchingBrace(source, blockStart);
        if (blockEnd < 0)
        {
            throw new InvalidOperationException(
                $"ShaderLab material contract block in '{shaderSourcePath}' is missing closing '}}'.");
        }

        string block = source.Substring(blockStart + 1, blockEnd - blockStart - 1);
        using var reader = new StringReader(block);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = StripLineComment(line).Trim().TrimEnd(';', ',');
            if (trimmed.Length == 0)
            {
                continue;
            }

            string[] parts = trimmed
                .Split(new[] { ' ', '\t', ':', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            AddShaderMaterialContractName(
                shaderSourcePath,
                parts[0],
                parts[1].Trim('"', '\''),
                textureSlots,
                scalarProperties,
                vector4Properties);
        }
    }

    private static int FindMatchingBrace(string source, int blockStart)
    {
        int depth = 0;
        for (int i = blockStart; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string StripLineComment(string line)
    {
        int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }

    private static void AddShaderMaterialContractName(
        string shaderSourcePath,
        string kind,
        string name,
        List<string> textureSlots,
        List<string> scalarProperties,
        List<string> vector4Properties)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                $"ShaderLab material contract block in '{shaderSourcePath}' has an empty binding name.");
        }

        if (string.Equals(kind, "Texture2D", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "Texture", StringComparison.OrdinalIgnoreCase))
        {
            textureSlots.Add(name);
            return;
        }

        if (string.Equals(kind, "Scalar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "Float", StringComparison.OrdinalIgnoreCase))
        {
            scalarProperties.Add(name);
            return;
        }

        if (string.Equals(kind, "Vector4", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "Float4", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "Color", StringComparison.OrdinalIgnoreCase))
        {
            vector4Properties.Add(name);
            return;
        }

        throw new InvalidOperationException(
            $"ShaderLab material contract block in '{shaderSourcePath}' uses unsupported kind '{kind}'.");
    }

    private static void AddMaterialRef(
        string section,
        string name,
        List<string> textureSlots,
        List<string> scalarProperties,
        List<string> vector4Properties)
    {
        if (string.Equals(section, "Texture2DRefs", StringComparison.OrdinalIgnoreCase))
        {
            textureSlots.Add(name);
            return;
        }

        if (string.Equals(section, "ScalarProperties", StringComparison.OrdinalIgnoreCase))
        {
            scalarProperties.Add(name);
            return;
        }

        if (string.Equals(section, "Vector4Properties", StringComparison.OrdinalIgnoreCase))
        {
            vector4Properties.Add(name);
        }
    }

    private static IReadOnlyList<string> Deduplicate(List<string> values)
    {
        if (values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, string> ReadSimpleMetadata(string metaPath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in File.ReadLines(metaPath))
        {
            string line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim().Trim('"', '\'');
            values[key] = value;
        }

        return values;
    }

    private static string StripComment(string line)
    {
        int commentIndex = line.IndexOf('#');
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }

    private static List<GeneratedAssetRef> AssignConstantNames(List<GeneratedAssetRef> assets)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<GeneratedAssetRef>(assets.Count);
        foreach (var asset in assets)
        {
            string baseName = SanitizeIdentifier(PathUtils.ToPascalCase(asset.BaseName));
            string typeSuffix = GetAssetTypeSuffix(asset.AssetType);
            string typedBaseName = ShouldAppendTypeSuffix(baseName, typeSuffix)
                ? $"{baseName}{typeSuffix}"
                : baseName;
            string name = $"{typedBaseName}Guid";

            if (usedNames.Contains(name))
            {
                name = $"{SanitizeIdentifier(PathUtils.ToPascalCase(Path.ChangeExtension(asset.RelativeSourcePath, null).Replace('/', '-')))}Guid";
            }

            string uniqueName = name;
            int suffix = 2;
            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{name}{suffix++}";
            }

            result.Add(asset with { ConstantName = uniqueName });
        }

        return result;
    }

    private static string GetAssetTypeSuffix(string assetType)
    {
        return assetType.Trim().ToLowerInvariant() switch
        {
            "shader" or "shadersource" => "Shader",
            "texture" or "texture2d" => "Texture",
            "mesh" => "Mesh",
            "material" => "Material",
            _ => SanitizeIdentifier(PathUtils.ToPascalCase(assetType))
        };
    }

    private static bool ShouldAppendTypeSuffix(string baseName, string typeSuffix)
    {
        return !string.IsNullOrWhiteSpace(typeSuffix) &&
               !baseName.EndsWith(typeSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderSource(
        string namespaceName,
        string className,
        string packageId,
        IReadOnlyList<GeneratedAssetRef> assets,
        bool emitTypedRefs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// Generated by ArisenBuildTool from package Assets/*.meta files.");
        builder.AppendLine("using System;");
        if (emitTypedRefs)
        {
            builder.AppendLine("using ArisenEngine.Core.Assets;");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {namespaceName};");
        builder.AppendLine();
        builder.AppendLine($"internal static class {className}");
        builder.AppendLine("{");
        builder.AppendLine($"    public const string PackageId = \"{EscapeString(packageId)}\";");
        builder.AppendLine();

        foreach (var asset in assets)
        {
            builder.AppendLine($"    // {EscapeComment(asset.RelativeSourcePath)} | Type: {EscapeComment(asset.AssetType)} | Importer: {EscapeComment(asset.Importer)}");
            builder.AppendLine($"    public static readonly Guid {asset.ConstantName} = Guid.Parse(\"{asset.Guid:D}\");");
            string? typedAssetName = emitTypedRefs ? GetTypedAssetMarkerName(asset.AssetType) : null;
            if (!string.IsNullOrWhiteSpace(typedAssetName))
            {
                string refName = GetAssetRefName(asset);
                builder.AppendLine($"    public static readonly AssetRef<{typedAssetName}> {refName} = new({asset.ConstantName}, \"{EscapeString(GetCanonicalAssetType(asset.AssetType))}\", PackageId);");
            }
        }

        foreach (var asset in assets.Where(asset => asset.MaterialRefs.HasAny || (emitTypedRefs && !string.IsNullOrWhiteSpace(GetTypedAssetMarkerName(asset.AssetType)))))
        {
            builder.AppendLine();
            RenderNestedAssetClass(builder, asset, emitTypedRefs);
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static bool CanReferenceCoreAssets(PackageManifest manifest)
    {
        if (string.Equals(manifest.Id, "com.arisen.core", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return manifest.Dependencies?.ContainsKey("com.arisen.core") == true;
    }

    private static void RenderNestedAssetClass(StringBuilder builder, GeneratedAssetRef asset, bool emitTypedRefs)
    {
        string className = GetNestedAssetClassName(asset);
        builder.AppendLine($"    public static class {className}");
        builder.AppendLine("    {");
        builder.AppendLine($"        public static readonly Guid Guid = {asset.ConstantName};");
        string? typedAssetName = emitTypedRefs ? GetTypedAssetMarkerName(asset.AssetType) : null;
        if (!string.IsNullOrWhiteSpace(typedAssetName))
        {
            string refName = GetAssetRefName(asset);
            builder.AppendLine($"        public static readonly AssetRef<{typedAssetName}> Ref = {refName};");
        }

        RenderStringConstants(builder, "Texture2DSlots", asset.MaterialRefs.Texture2DSlots, indent: "        ");
        RenderStringConstants(builder, "ScalarProperties", asset.MaterialRefs.ScalarProperties, indent: "        ");
        RenderStringConstants(builder, "Vector4Properties", asset.MaterialRefs.Vector4Properties, indent: "        ");
        builder.AppendLine("    }");
    }

    private static string GetAssetRefName(GeneratedAssetRef asset)
    {
        const string guidSuffix = "Guid";
        string name = asset.ConstantName;
        if (name.EndsWith(guidSuffix, StringComparison.Ordinal))
        {
            name = name[..^guidSuffix.Length];
        }

        return $"{SanitizeIdentifier(name)}Ref";
    }

    private static string? GetTypedAssetMarkerName(string assetType)
    {
        return assetType.Trim().ToLowerInvariant() switch
        {
            "shader" or "shadersource" => "ShaderSourceAsset",
            "texture" or "texture2d" => "Texture2DSourceAsset",
            "mesh" => "MeshSourceAsset",
            "material" => "MaterialSourceAsset",
            _ => null
        };
    }

    private static string GetCanonicalAssetType(string assetType)
    {
        return assetType.Trim().ToLowerInvariant() switch
        {
            "shader" or "shadersource" => "ShaderSource",
            "texture" or "texture2d" => "Texture2D",
            "mesh" => "Mesh",
            "material" => "Material",
            _ => assetType.Trim()
        };
    }

    private static string GetNestedAssetClassName(GeneratedAssetRef asset)
    {
        const string guidSuffix = "Guid";
        string name = asset.ConstantName;
        if (name.EndsWith(guidSuffix, StringComparison.Ordinal))
        {
            name = name[..^guidSuffix.Length];
        }

        return SanitizeIdentifier(name);
    }

    private static void RenderStringConstants(StringBuilder builder, string className, IReadOnlyList<string> values, string indent)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"{indent}public static class {className}");
        builder.AppendLine($"{indent}{{");

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            string baseName = SanitizeIdentifier(PathUtils.ToPascalCase(value));
            string constantName = baseName;
            int suffix = 2;
            while (!usedNames.Add(constantName))
            {
                constantName = $"{baseName}{suffix++}";
            }

            builder.AppendLine($"{indent}    public const string {constantName} = \"{EscapeString(value)}\";");
        }

        builder.AppendLine($"{indent}}}");
    }

    private static string GetGeneratedNamespace(string projectName, PackageInfo package)
    {
        string? entryClass = package.Manifest.Entry?.Class;
        if (!string.IsNullOrWhiteSpace(entryClass))
        {
            int lastDot = entryClass.LastIndexOf('.');
            if (lastDot > 0)
            {
                return entryClass[..lastDot];
            }
        }

        return $"ArisenEngine.{projectName.Replace("Com.Arisen.", string.Empty).Replace("Com.User.", string.Empty)}";
    }

    private static string GetAssetRefsClassName(string projectName, PackageInfo package)
    {
        string? entryClass = package.Manifest.Entry?.Class;
        if (!string.IsNullOrWhiteSpace(entryClass))
        {
            string typeName = entryClass.Split('.').Last();
            if (typeName.EndsWith("Package", StringComparison.Ordinal))
            {
                typeName = typeName[..^"Package".Length];
            }
            else if (typeName.EndsWith("Entry", StringComparison.Ordinal))
            {
                typeName = typeName[..^"Entry".Length];
            }

            return $"{SanitizeIdentifier(typeName)}AssetRefs";
        }

        return $"{SanitizeIdentifier(projectName.Replace(".", string.Empty))}AssetRefs";
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Asset";
        }

        var builder = new StringBuilder(value.Length + 5);
        foreach (char c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        if (builder.Length == 0)
        {
            return "Asset";
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, "Asset");
        }

        return builder.ToString();
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeComment(string value)
    {
        return value.Replace("\r", string.Empty).Replace("\n", " ");
    }

    private sealed record GeneratedAssetRef(
        Guid Guid,
        string RelativeSourcePath,
        string BaseName,
        string AssetType,
        string Importer,
        GeneratedMaterialRefs MaterialRefs)
    {
        public string ConstantName { get; init; } = string.Empty;
    }

    private sealed record DiscoveredAsset(
        Guid Guid,
        string SourcePath,
        string RelativeSourcePath,
        string BaseName,
        string AssetType,
        string Importer);

    private sealed record GeneratedMaterialRefs(
        IReadOnlyList<string> Texture2DSlots,
        IReadOnlyList<string> ScalarProperties,
        IReadOnlyList<string> Vector4Properties)
    {
        public static GeneratedMaterialRefs Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        public bool HasAny => Texture2DSlots.Count > 0 || ScalarProperties.Count > 0 || Vector4Properties.Count > 0;
    }
}
