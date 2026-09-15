using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Heimdall.Server
{
	internal static class BifrostEndpoints
	{
		private static readonly RequestDelegate EmptyAuthorizationPipeline = _ => Task.CompletedTask;

		internal static IEndpointRouteBuilder MapHeimdallBifrostEndpoints(this IEndpointRouteBuilder app)
		{
			var logger = app.ServiceProvider
				.GetService<ILoggerFactory>()
				?.CreateLogger("Heimdall.Server.BifrostEndpoints");

            app.MapGet("__heimdall/v1/bifrost/token", async (
				HttpContext ctx,
				[FromServices] BifrostSubscribeToken tokenSvc,
				[FromServices] IOptions<HeimdallServiceSettings> options) =>
            {
                var topic = ctx.Request.Query["topic"].ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(topic))
                    return Results.BadRequest("Querystring 'topic' is required.");

                if (options.Value.EnableAntiforgery)
                {
                    var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
                    try
                    {
                        await antiforgery.ValidateRequestAsync(ctx);
                    }
                    catch (AntiforgeryValidationException ex)
                    {
                        logger?.LogWarning(
                            ex,
                            "Heimdall Bifrost token request failed antiforgery validation for topic {Topic} on {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
                            topic,
                            ctx.Request.Method,
                            ctx.Request.Path,
                            ctx.TraceIdentifier);

                        if (options.Value.EnableDetailedErrors)
                        {
                            return Results.Problem(
                                detail: ex.ToString(),
                                title: "Invalid Heimdall antiforgery token",
                                statusCode: StatusCodes.Status400BadRequest);
                        }

                        return Results.BadRequest("Invalid Heimdall antiforgery token.");
                    }
                }

				var authorizationResult = await AuthorizeBifrostTopicAsync(ctx, topic, options.Value);
				if (authorizationResult is not null)
					return authorizationResult;

                var st = tokenSvc.Create(topic, ctx.User, TimeSpan.FromMinutes(2));
                return Results.Json(new { token = st, expiresInSeconds = 120 });

            }).ExcludeFromDescription();



			app.MapGet("__heimdall/v1/bifrost", async (
				HttpContext ctx,
				[FromServices] Bifrost bifrost,
				[FromServices] BifrostSubscribeToken tokenSvc,
				[FromServices] IOptions<HeimdallServiceSettings> options) =>
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
				var subscription = bifrost.Subscribe(topic);
				var connection = BifrostConnectionInfo.Create(subscription.Id, topic, ctx.User);
				bifrost.RegisterConnection(connection, subscription.RequestDisconnect);
				var reader = subscription.Reader;
				using var abortRegistration = abort.Register(subscription.Unsubscribe);
				using var connectionTelemetry = HeimdallTelemetry.OpenBifrostConnection();
				var disconnectReason = "client-disconnected";

				var heartbeatInterval = options.Value.BifrostHeartbeatInterval;
				if (heartbeatInterval <= TimeSpan.Zero)
					heartbeatInterval = TimeSpan.FromSeconds(15);

				async Task<bool> WriteDisconnectIfRequestedAsync()
				{
					if (!subscription.DisconnectRequested.IsCompleted)
						return false;

					var reason = await subscription.DisconnectRequested;
					disconnectReason = reason;
					await WriteEventAsync(ctx, Bifrost.DisconnectEventName, reason, null, abort);
					return true;
				}

				try
				{
					await InvokeBifrostAuthenticatedHandlersAsync(
						ctx,
						connection,
						bifrost.Connections,
						options.Value);

					if (await WriteDisconnectIfRequestedAsync())
						return Results.Empty;

					// Optional initial event (helps with debugging / client readiness)
					await WriteEventAsync(ctx, "heimdall:connected", $"topic:{topic}", null, abort);

					while (!abort.IsCancellationRequested)
					{
						if (await WriteDisconnectIfRequestedAsync())
							break;

						// Wait for messages, but wake on idle so proxies don't close quiet streams.
						using var idle = CancellationTokenSource.CreateLinkedTokenSource(abort);
						idle.CancelAfter(heartbeatInterval);
						var waitForMessages = reader.WaitToReadAsync(idle.Token).AsTask();
						var completed = await Task.WhenAny(
							waitForMessages,
							subscription.DisconnectRequested);

						if (completed == subscription.DisconnectRequested)
						{
							await WriteDisconnectIfRequestedAsync();
							break;
						}

						try
						{
							if (!await waitForMessages)
								break;
						}
						catch (OperationCanceledException) when (!abort.IsCancellationRequested)
						{
							if (await WriteDisconnectIfRequestedAsync())
								break;

							await WriteCommentAsync(ctx, "ping", abort);
							continue;
						}

						var disconnected = false;
						while (reader.TryRead(out var msg))
						{
							if (await WriteDisconnectIfRequestedAsync())
							{
								disconnected = true;
								break;
							}

							// Drop expired messages
							if (msg.ExpiresUtc <= DateTimeOffset.UtcNow)
							{
								HeimdallTelemetry.RecordBifrostExpired(msg.EventName);
								continue;
							}

							await WriteEventAsync(
								ctx,
								eventName: msg.EventName,
								data: msg.Html,
								eventId: msg.Id,
								ct: abort
							);
						}

						if (disconnected)
							break;
					}
				}
				catch (OperationCanceledException)
				{
					// Expected on disconnect
					connectionTelemetry.Complete("cancelled");
				}
				catch (Exception ex)
				{
					disconnectReason = "server-error";
					connectionTelemetry.RecordException(ex);
					throw;
				}
				finally
				{
					var disconnectedConnection = bifrost.RemoveConnection(connection.ConnectionId) ?? connection;
					subscription.Unsubscribe();
					await InvokeBifrostDisconnectedHandlersAsync(
						ctx,
						disconnectedConnection,
						disconnectReason,
						options.Value,
						logger);
				}

				return Results.Empty;
			})
			.ExcludeFromDescription();

			return app;
		}

		private static async Task InvokeBifrostAuthenticatedHandlersAsync(
			HttpContext ctx,
			BifrostConnectionInfo connection,
			IBifrostConnectionStore connections,
			HeimdallServiceSettings settings)
		{
			await using var scope = ctx.RequestServices.CreateAsyncScope();
			var context = new BifrostAuthenticatedContext(
				ctx,
				connection,
				connections,
				scope.ServiceProvider);

			foreach (var handler in scope.ServiceProvider.GetServices<IBifrostConnectionHandler>())
				await handler.OnBifrostAuthenticatedAsync(context);

			if (settings.OnBifrostAuthenticated is not null)
				await settings.OnBifrostAuthenticated(context);
		}

		private static async Task InvokeBifrostDisconnectedHandlersAsync(
			HttpContext ctx,
			BifrostConnectionInfo connection,
			string reason,
			HeimdallServiceSettings settings,
			ILogger? logger)
		{
			try
			{
				await using var scope = ctx.RequestServices.CreateAsyncScope();
				var context = new BifrostDisconnectedContext(
					ctx,
					connection,
					reason,
					scope.ServiceProvider);

				foreach (var handler in scope.ServiceProvider.GetServices<IBifrostConnectionHandler>())
				{
					try
					{
						await handler.OnBifrostDisconnectedAsync(context);
					}
					catch (Exception ex)
					{
						logger?.LogError(
							ex,
							"Bifrost connection handler {HandlerType} failed during disconnect cleanup for connection {ConnectionId}.",
							handler.GetType().FullName,
							connection.ConnectionId);
					}
				}

				if (settings.OnBifrostDisconnected is not null)
				{
					try
					{
						await settings.OnBifrostDisconnected(context);
					}
					catch (Exception ex)
					{
						logger?.LogError(
							ex,
							"Inline Bifrost disconnect handler failed during cleanup for connection {ConnectionId}.",
							connection.ConnectionId);
					}
				}
			}
			catch (Exception ex)
			{
				logger?.LogError(
					ex,
					"Bifrost disconnect cleanup scope failed for connection {ConnectionId}.",
					connection.ConnectionId);
			}
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

			var handlers = ctx.RequestServices
				.GetServices<IBifrostTopicAuthHandler>()
				.ToArray();

			if (handlers.Length > 0)
			{
				var matchingHandlers = handlers
					.Where(handler => handler.CanHandle(topic))
					.ToArray();

				if (matchingHandlers.Length != 1)
					return CreateDeniedTopicResult(ctx);

				var authorizationContext = new BifrostTopicAuthorizationContext(ctx, topic);
				if (!await matchingHandlers[0].AuthorizeAsync(authorizationContext))
					return CreateDeniedTopicResult(ctx);
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
