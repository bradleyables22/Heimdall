using Heimdall.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.E2E.TestPlugin;

public sealed class TestPluginOptions
{
    public string Label { get; set; } = "Library default";
}

public sealed class TestPluginMarker;

public sealed class TestPluginRequestScope
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
}

public static class TestPluginRegistration
{
    public static IServiceCollection AddHeimdallE2ETestPlugin(
        this IServiceCollection services,
        Action<TestPluginOptions>? configure = null)
    {
        services.AddHeimdallInvocationAssembly<TestPluginMarker>();
        services.AddScoped<TestPluginRequestScope>();
        services.AddScoped<TestPluginWorkbench.Invocations>();
        services.AddOptions<TestPluginOptions>();
        if (configure is not null) services.Configure(configure);
        return services;
    }
}
