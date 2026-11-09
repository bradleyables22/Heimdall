using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace Heimdall.Functions;

/// <summary>Dispatches SDK static web asset endpoints without an ASP.NET Core middleware pipeline.</summary>
public sealed class HeimdallFunctionStaticWebAssets
{
    private readonly Lazy<AssetCatalog> _catalog;

    /// <summary>Creates the dispatcher for the application's SDK-generated static asset manifest.</summary>
    public HeimdallFunctionStaticWebAssets(IServiceProvider services, IOptions<HeimdallFunctionAssetOptions> options)
    {
        _catalog = new(() => CreateCatalog(services, options.Value));
    }

    /// <summary>Executes a matching static web asset endpoint, or returns null for a page route.</summary>
    public async Task<IActionResult?> TryServeAsync(HttpRequest request, string path)
    {
        var catalog = _catalog.Value;
        if (ReferenceEquals(catalog, AssetCatalog.Empty) &&
            path.StartsWith("_content/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The Function App has no SDK static web assets manifest. Use Microsoft.NET.Sdk.Razor and rebuild the app.");
        if (!catalog.Endpoints.TryGetValue(path, out var candidates)) return null;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            request.HttpContext.Response.Headers.Allow = "GET, HEAD";
            return new StatusCodeResult(StatusCodes.Status405MethodNotAllowed);
        }

        var endpoint = SelectEndpoint(candidates, request.Headers.AcceptEncoding.ToString());
        if (endpoint is null) return new StatusCodeResult(StatusCodes.Status406NotAcceptable);
        if (request.Headers.Range.Count > 0 || request.Headers.IfNoneMatch.Count > 0 ||
            request.Headers.IfModifiedSince.Count > 0 || request.Headers.IfMatch.Count > 0 ||
            request.Headers.IfUnmodifiedSince.Count > 0 || request.Headers.IfRange.Count > 0)
            return ServeConditionalOrRange(request, endpoint, catalog.Provider);

        var context = request.HttpContext;
        var previousPath = request.Path;
        var previousEndpoint = context.GetEndpoint();
        var previousRouteValues = request.RouteValues;
        request.Path = new PathString('/' + path);
        context.SetEndpoint(endpoint);
        request.RouteValues = new RouteValueDictionary();
        try
        {
            await endpoint.RequestDelegate!(context);
            return new EmptyResult();
        }
        finally
        {
            request.Path = previousPath;
            context.SetEndpoint(previousEndpoint);
            request.RouteValues = previousRouteValues;
        }
    }

