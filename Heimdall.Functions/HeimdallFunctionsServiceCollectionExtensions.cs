using Heimdall.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Heimdall.Functions;

/// <summary>Registers the Heimdall isolated-worker HTTP adapter.</summary>
public static class HeimdallFunctionsServiceCollectionExtensions
{
    /// <summary>Registers pages for the application's single catch-all HTTP trigger.</summary>
    public static IServiceCollection AddHeimdallFunctionPages(
        this IServiceCollection services, Action<HeimdallFunctionPages> mapPages)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mapPages);
        var pages = new HeimdallFunctionPages();
        mapPages(pages);
        services.AddSingleton(pages);
        return services;
    }

    /// <summary>Registers MVC partial rendering and compiled view discovery for the marker's assembly.</summary>
    /// <remarks>This does not map MVC controller or Razor Pages routes. Additional view libraries can use AddApplicationPart.</remarks>
    public static IServiceCollection AddHeimdallFunctionsMvc<TMarker>(this IServiceCollection services)
    {
        services.AddHeimdallMvc();
        services.AddControllersWithViews().AddApplicationPart(typeof(TMarker).Assembly);
        return services;
    }

    /// <summary>Registers shared Heimdall services, antiforgery, and the Functions request dispatcher.</summary>
    /// <remarks>Call ConfigureFunctionsWebApplication on the worker builder separately. Supply invocation assemblies explicitly.</remarks>
    public static IServiceCollection AddHeimdallFunctions(
        this IServiceCollection services,
        Action<HeimdallServiceSettings>? configure = null,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        services.AddRouting();
        services.AddHttpContextAccessor();
        services.AddAntiforgery();
        services.AddOptions<HeimdallFunctionAssetOptions>();
        services.TryAddSingleton<HeimdallFunctionAssets>();
        services.TryAddSingleton<HeimdallFunctionStaticWebAssets>();
        services.TryAddSingleton<HeimdallFunctionPages>();
        services.AddHeimdall(configure ?? (_ => { }), assemblies);
        services.TryAddSingleton<HeimdallFunctions>();
        services.TryAddSingleton<HeimdallFunctionRouter>();
        return services;
    }
}
