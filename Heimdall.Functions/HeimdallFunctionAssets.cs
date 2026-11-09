using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Heimdall.Functions;

/// <summary>Configures static assets served by explicit Functions HTTP triggers.</summary>
public sealed class HeimdallFunctionAssetOptions
{
    /// <summary>Gets or sets the asset directory. Defaults to wwwroot beside the application assembly.</summary>
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    /// <summary>Gets or sets the public browser cache duration for successful asset responses.</summary>
    public TimeSpan CacheMaxAge { get; set; } = TimeSpan.Zero;

    /// <summary>Gets or sets the app's SDK static web assets endpoint manifest path.</summary>
    public string? StaticWebAssetsManifestPath { get; set; }
}

/// <summary>Serves application web-root files without ASP.NET Core routing.</summary>
public sealed class HeimdallFunctionAssets(IOptions<HeimdallFunctionAssetOptions> options)
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>Serves a GET or HEAD request for a path relative to the asset trigger.</summary>
    /// <remarks>Files are resolved only under RootPath; links and traversal are rejected.</remarks>
    public IActionResult Serve(HttpRequest request, string? path)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            request.HttpContext.Response.Headers.Allow = "GET, HEAD";
            return HeimdallFunctions.Adapt(Results.StatusCode(405));
        }
        var settings = options.Value;
        if (settings.CacheMaxAge < TimeSpan.Zero) throw new InvalidOperationException("Asset cache duration cannot be negative.");
        if (path is null || path.Contains('\\') || path.Contains(':') ||
            path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".." || segment.StartsWith('.')))
            return HeimdallFunctions.Adapt(Results.NotFound());

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.RootPath));
        var resolved = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, pathComparison) ||
            !File.Exists(resolved) || !ContentTypes.TryGetContentType(resolved, out var contentType))
            return HeimdallFunctions.Adapt(Results.NotFound());
        // Reject symlinks/junctions along the entire configured asset path.
        for (FileSystemInfo? entry = new FileInfo(resolved); entry is not null; entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                return HeimdallFunctions.Adapt(Results.NotFound());
        }
        var info = new FileInfo(resolved);
        var result = Results.File(resolved, contentType, lastModified: info.LastWriteTimeUtc,
            entityTag: new EntityTagHeaderValue($"\"{info.Length:x}-{info.LastWriteTimeUtc.Ticks:x}\"", true), enableRangeProcessing: true);
        request.HttpContext.Response.Headers.CacheControl = $"public, max-age={(long)settings.CacheMaxAge.TotalSeconds}, must-revalidate";
        request.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
        return HeimdallFunctions.Adapt(result);
    }
}
