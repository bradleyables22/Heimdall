using Heimdall.Server.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Heimdall.Server
{
	public static class HeimdallServiceCollection
	{
		public static IServiceCollection AddHeimdall(this IServiceCollection services, params Assembly[]? assemblies)
		{
			var scan = new HashSet<Assembly>();

			if (assemblies != null && assemblies.Length > 0)
			{
				foreach (var a in assemblies.Where(a => a != null))
					scan.Add(a);
			}
			else
			{
				var entry = Assembly.GetEntryAssembly();
				if (entry != null) scan.Add(entry);

				scan.Add(Assembly.GetCallingAssembly());
			}

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

	}
}
