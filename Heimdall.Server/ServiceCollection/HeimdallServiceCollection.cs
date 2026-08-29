using Heimdall.Server.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Heimdall.Server
{
    /// <summary>
    /// Provides extension methods for registering and configuring Heimdall services and middleware within an
    /// application's dependency injection container and ASP.NET request pipeline.
    /// </summary>
    /// <remarks>This static class offers a set of fluent extension methods to simplify the integration of
    /// Heimdall into ASP.NET Core applications. It supports both default and customized service registration, as well
    /// as flexible assembly discovery for content and endpoint registration. Use these methods during application
    /// startup to ensure all required Heimdall infrastructure is properly configured.</remarks>
    public static class HeimdallServiceCollection
    {
        /// <summary>
        /// Registers Heimdall services using default settings and default assembly discovery.
        /// </summary>
        public static IServiceCollection AddHeimdall(this IServiceCollection services)
            => AddHeimdallCore(services, configure: null, assemblies: null);

        /// <summary>
        /// Registers Heimdall services using default settings and the provided assemblies for discovery.
        /// </summary>
        public static IServiceCollection AddHeimdall(
            this IServiceCollection services,
            params Assembly[] assemblies)
            => AddHeimdallCore(services, configure: null, assemblies: assemblies);

        /// <summary>
        /// Registers Heimdall services using the provided configuration and default assembly discovery.
        /// </summary>
        public static IServiceCollection AddHeimdall(
            this IServiceCollection services,
            Action<HeimdallServiceSettings> configure)
            => AddHeimdallCore(services, configure, assemblies: null);

        /// <summary>
        /// Registers Heimdall services using the provided configuration and assemblies for discovery.
        /// </summary>
        public static IServiceCollection AddHeimdall(
            this IServiceCollection services,
            Action<HeimdallServiceSettings> configure,
            params Assembly[] assemblies)
            => AddHeimdallCore(services, configure, assemblies);

        /// <summary>
        /// Registers MVC view rendering support for Heimdall content actions.
        /// </summary>
        /// <remarks>
        /// This method adds ASP.NET Core MVC view services, an HTTP context accessor, and
        /// <see cref="IHeimdallMvcRenderer"/> so instance content actions can render MVC partial views.
        /// It does not map controller routes; applications should configure their MVC endpoints separately
        /// when they also need normal controllers or Razor views.
        /// </remarks>
        public static IServiceCollection AddHeimdallMvc(this IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddHttpContextAccessor();
            services.TryAddScoped<IHeimdallMvcRenderer, HeimdallMvcRenderer>();

            return services;
        }

        /// <summary>
        /// Enables Heimdall within the ASP.NET request pipeline.
        /// 
        /// This method finalizes Heimdall setup by registering its middleware,
        /// endpoints, and supporting infrastructure required to serve Heimdall
        /// pages and server actions at runtime.
        /// </summary>
        /// <param name="app">
        /// The <see cref="WebApplication"/> instance used to configure the request pipeline.
        /// </param>
        /// <returns>
        /// The same <see cref="WebApplication"/> instance, allowing fluent
        /// pipeline configuration.
        /// </returns>
        public static WebApplication UseHeimdall(this WebApplication app)
        {
            EnsureHeimdallServicesRegistered(app.Services);

            app.MapHeimdallSecurityEndpoints();
            app.MapHeimdallContentEndpoints();
            app.MapHeimdallBifrostEndpoints();
            return app;
        }

        /// <summary>
        /// Enables Heimdall within an ASP.NET Core middleware pipeline that uses endpoint routing.
        /// </summary>
        /// <param name="app">
        /// The <see cref="IApplicationBuilder"/> instance used to configure the request pipeline.
        /// </param>
        /// <returns>
        /// The same <see cref="IApplicationBuilder"/> instance, allowing fluent pipeline configuration.
        /// </returns>
        public static IApplicationBuilder UseHeimdall(this IApplicationBuilder app)
        {
            EnsureHeimdallServicesRegistered(app.ApplicationServices);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHeimdallSecurityEndpoints();
                endpoints.MapHeimdallContentEndpoints();
                endpoints.MapHeimdallBifrostEndpoints();
            });

            return app;
        }

        private static void EnsureHeimdallServicesRegistered(IServiceProvider services)
        {
            var serviceInspector = services.GetService<IServiceProviderIsService>();

            if (IsRegistered<ContentRegistry>(services, serviceInspector) &&
                IsRegistered<BifrostSubscribeToken>(services, serviceInspector) &&
                IsRegistered<Bifrost>(services, serviceInspector))
            {
                return;
            }

            throw new InvalidOperationException(
                "UseHeimdall() requires Heimdall runtime services. " +
                "Call builder.Services.AddHeimdall(...) before builder.Build(), " +
                "or omit UseHeimdall() for static-site-generation-only apps.");
        }

        private static bool IsRegistered<T>(
            IServiceProvider services,
            IServiceProviderIsService? serviceInspector)
            where T : class
        {
            if (serviceInspector is not null)
                return serviceInspector.IsService(typeof(T));

            return services.GetService<T>() is not null;
        }

        private static IServiceCollection AddHeimdallCore(
            IServiceCollection services,
            Action<HeimdallServiceSettings>? configure,
            Assembly[]? assemblies)
        {
            // Bifrost subscribe tokens require data protection independently of antiforgery.
            services.AddDataProtection();

            if (configure != null)
                services.Configure(configure);
            else
                services.AddOptions<HeimdallServiceSettings>();

            var scan = ResolveAssemblies(assemblies);

            services.AddSingleton<ContentRegistry>(sp =>
            {
                var registry = new ContentRegistry();

                foreach (var asm in scan)
                    registry.AddFromAssembly(asm, sp);

                return registry;
            });

            services.AddSingleton<BifrostSubscribeToken>();
            services.AddSingleton<Bifrost>();

            return services;
        }

        private static IReadOnlyCollection<Assembly> ResolveAssemblies(Assembly[]? assemblies)
        {
            var result = new HashSet<Assembly>();

            if (assemblies != null && assemblies.Length > 0)
            {
                foreach (var asm in assemblies)
                {
                    if (asm != null)
                        result.Add(asm);
                }

                return result;
            }

            var entry = Assembly.GetEntryAssembly();
            if (entry != null)
                result.Add(entry);

            var calling = Assembly.GetCallingAssembly();
            if (calling != null)
                result.Add(calling);

            if (result.Count == 0)
            {
                var executing = Assembly.GetExecutingAssembly();
                if (executing != null)
                    result.Add(executing);
            }

            return result;
        }
    }
}
