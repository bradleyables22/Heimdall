using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server
{
	public static class HeimdallPageCollection
	{
		/// <summary>
		/// Adds an endpoint that serves a static HTML page from the wwwroot folder
		/// </summary>
		/// <param name="app"></param>
		/// <param name="configure"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static RouteHandlerBuilder MapHeimdallPage(this IEndpointRouteBuilder app, Action<HeimdallPageSettings> configure)
		{
			var settings = new HeimdallPageSettings();
			configure(settings);
			
			if (string.IsNullOrWhiteSpace(settings.Pattern))
				throw new InvalidOperationException("Pattern is required.");

			if (string.IsNullOrWhiteSpace(settings.RelativePath))
				throw new InvalidOperationException("RelativePath is required.");

			var rel = settings.RelativePath.Replace('\\', '/').TrimStart('/');

			var b = app.MapGet(settings.Pattern, async (HttpContext ctx) =>
			{
				var env = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>();
				var file = env.WebRootFileProvider.GetFileInfo(rel);

				if (!file.Exists)
					return Results.NotFound();

				return Results.File(file.PhysicalPath!, "text/html; charset=utf-8");
			});

			b.ExcludeFromDescription();
			return b;
		}
	}
}
