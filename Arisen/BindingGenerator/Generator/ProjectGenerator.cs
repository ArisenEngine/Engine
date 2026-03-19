using System.IO;

namespace BindingGenerator.Generator;

public static class ProjectGenerator
{
    public static void GeneratePackageFiles(string packageId, string packageDir)
    {
        if (!Directory.Exists(packageDir))
            Directory.CreateDirectory(packageDir);

        string projectName = GetProjectNameFromPackageId(packageId);

        // 1. Generate package.json if it doesn't exist
        var manifestPath = Path.Combine(packageDir, "package.json");
        if (!File.Exists(manifestPath))
        {
            var manifestContent = $$"""
{
  "id": "{{packageId}}",
  "name": "{{projectName}}",
  "version": "1.0.0",
  "description": "Auto-generated bindings for {{projectName}}",
  "entryAssembly": "{{projectName}}.dll",
  "author": "Arisen Team",
  "tags": [
      "binding",
      "native"
  ],
  "engineVersion": "0.1.0",
  "dependencies": {}
}
""";
            File.WriteAllText(manifestPath, manifestContent);
            System.Console.WriteLine($"Generated {packageId}/package.json");
        }

        // 2. Generate .csproj if it doesn't exist
        var csprojPath = Path.Combine(packageDir, $"{projectName}.csproj");
        if (!File.Exists(csprojPath))
        {
            string extraDependencies = "";
            if (packageId == "com.arisen.shader-compiler")
            {
                extraDependencies = $"\n    <ProjectReference Include=\"..\\com.arisen.rhi.core\\Com.Arisen.Rhi.Core.csproj\" />";
            }

            var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <!-- Reference the Kernel for shared contracts -->
  <ItemGroup>
    <ProjectReference Include=""..\..\ArisenKernel\ArisenKernel.csproj"" />{extraDependencies}
  </ItemGroup>
</Project>";
            File.WriteAllText(csprojPath, csprojContent);
            System.Console.WriteLine($"Generated {packageId}/{projectName}.csproj");
        }
    }

    private static string GetProjectNameFromPackageId(string packageId)
    {
        var parts = packageId.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        }
        return string.Join(".", parts).Replace("-", "");
    }
}