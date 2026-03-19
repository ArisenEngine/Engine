using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using ArisenKernel.Diagnostics;

namespace ArisenKernel.Packages;

/// <summary>
/// Default implementation of IPackageResolver for Arisen Engine.
/// Handles local file system paths (directories and ZIPs) and remote URLs.
/// </summary>
public class DefaultPackageResolver : IPackageResolver
{
    private static readonly HttpClient s_HttpClient = new();

    public bool CanResolve(string url)
    {
        return url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> ResolveAsync(string id, string url, string destinationDir)
    {
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveLocalAsync(url.Substring(7), destinationDir);
        }
        else if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveRemoteAsync(id, url, destinationDir);
        }

        throw new NotSupportedException($"URL scheme not supported: {url}");
    }

    private async Task<string> ResolveLocalAsync(string path, string destinationDir)
    {
        // Handle potentially escaped characters in file:// paths
        path = Uri.UnescapeDataString(path);

        if (Directory.Exists(path))
        {
            string fullPath = Path.GetFullPath(path);
            KernelLog.Info($"[PackageResolver] Using local directory: {fullPath}");
            return fullPath;
        }

        if (File.Exists(path) && (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            string extractDir = Path.Combine(destinationDir, Path.GetFileNameWithoutExtension(path));
            if (!Directory.Exists(extractDir))
            {
                KernelLog.Info($"[PackageResolver] Extracting local ZIP: {path} to {extractDir}");
                ZipFile.ExtractToDirectory(path, extractDir);
            }
            return extractDir;
        }

        throw new FileNotFoundException($"Local package source not found: {path}");
    }

    private async Task<string> ResolveRemoteAsync(string id, string url, string destinationDir)
    {
        string fileName = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrEmpty(fileName)) fileName = $"{id}.zip";

        string tempFile = Path.Combine(Path.GetTempPath(), fileName);
        string extractDir = Path.Combine(destinationDir, id);

        if (Directory.Exists(extractDir))
        {
            // For now, we assume if it exists, it's correct. 
            // In a real scenario, we'd check versions here.
            KernelLog.Info($"[PackageResolver] Package {id} already exists in {extractDir}. Skipping download.");
            return extractDir;
        }

        KernelLog.Info($"[PackageResolver] Downloading package from {url}...");

        using (var response = await s_HttpClient.GetAsync(url))
        {
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }
        }

        KernelLog.Info($"[PackageResolver] Extracting to {extractDir}...");
        ZipFile.ExtractToDirectory(tempFile, extractDir);
        File.Delete(tempFile);

        return extractDir;
    }
}

