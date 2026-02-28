using System.IO;

namespace BindingGenerator.Generator;

public static class ProjectGenerator
{
    public static void GenerateProjectFile(string outputDir)
    {
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var projPath = Path.Combine(outputDir, "AutoBinding.csproj");
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <BaseOutputPath>..\..\..\x64\</BaseOutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include=""ArisenEngine"" />
  </ItemGroup>

</Project>
";
        File.WriteAllText(projPath, content);
        System.Console.WriteLine("Generated AutoBinding.csproj (no CppSharp dependency)");
    }
}
