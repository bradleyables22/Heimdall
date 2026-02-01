using Heimdall.Server.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Heimdall.Server
{
	public static class HeimdallServiceCollection
	{
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

			return services;
		}

		public static WebApplication UseHeimdall(this WebApplication app) 
		{
			app.MapHeimdallSecurityEndpoints();
			app.MapHeimdallContentEndpoints();

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
