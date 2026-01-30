
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server.Endpoints
{
	internal static class SecurityEndpoints
	{
		internal static WebApplication MapHeimdallSecurityEndpoints(this WebApplication app) 
		{
			app.MapGet("__heimdall/csrf", (HttpContext ctx) =>
			{
				var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
				var tokens = antiforgery.GetAndStoreTokens(ctx);

				return Results.Json(new { requestToken = tokens.RequestToken });
			});

			return app;
		}

	}
}
