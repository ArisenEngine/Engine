using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace ArisenLauncher.Services;

public class PackageResolver
{
    private readonly ILogService _logService;
    private static readonly HttpClient s_HttpClient = new();

    public PackageResolver(ILogService logService)
    {
        _logService = logService;
    }

    public async Task<string> ResolveAsync(string id, string url, string destinationDir)
    {
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveRemoteAsync(id, url, destinationDir);
        }

        throw new NotSupportedException($"URL scheme not supported by PackageResolver: {url}");
    }

    private async Task<string> ResolveRemoteAsync(string id, string url, string destinationDir)
    {
        string fileName = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrEmpty(fileName)) fileName = $"{id}.zip";

        string tempFile = Path.Combine(Path.GetTempPath(), fileName);
        string extractDir = Path.Combine(destinationDir, id);

        if (Directory.Exists(extractDir))
        {
            _logService.Info($"[PackageResolver] Package {id} already exists in {extractDir}. Skipping download.");
            return extractDir;
        }

        _logService.Info($"[PackageResolver] Downloading package from {url}...");

        using (var response = await s_HttpClient.GetAsync(url))
        {
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }
        }

        string tempExtractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            _logService.Info($"[PackageResolver] Extracting to temporary directory...");
            ZipFile.ExtractToDirectory(tempFile, tempExtractDir);

            _logService.Info($"[PackageResolver] Moving safely to {extractDir}...");
            Directory.Move(tempExtractDir, extractDir);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
        }

        return extractDir;
    }
}
