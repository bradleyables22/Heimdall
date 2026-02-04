
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Heimdall.Server.Endpoints
{
	internal static class SecurityEndpoints
	{
		internal static WebApplication MapHeimdallSecurityEndpoints(this WebApplication app) 
		{
			app.MapGet("__heimdall/v1/csrf", (HttpContext ctx, IAntiforgery antiforgery, IOptions<HeimdallServiceSettings> options) =>
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
			});

			return app;
		}

	}
}
