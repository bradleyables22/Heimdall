
using Heimdall.Server.Helpers;
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

            app.MapPost("__heimdall/v1/content/actions", async (HttpContext ctx, ContentRegistry registry, IOptions<HeimdallServiceSettings> options) =>
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

					object? invokeResult = method.Invoke(null, args);

					if (invokeResult is null)
						return Results.NoContent();

					IHtmlContent? raw = invokeResult switch
					{
						IHtmlContent html => html,

						Task<IHtmlContent> taskHtml => await taskHtml,

						ValueTask<IHtmlContent> vtaskHtml => await vtaskHtml,

						// If someone returns Task (non-generic) or some other type, fail loudly
						_ => throw new InvalidOperationException(
								$"Heimdall action '{methodId}' returned unsupported type '{invokeResult.GetType().FullName}'. " +
								"Expected IHtmlContent, Task<IHtmlContent>, or ValueTask<IHtmlContent>.")
					};

					if (raw is null)
						return Results.NoContent();

					return Results.Content(raw.RenderHtml(), "text/html; charset=utf-8");
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
