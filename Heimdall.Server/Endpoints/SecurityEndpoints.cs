using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server.Endpoints
{
	internal static class SecurityEndpoints
	{
		internal static WebApplication MapHeimdallSecurityEndpoints(this WebApplication app)
		{
			var handler = BuildCsrfHandler();
			app.MapGet("__heimdall/v1/csrf", handler).ExcludeFromDescription();

			return app;
		}

		internal static IApplicationBuilder MapHeimdallSecurityEndpoints(this IApplicationBuilder app)
		{
			var handler = BuildCsrfHandler();
			app.UseEndpoints(endpoints => endpoints.MapGet("__heimdall/v1/csrf", handler));
			return app;
		}

		private static RequestDelegate BuildCsrfHandler() =>
			async ctx =>
			{
				var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
				var options = ctx.RequestServices.GetRequiredService<IOptions<HeimdallServiceSettings>>();
				var settings = options.Value;

				try
				{
					var tokens = antiforgery.GetAndStoreTokens(ctx);

					ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
					ctx.Response.Headers.Pragma = "no-cache";
					ctx.Response.Headers.Expires = "0";

					if (settings.EnableDetailedErrors)
					{
						ctx.Response.Headers["X-Heimdall-Csrf"] = "issued";
					}

					await Results.Json(new { requestToken = tokens.RequestToken }).ExecuteAsync(ctx);
				}
				catch (Exception ex)
				{
					if (settings.EnableDetailedErrors)
					{
						await Results.Problem(
							title: "Failed to issue CSRF token",
							detail: ex.ToString(),
							statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(ctx);
					}
					else
					{
						await Results.Problem(
							title: "Failed to issue CSRF token",
							statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(ctx);
					}
				}
			};
	}
}
