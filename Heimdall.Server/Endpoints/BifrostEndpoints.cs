using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;

namespace Heimdall.Server
{
	internal static class BifrostEndpoints
	{
		private static readonly RequestDelegate EmptyAuthorizationPipeline = _ => Task.CompletedTask;

		internal static WebApplication MapHeimdallBifrostEndpoints(this WebApplication app)
		{

            app.MapGet("__heimdall/v1/bifrost/token", async (
				HttpContext ctx,
				IAntiforgery antiforgery,
				BifrostSubscribeToken tokenSvc,
				IOptions<HeimdallServiceSettings> options) =>
            {
                var topic = ctx.Request.Query["topic"].ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(topic))
                    return Results.BadRequest("Querystring 'topic' is required.");

                try
                {
                    await antiforgery.ValidateRequestAsync(ctx);
                }
                catch
                {
                    return Results.Unauthorized();
                }

				var authorizationResult = await AuthorizeBifrostTopicAsync(ctx, topic, options.Value);
				if (authorizationResult is not null)
					return authorizationResult;

                var st = tokenSvc.Create(topic, ctx.User, TimeSpan.FromMinutes(2));
                return Results.Json(new { token = st, expiresInSeconds = 120 });

            }).ExcludeFromDescription();



            app.MapGet("__heimdall/v1/bifrost", async (
				HttpContext ctx,
				Bifrost bifrost,
				BifrostSubscribeToken tokenSvc,
				IOptions<HeimdallServiceSettings> options) =>
			{
				var topic = ctx.Request.Query["topic"].ToString()?.Trim();

				if (string.IsNullOrWhiteSpace(topic))
					return Results.BadRequest("Querystring 'topic' is required.");

				var st = ctx.Request.Query["st"].ToString()?.Trim() ?? string.Empty;
                if (!tokenSvc.TryValidate(topic, st, ctx.User))
                    return Results.Unauthorized();

                // SSE headers
                ctx.Response.Headers.CacheControl = "no-cache";
				ctx.Response.Headers["X-Accel-Buffering"] = "no"; 
				ctx.Response.ContentType = "text/event-stream";

				var abort = ctx.RequestAborted;

				// Subscribe to topic
				var (id, reader, unsubscribe) = bifrost.Subscribe(topic);
				abort.Register(unsubscribe);

				// Optional initial event (helps with debugging / client readiness)
				await WriteEventAsync(ctx, "heimdall:connected", $"topic:{topic}", null, abort);

				var heartbeatInterval = options.Value.BifrostHeartbeatInterval;
				if (heartbeatInterval <= TimeSpan.Zero)
					heartbeatInterval = TimeSpan.FromSeconds(15);

				try
				{
					while (!abort.IsCancellationRequested)
					{
						// Wait for messages, but wake on idle so proxies don't close quiet streams.
						using var idle = CancellationTokenSource.CreateLinkedTokenSource(abort);
						idle.CancelAfter(heartbeatInterval);

						try
						{
							if (!await reader.WaitToReadAsync(idle.Token))
								break;
						}
						catch (OperationCanceledException) when (!abort.IsCancellationRequested)
						{
							await WriteCommentAsync(ctx, "ping", abort);
							continue;
						}

						while (reader.TryRead(out var msg))
						{
							// Drop expired messages
							if (msg.ExpiresUtc <= DateTimeOffset.UtcNow)
								continue;

							await WriteEventAsync(
								ctx,
								eventName: "heimdall",
								data: msg.Html,
								eventId: msg.Id,
								ct: abort
							);
						}
					}
				}
				catch (OperationCanceledException)
				{
					// Expected on disconnect
				}
				finally
				{
					unsubscribe();
				}

				return Results.Empty;
			})
			.ExcludeFromDescription();

			return app;
		}

		private static async Task<IResult?> AuthorizeBifrostTopicAsync(
			HttpContext ctx,
			string topic,
			HeimdallServiceSettings settings)
		{
			if (!string.IsNullOrWhiteSpace(settings.BifrostTopicPolicy))
			{
				var policyResult = await AuthorizeBifrostTopicPolicyAsync(ctx, topic, settings.BifrostTopicPolicy);
				if (policyResult is not null)
					return policyResult;
			}

			if (settings.AuthorizeBifrostTopic is not null &&
				!await settings.AuthorizeBifrostTopic(ctx, topic))
			{
				return CreateDeniedTopicResult(ctx);
			}

			return null;
		}

		private static async Task<IResult?> AuthorizeBifrostTopicPolicyAsync(
			HttpContext ctx,
			string topic,
			string policyName)
		{
			var policyProvider = GetRequiredAuthorizationService<IAuthorizationPolicyProvider>(ctx);
			var policy = await policyProvider.GetPolicyAsync(policyName);

			if (policy is null)
				throw new InvalidOperationException($"Bifrost topic authorization policy '{policyName}' was not found.");

			var resource = new BifrostTopicResource(topic, ctx);
			var policyEvaluator = GetRequiredAuthorizationService<IPolicyEvaluator>(ctx);
			var authenticateResult = await policyEvaluator.AuthenticateAsync(policy, ctx);
			var authorizeResult = await policyEvaluator.AuthorizeAsync(policy, authenticateResult, ctx, resource);

			if (authorizeResult.Succeeded)
				return null;

			var resultHandler = GetRequiredAuthorizationService<IAuthorizationMiddlewareResultHandler>(ctx);
			await resultHandler.HandleAsync(EmptyAuthorizationPipeline, ctx, policy, authorizeResult);

			return Results.Empty;
		}

		private static T GetRequiredAuthorizationService<T>(HttpContext ctx)
			where T : notnull
		{
			return ctx.RequestServices.GetService<T>()
				?? throw new InvalidOperationException(
					$"Bifrost topic authorization requires '{typeof(T).FullName}'. " +
					"Register authorization services with services.AddAuthorization(...).");
		}

		private static IResult CreateDeniedTopicResult(HttpContext ctx)
			=> ctx.User.Identity?.IsAuthenticated == true
				? Results.Forbid()
				: Results.Challenge();

		private static async Task WriteEventAsync(HttpContext ctx, string eventName, string data, string? eventId, CancellationToken ct)
		{
			var sb = new StringBuilder();

			if (!string.IsNullOrWhiteSpace(eventName))
				sb.Append("event: ").Append(eventName).Append('\n');

			if (!string.IsNullOrWhiteSpace(eventId))
				sb.Append("id: ").Append(eventId).Append('\n');

			if (data is null)
				data = string.Empty;

			using (var sr = new StringReader(data))
			{
				string? line;
				while ((line = sr.ReadLine()) is not null)
				{
					sb.Append("data: ").Append(line).Append('\n');
				}
			}

			sb.Append('\n');

			await ctx.Response.WriteAsync(sb.ToString(), ct);
			await ctx.Response.Body.FlushAsync(ct);
		}

		private static async Task WriteCommentAsync(HttpContext ctx, string comment, CancellationToken ct)
		{
			await ctx.Response.WriteAsync($": {comment}\n\n", ct);
			await ctx.Response.Body.FlushAsync(ct);
		}
	}
}
