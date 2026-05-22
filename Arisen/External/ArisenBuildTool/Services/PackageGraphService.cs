using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public static class PackageGraphService
{
    public static string Render(PackageValidationResult validation, string profile, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => RenderJson(validation, profile),
            "dot" => RenderDot(validation, profile),
            "text" => RenderText(validation, profile),
            _ => throw new ArgumentException($"Unsupported graph format '{format}'. Use text, json, or dot.", nameof(format))
        };
    }

    private static string RenderText(PackageValidationResult validation, string profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Package graph for profile '{profile}'");
        sb.AppendLine($"Packages: {validation.SortedPackages.Count}");
        sb.AppendLine();
        sb.AppendLine("Topological order:");

        for (int i = 0; i < validation.SortedPackages.Count; i++)
        {
            var package = validation.SortedPackages[i];
            sb.AppendLine($"  {i + 1}. {package.Manifest.Id} ({package.Manifest.Type})");
            sb.AppendLine($"     Path: {package.DirectoryPath}");

            var dependencies = package.Manifest.Dependencies?.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
            if (dependencies is { Count: > 0 })
            {
                sb.AppendLine("     Dependencies:");
                foreach (var dependency in dependencies)
                {
                    sb.AppendLine($"       - {dependency.Key} {dependency.Value}");
                }
            }
            else
            {
                sb.AppendLine("     Dependencies: <none>");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Edges:");
        bool wroteEdge = false;
        foreach (var edge in EnumerateEdges(validation))
        {
            sb.AppendLine($"  {edge.From} -> {edge.To} [{edge.Version}]");
            wroteEdge = true;
        }

        if (!wroteEdge)
        {
            sb.AppendLine("  <none>");
        }

        return sb.ToString();
    }

    private static string RenderJson(PackageValidationResult validation, string profile)
    {
        var data = new
        {
            profile,
            packages = validation.SortedPackages.Select((package, index) => new
            {
                order = index + 1,
                id = package.Manifest.Id,
                type = package.Manifest.Type,
                path = package.DirectoryPath,
                dependencies = package.Manifest.Dependencies ?? new Dictionary<string, string>()
            }),
            edges = EnumerateEdges(validation).Select(edge => new
            {
                from = edge.From,
                to = edge.To,
                version = edge.Version
            })
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string RenderDot(PackageValidationResult validation, string profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"digraph \"ArisenPackageGraph_{EscapeDotId(profile)}\" {{");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box];");

        foreach (var package in validation.SortedPackages)
        {
            sb.AppendLine($"  \"{EscapeDotId(package.Manifest.Id)}\" [label=\"{EscapeDotLabel(package.Manifest.Id)}\\n{EscapeDotLabel(package.Manifest.Type ?? string.Empty)}\"];");
        }

        foreach (var edge in EnumerateEdges(validation))
        {
            sb.AppendLine($"  \"{EscapeDotId(edge.From)}\" -> \"{EscapeDotId(edge.To)}\" [label=\"{EscapeDotLabel(edge.Version)}\"];");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<PackageGraphEdge> EnumerateEdges(PackageValidationResult validation)
    {
        foreach (var package in validation.SortedPackages.OrderBy(x => x.Manifest.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (package.Manifest.Dependencies == null) continue;

            foreach (var dependency in package.Manifest.Dependencies.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                yield return new PackageGraphEdge(package.Manifest.Id, dependency.Key, dependency.Value);
            }
        }
    }

    private static string EscapeDotId(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeDotLabel(string value) => EscapeDotId(value).Replace("\r", string.Empty).Replace("\n", "\\n");

    private sealed record PackageGraphEdge(string From, string To, string Version);
}