    private static IActionResult ServeConditionalOrRange(HttpRequest request, RouteEndpoint endpoint, IFileProvider provider)
    {
        var descriptor = endpoint.Metadata.GetMetadata<StaticAssetDescriptor>()!;
        var file = provider.GetFileInfo(descriptor.AssetPath);
        if (!file.Exists) return new NotFoundResult();

        var headers = descriptor.ResponseHeaders.ToDictionary(header => header.Name, header => header.Value,
            StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
            if (header.Key is not ("Content-Length" or "Content-Type" or "ETag" or "Last-Modified"))
                request.HttpContext.Response.Headers[header.Key] = header.Value;

        var modified = DateTimeOffset.Parse(headers["Last-Modified"], CultureInfo.InvariantCulture);
        var tag = EntityTagHeaderValue.Parse(headers["ETag"]);
        var result = file.PhysicalPath is { } physicalPath
            ? Results.File(physicalPath, headers["Content-Type"], lastModified: modified,
                entityTag: tag, enableRangeProcessing: true)
            : Results.File(file.CreateReadStream(), headers["Content-Type"], lastModified: modified,
                entityTag: tag, enableRangeProcessing: true);
        return HeimdallFunctions.Adapt(result);
    }

    private static RouteEndpoint? SelectEndpoint(RouteEndpoint[] candidates, string acceptEncoding)
    {
        var accepted = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in acceptEncoding.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (System.Net.Http.Headers.StringWithQualityHeaderValue.TryParse(part, out var value))
                accepted[value.Value] = value.Quality ?? 1;

        var identity = candidates.FirstOrDefault(endpoint => endpoint.Metadata.GetMetadata<ContentEncodingMetadata>() is null);
        var wildcard = accepted.GetValueOrDefault("*", -1);
        var identityQuality = accepted.GetValueOrDefault("identity", wildcard == 0 ? 0 : 1);
        var best = identityQuality > 0 ? identity : null;
        var bestQuality = best is null ? 0 : identityQuality;
        var bestPreference = 0d;
        foreach (var candidate in candidates)
        {
            var encoding = candidate.Metadata.GetMetadata<ContentEncodingMetadata>();
            if (encoding is null) continue;
            var quality = accepted.GetValueOrDefault(encoding.Value, wildcard);
            if (quality <= 0 || (quality < bestQuality) || (quality == bestQuality && encoding.Quality <= bestPreference))
                continue;
            best = candidate;
            bestQuality = quality;
            bestPreference = encoding.Quality;
        }
        return best;
    }

    private static AssetCatalog CreateCatalog(IServiceProvider services, HeimdallFunctionAssetOptions options)
    {
        var applicationName = services.GetService<IHostEnvironment>()?.ApplicationName;
        if (string.IsNullOrWhiteSpace(applicationName)) return AssetCatalog.Empty;

        var manifestPath = options.StaticWebAssetsManifestPath ??
            Path.Combine(AppContext.BaseDirectory, applicationName + ".staticwebassets.endpoints.json");
        if (!Path.IsPathRooted(manifestPath)) manifestPath = Path.Combine(AppContext.BaseDirectory, manifestPath);
        if (!File.Exists(manifestPath)) return AssetCatalog.Empty;

        var root = Path.GetFullPath(options.RootPath);
        IFileProvider physicalProvider = Directory.Exists(root) ? new PhysicalFileProvider(root) : new NullFileProvider();
        var environment = new StaticAssetEnvironment
        {
            ApplicationName = applicationName,
            EnvironmentName = services.GetService<IHostEnvironment>()?.EnvironmentName ?? Environments.Production,
            ContentRootPath = AppContext.BaseDirectory,
            ContentRootFileProvider = new PhysicalFileProvider(AppContext.BaseDirectory),
            WebRootPath = root,
            WebRootFileProvider = physicalProvider
        };
        const string endpointSuffix = ".staticwebassets.endpoints.json";
        var manifestName = Path.GetFileName(manifestPath);
        var runtimeName = manifestName.EndsWith(endpointSuffix, StringComparison.OrdinalIgnoreCase)
            ? manifestName[..^endpointSuffix.Length] + ".staticwebassets.runtime.json"
            : applicationName + ".staticwebassets.runtime.json";
        var runtimeManifest = Path.Combine(Path.GetDirectoryName(manifestPath)!, runtimeName);
        if (File.Exists(runtimeManifest))
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [WebHostDefaults.StaticWebAssetsKey] = runtimeManifest })
                .Build();
            StaticWebAssetsLoader.UseStaticWebAssets(environment, configuration);
        }

        var assetServices = new StaticAssetServiceProvider(services, environment);
        var builder = new StaticAssetRouteBuilder(assetServices);
        builder.MapStaticAssets(manifestPath);
        var endpoints = builder.DataSources.SelectMany(source => source.Endpoints).Cast<RouteEndpoint>()
            .GroupBy(endpoint => endpoint.RoutePattern.RawText!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        return new AssetCatalog(endpoints, environment.WebRootFileProvider);
    }

    private sealed record AssetCatalog(IReadOnlyDictionary<string, RouteEndpoint[]> Endpoints, IFileProvider Provider)
    {
        public static AssetCatalog Empty { get; } = new(new Dictionary<string, RouteEndpoint[]>(), new NullFileProvider());
    }

    private sealed class StaticAssetServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public StaticAssetServiceProvider(IServiceProvider inner, IWebHostEnvironment environment)
        {
            _inner = inner;
            _environment = environment;
            _configuration = new ConfigurationBuilder()
                .AddConfiguration(inner.GetService<IConfiguration>() ?? new ConfigurationBuilder().Build())
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ReloadStaticAssetsAtRuntime"] = "false" })
                .Build();
        }

        public object? GetService(Type serviceType)
            => serviceType == typeof(IWebHostEnvironment) ? _environment :
                serviceType == typeof(IConfiguration) ? _configuration : _inner.GetService(serviceType);
    }

    private sealed class StaticAssetRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => services;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(services);
    }

    private sealed class StaticAssetEnvironment : IWebHostEnvironment
    {
        public required string ApplicationName { get; set; }
        public required string EnvironmentName { get; set; }
        public required string ContentRootPath { get; set; }
        public required IFileProvider ContentRootFileProvider { get; set; }
        public required string WebRootPath { get; set; }
        public required IFileProvider WebRootFileProvider { get; set; }
    }
}
