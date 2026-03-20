namespace ArisenBuildTool.Models;

public class PackageInfo
{
    public PackageManifest Manifest { get; set; } = new();
    public string DirectoryPath { get; set; } = string.Empty;
}
