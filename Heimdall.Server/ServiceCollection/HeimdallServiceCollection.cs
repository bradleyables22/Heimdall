using Heimdall.Server.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Heimdall.Server
{
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
            app.MapHeimdallSecurityEndpoints();
            app.MapHeimdallContentEndpoints();
            app.MapHeimdallBifrostEndpoints();
            return app;
        }

        private static IServiceCollection AddHeimdallCore(
            IServiceCollection services,
            Action<HeimdallServiceSettings>? configure,
            Assembly[]? assemblies)
        {
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