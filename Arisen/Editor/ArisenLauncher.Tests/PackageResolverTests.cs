using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArisenLauncher.Models;
using ArisenLauncher.Services;
using Xunit;

namespace ArisenLauncher.Tests;

public sealed class PackageResolverTests
{
    [Fact]
    public async Task RestoreRemotePackageExtractsArchiveWithPackageAtRoot()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive = CreatePackageArchive("com.test.remote", topLevelFolder: false);
        await using var server = await LoopbackPackageServer.StartAsync(archive);

        await workspace.RestoreAsync("com.test.remote", server.PackageUrl);

        Assert.True(File.Exists(Path.Combine(workspace.Root, ".Cache", "com.test.remote", "package.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Root, ".arisen", "package-lock.json")));
    }

    [Fact]
    public async Task RestoreRemotePackageExtractsArchiveWithSingleTopLevelFolder()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive = CreatePackageArchive("com.test.foldered", topLevelFolder: true);
        await using var server = await LoopbackPackageServer.StartAsync(archive);

        await workspace.RestoreAsync("com.test.foldered", server.PackageUrl);

        string packageRoot = Path.Combine(workspace.Root, ".Cache", "com.test.foldered");
        Assert.True(File.Exists(Path.Combine(packageRoot, "package.json")));
        Assert.False(Directory.Exists(Path.Combine(packageRoot, "com.test.foldered")));
    }

    [Fact]
    public async Task RestoreRemotePackageCreatesLockWithSourceVersionHashAndTimestamp()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive = CreatePackageArchive("com.test.locked", topLevelFolder: false);
        await using var server = await LoopbackPackageServer.StartAsync(archive);

        await workspace.RestoreAsync("com.test.locked", server.PackageUrl, version: "2.3.4");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, ".arisen", "package-lock.json")));
        var package = doc.RootElement.GetProperty("Packages").GetProperty("com.test.locked");
        Assert.Equal("com.test.locked", package.GetProperty("Id").GetString());
        Assert.Equal("2.3.4", package.GetProperty("Version").GetString());
        Assert.Equal(server.PackageUrl, package.GetProperty("SourceUrl").GetString());
        Assert.Equal(".Cache/com.test.locked", package.GetProperty("CachePath").GetString());
        Assert.StartsWith("sha256:", package.GetProperty("ContentHash").GetString());
        Assert.NotEqual(default, package.GetProperty("AcquiredAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task RestoreRemotePackageFailsWhenCachedContentDiffersFromLock()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive = CreatePackageArchive("com.test.hash", topLevelFolder: false);
        await using var server = await LoopbackPackageServer.StartAsync(archive);

        await workspace.RestoreAsync("com.test.hash", server.PackageUrl);
        await File.AppendAllTextAsync(Path.Combine(workspace.Root, ".Cache", "com.test.hash", "payload.txt"), "changed");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.RestoreAsync("com.test.hash", server.PackageUrl));
        Assert.Contains("does not match package-lock.json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreRemotePackageFailsWhenManifestSourceChangesFromLock()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive = CreatePackageArchive("com.test.source", topLevelFolder: false);
        await using var firstServer = await LoopbackPackageServer.StartAsync(archive);
        await using var secondServer = await LoopbackPackageServer.StartAsync(archive);

        await workspace.RestoreAsync("com.test.source", firstServer.PackageUrl);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.RestoreAsync("com.test.source", secondServer.PackageUrl));
        Assert.Contains("source mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreRegistryPackageDownloadsExactVersionAndLocksArchiveMetadata()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive = CreatePackageArchive("com.test.registry", topLevelFolder: false);
        string archiveSha256 = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();

        await using var server = await LoopbackPackageServer.StartAsync(new Dictionary<string, byte[]>
        {
            ["/packages/com.test.registry-1.2.3.zip"] = archive,
            ["/registry.json"] = Encoding.UTF8.GetBytes($$"""
{
  "schemaVersion": 1,
  "packages": [
    {
      "id": "com.test.registry",
      "version": "1.2.3",
      "archive": {
        "url": "packages/com.test.registry-1.2.3.zip",
        "sha256": "{{archiveSha256}}",
        "sizeBytes": {{archive.Length}}
      }
    }
  ]
}
""")
        });

        await workspace.RestoreAsync("com.test.registry", server.GetUrl("/registry.json"), version: "1.2.3");

        Assert.True(File.Exists(Path.Combine(workspace.Root, ".Cache", "com.test.registry", "package.json")));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, ".arisen", "package-lock.json")));
        var package = doc.RootElement.GetProperty("Packages").GetProperty("com.test.registry");
        Assert.Equal(server.GetUrl("/registry.json"), package.GetProperty("SourceUrl").GetString());
        Assert.Equal(server.GetUrl("/packages/com.test.registry-1.2.3.zip"), package.GetProperty("ArchiveUrl").GetString());
        Assert.Equal($"sha256:{archiveSha256}", package.GetProperty("ArchiveHash").GetString());
    }

    [Fact]
    public async Task RestoreRegistryPackageRangeDownloadsHighestMatchingVersionAndLocksResolvedVersion()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive120 = CreatePackageArchive("com.test.range", topLevelFolder: false, payload: "one-two-zero");
        byte[] archive140 = CreatePackageArchive("com.test.range", topLevelFolder: false, payload: "one-four-zero");
        byte[] archive200 = CreatePackageArchive("com.test.range", topLevelFolder: false, payload: "two-zero-zero");
        string sha120 = Convert.ToHexString(SHA256.HashData(archive120)).ToLowerInvariant();
        string sha140 = Convert.ToHexString(SHA256.HashData(archive140)).ToLowerInvariant();
        string sha200 = Convert.ToHexString(SHA256.HashData(archive200)).ToLowerInvariant();

        await using var server = await LoopbackPackageServer.StartAsync(new Dictionary<string, byte[]>
        {
            ["/packages/com.test.range-1.2.0.zip"] = archive120,
            ["/packages/com.test.range-1.4.0.zip"] = archive140,
            ["/packages/com.test.range-2.0.0.zip"] = archive200,
            ["/registry.json"] = Encoding.UTF8.GetBytes($$"""
{
  "schemaVersion": 1,
  "packages": [
    {
      "id": "com.test.range",
      "version": "1.2.0",
      "archive": {
        "url": "packages/com.test.range-1.2.0.zip",
        "sha256": "{{sha120}}",
        "sizeBytes": {{archive120.Length}}
      }
    },
    {
      "id": "com.test.range",
      "version": "1.4.0",
      "archive": {
        "url": "packages/com.test.range-1.4.0.zip",
        "sha256": "{{sha140}}",
        "sizeBytes": {{archive140.Length}}
      }
    },
    {
      "id": "com.test.range",
      "version": "2.0.0",
      "archive": {
        "url": "packages/com.test.range-2.0.0.zip",
        "sha256": "{{sha200}}",
        "sizeBytes": {{archive200.Length}}
      }
    }
  ]
}
""")
        });

        await workspace.RestoreAsync("com.test.range", server.GetUrl("/registry.json"), version: "^1.2.0");

        Assert.Equal("one-four-zero", await File.ReadAllTextAsync(Path.Combine(workspace.Root, ".Cache", "com.test.range", "payload.txt")));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, ".arisen", "package-lock.json")));
        var package = doc.RootElement.GetProperty("Packages").GetProperty("com.test.range");
        Assert.Equal("^1.2.0", package.GetProperty("Version").GetString());
        Assert.Equal("1.4.0", package.GetProperty("ResolvedVersion").GetString());
        Assert.Equal(server.GetUrl("/packages/com.test.range-1.4.0.zip"), package.GetProperty("ArchiveUrl").GetString());
    }

    [Fact]
    public async Task RestoreRegistryPackageRangeFailsWhenLockedRangeWouldResolveToDifferentVersion()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive140 = CreatePackageArchive("com.test.float", topLevelFolder: false, payload: "one-four-zero");
        byte[] archive150 = CreatePackageArchive("com.test.float", topLevelFolder: false, payload: "one-five-zero");
        string sha140 = Convert.ToHexString(SHA256.HashData(archive140)).ToLowerInvariant();
        string sha150 = Convert.ToHexString(SHA256.HashData(archive150)).ToLowerInvariant();

        string firstRegistryUrl;
        await using (var firstServer = await LoopbackPackageServer.StartAsync(new Dictionary<string, byte[]>
        {
            ["/packages/com.test.float-1.4.0.zip"] = archive140,
            ["/registry.json"] = Encoding.UTF8.GetBytes($$"""
{
  "schemaVersion": 1,
  "packages": [
    {
      "id": "com.test.float",
      "version": "1.4.0",
      "archive": {
        "url": "packages/com.test.float-1.4.0.zip",
        "sha256": "{{sha140}}",
        "sizeBytes": {{archive140.Length}}
      }
    }
  ]
}
""")
        }))
        {
            firstRegistryUrl = firstServer.GetUrl("/registry.json");
            await workspace.RestoreAsync("com.test.float", firstRegistryUrl, version: "^1.0.0");
        }

        await using var secondServer = await LoopbackPackageServer.StartAsync(new Dictionary<string, byte[]>
        {
            ["/packages/com.test.float-1.5.0.zip"] = archive150,
            ["/registry.json"] = Encoding.UTF8.GetBytes($$"""
{
  "schemaVersion": 1,
  "packages": [
    {
      "id": "com.test.float",
      "version": "1.5.0",
      "archive": {
        "url": "packages/com.test.float-1.5.0.zip",
        "sha256": "{{sha150}}",
        "sizeBytes": {{archive150.Length}}
      }
    }
  ]
}
""")
        });

        string lockFilePath = Path.Combine(workspace.Root, ".arisen", "package-lock.json");
        string lockJson = await File.ReadAllTextAsync(lockFilePath);
        await File.WriteAllTextAsync(lockFilePath, lockJson.Replace(firstRegistryUrl, secondServer.GetUrl("/registry.json"), StringComparison.OrdinalIgnoreCase));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.RestoreAsync("com.test.float", secondServer.GetUrl("/registry.json"), version: "^1.0.0"));
        Assert.Contains("resolved version mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreRegistryPackageFailsWhenArchiveHashDoesNotMatchIndex()
    {
        using var workspace = ResolverWorkspace.Create();
        byte[] archive = CreatePackageArchive("com.test.badregistry", topLevelFolder: false);

        await using var server = await LoopbackPackageServer.StartAsync(new Dictionary<string, byte[]>
        {
            ["/packages/com.test.badregistry-1.0.0.zip"] = archive,
            ["/registry.json"] = Encoding.UTF8.GetBytes("""
{
  "schemaVersion": 1,
  "packages": [
    {
      "id": "com.test.badregistry",
      "version": "1.0.0",
      "archive": {
        "url": "packages/com.test.badregistry-1.0.0.zip",
        "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
        "sizeBytes": 1
      }
    }
  ]
}
""")
        });

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.RestoreAsync("com.test.badregistry", server.GetUrl("/registry.json")));
        Assert.Contains("integrity validation", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, ".Cache", "com.test.badregistry")));
    }

    private static byte[] CreatePackageArchive(string packageId, bool topLevelFolder, string payload = "payload")
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            string prefix = topLevelFolder ? $"{packageId}/" : string.Empty;
            WriteEntry(archive, $"{prefix}package.json", $$"""
{
  "id": "{{packageId}}",
  "name": "{{packageId}}",
  "version": "1.0.0",
  "layer": "user",
  "type": "managed",
  "dependencies": {}
}
""");
            WriteEntry(archive, $"{prefix}payload.txt", payload);
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private sealed class ResolverWorkspace : IDisposable
    {
        public string Root { get; }

        private ResolverWorkspace(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public static ResolverWorkspace Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "ArisenLauncher.Tests", Guid.NewGuid().ToString("N"));
            return new ResolverWorkspace(root);
        }

        public Task RestoreAsync(string packageId, string url, string version = "1.0.0")
        {
            var manifest = new ProjectManifest
            {
                Name = "ResolverFixture",
                Packages = new List<PackageRequirement>
                {
                    new() { Id = packageId, Url = url, Version = version }
                },
                Profiles = new Dictionary<string, ProfileDefinition>
                {
                    ["Development"] = new()
                }
            };

            return new PackageResolver(null).RestoreManifestPackagesAsync(manifest, "Development", Root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; failed deletion should not mask test results.
            }
        }
    }

    private sealed class LoopbackPackageServer : IAsyncDisposable
    {
        private sealed record Response(byte[] Body, string ContentType);

        private readonly TcpListener m_Listener;
        private readonly Task m_ServerTask;
        private readonly Dictionary<string, Response> m_Routes;
        private readonly CancellationTokenSource m_Cts = new();

        private LoopbackPackageServer(TcpListener listener, Dictionary<string, Response> routes)
        {
            m_Listener = listener;
            m_Routes = routes;
            PackageUrl = $"http://127.0.0.1:{((IPEndPoint)m_Listener.LocalEndpoint).Port}/package.zip";
            m_ServerTask = Task.Run(ServeAsync);
        }

        public string PackageUrl { get; }

        public string GetUrl(string path)
        {
            if (!path.StartsWith('/')) path = "/" + path;
            return $"http://127.0.0.1:{((IPEndPoint)m_Listener.LocalEndpoint).Port}{path}";
        }

        public static Task<LoopbackPackageServer> StartAsync(byte[] responseBody)
        {
            return StartAsync(new Dictionary<string, byte[]>
            {
                ["/package.zip"] = responseBody
            });
        }

        public static Task<LoopbackPackageServer> StartAsync(Dictionary<string, byte[]> routes)
        {
            var listener = new TcpListener(IPAddress.Loopback, port: 0);
            listener.Start();
            var responses = routes.ToDictionary(
                route => route.Key.StartsWith('/') ? route.Key : "/" + route.Key,
                route => new Response(route.Value, route.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "application/zip"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new LoopbackPackageServer(listener, responses));
        }

        private async Task ServeAsync()
        {
            while (!m_Cts.IsCancellationRequested)
            {
                try
                {
                    using TcpClient client = await m_Listener.AcceptTcpClientAsync(m_Cts.Token);
                    await using NetworkStream stream = client.GetStream();
                    string requestPath = await ReadRequestPathAsync(stream, m_Cts.Token);
                    if (!m_Routes.TryGetValue(requestPath, out var response))
                    {
                        string notFound = "HTTP/1.1 404 Not Found\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(notFound), m_Cts.Token);
                        continue;
                    }

                    string header = "HTTP/1.1 200 OK\r\n"
                        + $"Content-Type: {response.ContentType}\r\n"
                        + $"Content-Length: {response.Body.Length}\r\n"
                        + "Connection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(headerBytes, m_Cts.Token);
                    await stream.WriteAsync(response.Body, m_Cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private static async Task<string> ReadRequestPathAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            var request = new StringBuilder();
            while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                int read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                request.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            string firstLine = request.ToString().Split("\r\n", StringSplitOptions.None)[0];
            string[] parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[1] : "/";
        }

        public async ValueTask DisposeAsync()
        {
            m_Cts.Cancel();
            m_Listener.Stop();
            try
            {
                await m_ServerTask;
            }
            catch
            {
                // Listener shutdown can race with AcceptTcpClientAsync.
            }
            m_Cts.Dispose();
        }
    }
}
