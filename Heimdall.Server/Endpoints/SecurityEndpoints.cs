using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Server.Endpoints;

internal static class SecurityEndpoints
{
	internal static IEndpointRouteBuilder MapHeimdallSecurityEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapGet("__heimdall/v1/csrf", GetToken).ExcludeFromDescription();
		return app;
	}

	internal static IResult GetToken(HttpContext ctx)
	{
		var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
		var options = ctx.RequestServices.GetRequiredService<IOptions<HeimdallServiceSettings>>();
		var logger = ctx.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Heimdall.Server.SecurityEndpoints");
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
	}
}
