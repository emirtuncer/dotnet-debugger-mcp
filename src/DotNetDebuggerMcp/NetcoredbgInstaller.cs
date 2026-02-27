using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace DotNetDebuggerMcp;

/// <summary>
/// Ensures netcoredbg is available, auto-downloading it if necessary.
/// Resolution order:
///   1. NETCOREDBG_PATH env var (if set and file exists)
///   2. Default per-user install location
///   3. Auto-download from GitHub releases
/// </summary>
public static class NetcoredbgInstaller
{
    private const string Version = "3.1.3-1062";
    private const string BaseUrl = "https://github.com/Samsung/netcoredbg/releases/download";

    /// <summary>
    /// Returns the path to a working netcoredbg executable, downloading it if needed.
    /// </summary>
    public static async Task<string> EnsureInstalledAsync()
    {
        // 1. Explicit env var
        string? envPath = Environment.GetEnvironmentVariable("NETCOREDBG_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            return envPath;

        // 2. Default per-user install location
        string defaultPath = GetDefaultExePath();
        if (File.Exists(defaultPath))
            return defaultPath;

        // 3. Auto-download
        Console.Error.WriteLine($"[dotnet-debugger] netcoredbg not found. Downloading version {Version}...");
        await DownloadAndInstallAsync(defaultPath);
        Console.Error.WriteLine($"[dotnet-debugger] netcoredbg installed to: {defaultPath}");
        return defaultPath;
    }

    private static string GetDefaultExePath()
    {
        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "netcoredbg.exe"
            : "netcoredbg";
        return Path.Combine(GetInstallDir(), exeName);
    }

    private static string GetInstallDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localApp, "dotnet-debugger-mcp", "netcoredbg");
        }
        else
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share", "dotnet-debugger-mcp", "netcoredbg");
        }
    }

    private static async Task DownloadAndInstallAsync(string targetExePath)
    {
        string installDir = Path.GetDirectoryName(targetExePath)!;
        Directory.CreateDirectory(installDir);

        string archiveName = GetArchiveName();
        string url = $"{BaseUrl}/{Version}/{archiveName}";

        string tempArchive = Path.Combine(Path.GetTempPath(), $"netcoredbg-{Guid.NewGuid()}{Path.GetExtension(archiveName)}");
        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"netcoredbg-extract-{Guid.NewGuid()}");

        try
        {
            // Download
            Console.Error.WriteLine($"[dotnet-debugger] Fetching {url}");
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(5);
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(tempArchive);
                await response.Content.CopyToAsync(fs);
            }

            // Extract to temp dir
            Directory.CreateDirectory(tempExtractDir);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ZipFile.ExtractToDirectory(tempArchive, tempExtractDir, overwriteFiles: true);
            }
            else
            {
                await using var gz = new GZipStream(File.OpenRead(tempArchive), CompressionMode.Decompress);
                await TarFile.ExtractToDirectoryAsync(gz, tempExtractDir, overwriteFiles: true);
            }

            // Find the executable (handle archives with or without a top-level directory)
            string exeName = Path.GetFileName(targetExePath);
            string? foundExe = Directory
                .GetFiles(tempExtractDir, exeName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (foundExe == null)
                throw new FileNotFoundException($"'{exeName}' not found in the downloaded archive.");

            // Copy all files from the same directory as the found executable into installDir
            string sourceDir = Path.GetDirectoryName(foundExe)!;
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(installDir, Path.GetFileName(file)), overwrite: true);

            // Make executable on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(targetExePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        finally
        {
            try { File.Delete(tempArchive); } catch { }
            try { Directory.Delete(tempExtractDir, recursive: true); } catch { }
        }
    }

    private static string GetArchiveName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "netcoredbg-win64.zip";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "netcoredbg-osx-x64.tar.gz";
        return RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? "netcoredbg-linux-arm64.tar.gz"
            : "netcoredbg-linux-x64.tar.gz";
    }
}
