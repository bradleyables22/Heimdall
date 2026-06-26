
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Server.Endpoints
{
	internal static class SecurityEndpoints
	{
		internal static IEndpointRouteBuilder MapHeimdallSecurityEndpoints(this IEndpointRouteBuilder app)
		{
			var logger = app.ServiceProvider
				.GetService<ILoggerFactory>()
				?.CreateLogger("Heimdall.Server.SecurityEndpoints");

			app.MapGet("__heimdall/v1/csrf", (
				HttpContext ctx,
				[FromServices] IAntiforgery antiforgery,
				[FromServices] IOptions<HeimdallServiceSettings> options) =>
			{
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

					return Results.Json(new { requestToken = tokens.RequestToken });
				}
				catch (Exception ex)
				{
					logger?.LogError(
						ex,
						"Failed to issue Heimdall CSRF token for {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
						ctx.Request.Method,
						ctx.Request.Path,
						ctx.TraceIdentifier);

					if (settings.EnableDetailedErrors)
					{
						return Results.Problem(
							title: "Failed to issue CSRF token",
							detail: ex.ToString(),
							statusCode: StatusCodes.Status500InternalServerError);
					}

					return Results.Problem(
						title: "Failed to issue CSRF token",
						statusCode: StatusCodes.Status500InternalServerError);
				}
			}).ExcludeFromDescription();

			return app;
		}

	}
}
