using Heimdall.Server.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Heimdall.Server
{
	public static class HeimdallServiceCollection
	{
		/// <summary>
		/// Registers Heimdall services and infrastructure with the dependency injection container.
		/// 
		/// This method enables Heimdall's HTML-first rendering pipeline, server actions,
		/// page mapping, and supporting services. It is intended to be called once during
		/// application startup.
		/// </summary>
		/// <param name="services">
		/// The service collection to which Heimdall services will be added.
		/// </param>
		/// <param name="configure">
		/// An optional configuration delegate used to customize Heimdall service behavior,
		/// such as feature flags, defaults, or global settings.
		/// </param>
		/// <param name="assemblies">
		/// Optional assemblies to scan for Heimdall-related components such as server actions,
		/// page definitions, or other attributed types.
		/// If omitted, Heimdall will use its default discovery behavior.
		/// </param>
		/// <returns>
		/// The same <see cref="IServiceCollection"/> instance, allowing fluent chaining
		/// of additional service registrations.
		/// </returns>
		public static IServiceCollection AddHeimdall( this IServiceCollection services, Action<HeimdallServiceSettings>? configure = null, params Assembly[]? assemblies)
		{
			if (configure != null)
				services.Configure(configure);
			else
				services.AddOptions<HeimdallServiceSettings>();

			var scan = ResolveAssemblies(assemblies);

			services.AddSingleton(sp =>
			{
				var registry = new ContentRegistry(); 
				foreach (var asm in scan)
					registry.AddFromAssembly(asm);

				return registry;
			});

            services.AddSingleton<BifrostSubscribeToken>();
            services.AddSingleton<Bifrost>();
			
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
			app.MapHeimdallSecurityEndpoints();
			app.MapHeimdallContentEndpoints();
			app.MapHeimdallBifrostEndpoints();
            return app;
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
