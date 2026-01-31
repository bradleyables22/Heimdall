using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server
{
	public sealed class HeimdallPageOptions
	{
		public string Pattern { get; set; } = string.Empty;
		public string RelativePath { get; set; } = string.Empty;
	}

	public static class HeimdallPageCollection
	{
		public static RouteHandlerBuilder MapHeimdallPage(
			this IEndpointRouteBuilder app,
			Action<HeimdallPageOptions> configure)
		{
			var options = new HeimdallPageOptions();
			configure(options);

			if (string.IsNullOrWhiteSpace(options.Pattern))
				throw new InvalidOperationException("Pattern is required.");

			if (string.IsNullOrWhiteSpace(options.RelativePath))
				throw new InvalidOperationException("RelativePath is required.");

			var rel = options.RelativePath.Replace('\\', '/').TrimStart('/');

			var b = app.MapGet(options.Pattern, async (HttpContext ctx) =>
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
