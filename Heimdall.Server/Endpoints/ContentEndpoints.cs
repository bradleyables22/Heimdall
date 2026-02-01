
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;

namespace Heimdall.Server
{
	internal static class ContentEndpoints
	{
		private const string ActionHeader = "X-Heimdall-Content-Action";
		private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);


		internal static WebApplication MapHeimdallContentEndpoints(this WebApplication app) 
		{
			app.MapPost("__heimdall/v1/content/actions",
		async (HttpContext ctx, ContentRegistry registry, IOptions<HeimdallServiceSettings> options) =>
		{
			var settings = options.Value;

			var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
			await antiforgery.ValidateRequestAsync(ctx);

			if (!ctx.Request.Headers.TryGetValue(ActionHeader, out var values) ||
				string.IsNullOrWhiteSpace(values))
			{
				return Results.BadRequest($"Missing {ActionHeader} header.");
			}

			var methodId = values.ToString();

			if (!registry.TryGet(methodId, out var method))
				return Results.NotFound($"Unknown action '{methodId}'.");

			try
			{
				var args = await BindArgumentsAsync(ctx, method);

				IHtmlContent? raw = (IHtmlContent?)method.Invoke(null, args);


				if (raw is null)
					return Results.NoContent();

				return Results.Content(RenderHtml(raw), "text/html; charset=utf-8");
			}
			catch (Exception ex)
			{
				if (settings.EnableDetailedErrors)
				{
					var msg = ex is TargetInvocationException tie && tie.InnerException != null
						? tie.InnerException.ToString()
						: ex.ToString();

					return Results.Problem(
						detail: msg,
						title: "Heimdall action invocation failed",
						statusCode: StatusCodes.Status500InternalServerError);
				}

				return Results.Problem(
					title: "Heimdall action invocation failed",
					statusCode: StatusCodes.Status500InternalServerError);
			}
		});
			return app;
		}
		private static string RenderHtml(IHtmlContent content)
		{
			using var sw = new StringWriter();
			content.WriteTo(sw, System.Text.Encodings.Web.HtmlEncoder.Default);
			return sw.ToString();
		}
		private static async Task<object?[]> BindArgumentsAsync(HttpContext ctx, MethodInfo method)
		{
			var parameters = method.GetParameters();
			if (parameters.Length == 0)
				return Array.Empty<object?>();

			var args = new object?[parameters.Length];
			JsonElement? bodyJson = null;
			bool bodyRead = false;
			bool payloadBound = false;

			for (int i = 0; i < parameters.Length; i++)
			{
				var p = parameters[i];
				var pt = p.ParameterType;

				if (pt == typeof(HttpContext))
				{
					args[i] = ctx;
					continue;
				}

				if (pt == typeof(CancellationToken))
				{
					args[i] = ctx.RequestAborted;
					continue;
				}

				if (pt == typeof(System.Security.Claims.ClaimsPrincipal))
				{
					args[i] = ctx.User;
					continue;
				}

				var service = ctx.RequestServices.GetService(pt);
				if (service != null)
				{
					args[i] = service;
					continue;
				}

				if (payloadBound)
				{
					throw new InvalidOperationException(
						$"Action '{method.DeclaringType?.Name}.{method.Name}' " +
						"has multiple non-DI parameters. Only one payload DTO is allowed.");
				}

				if (!bodyRead)
				{
					bodyRead = true;

					if (ctx.Request.ContentLength is null or 0)
						throw new InvalidOperationException("Request body is empty.");

					bodyJson = await JsonSerializer.DeserializeAsync<JsonElement>(
						ctx.Request.Body,
						JsonOptions);
				}

				args[i] = bodyJson!.Value.Deserialize(pt, JsonOptions)
					?? throw new InvalidOperationException(
						$"Failed to bind payload parameter '{p.Name}'.");

				payloadBound = true;
			}

			return args;
		}
	}
}
