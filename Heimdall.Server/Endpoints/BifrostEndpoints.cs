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

            var tokenHandler = BuildBifrostTokenHandler();
            var streamHandler = BuildBifrostStreamHandler();

            app.MapGet("__heimdall/v1/bifrost/token", tokenHandler).ExcludeFromDescription();
            app.MapGet("__heimdall/v1/bifrost", streamHandler).ExcludeFromDescription();

            return app;
		}

		internal static IApplicationBuilder MapHeimdallBifrostEndpoints(this IApplicationBuilder app)
		{
			var tokenHandler = BuildBifrostTokenHandler();
			var streamHandler = BuildBifrostStreamHandler();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapGet("__heimdall/v1/bifrost/token", tokenHandler);
				endpoints.MapGet("__heimdall/v1/bifrost", streamHandler);
			});

			return app;
		}

		private static RequestDelegate BuildBifrostTokenHandler() =>
			async ctx =>
			{
				var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
				var tokenSvc = ctx.RequestServices.GetRequiredService<BifrostSubscribeToken>();
				var options = ctx.RequestServices.GetRequiredService<IOptions<HeimdallServiceSettings>>();

				var topic = ctx.Request.Query["topic"].ToString()?.Trim();

				if (string.IsNullOrWhiteSpace(topic))
				{
					await Results.BadRequest("Querystring 'topic' is required.").ExecuteAsync(ctx);
					return;
				}

				try
				{
					await antiforgery.ValidateRequestAsync(ctx);
				}
				catch
				{
					await Results.Unauthorized().ExecuteAsync(ctx);
					return;
				}

				var authorizationResult = await AuthorizeBifrostTopicAsync(ctx, topic, options.Value);
				if (authorizationResult is not null)
				{
					await authorizationResult.ExecuteAsync(ctx);
					return;
				}

				var st = tokenSvc.Create(topic, ctx.User, TimeSpan.FromMinutes(2));
				await Results.Json(new { token = st, expiresInSeconds = 120 }).ExecuteAsync(ctx);
			};

		private static RequestDelegate BuildBifrostStreamHandler() =>
			async ctx =>
			{
				var bifrost = ctx.RequestServices.GetRequiredService<Bifrost>();
				var tokenSvc = ctx.RequestServices.GetRequiredService<BifrostSubscribeToken>();
				var options = ctx.RequestServices.GetRequiredService<IOptions<HeimdallServiceSettings>>();

				// validate topic and token
				var (valid, topic, tokenError) = TryValidateTopicAndToken(ctx, tokenSvc);
				if (!valid)
				{
					await tokenError!.ExecuteAsync(ctx);
					return;
				}

				// Set SSE headers
				ConfigureSseHeaders(ctx);

				var abort = ctx.RequestAborted;

				// Subscribe to topic (discard subscription id)
				var (_, reader, unsubscribe) = bifrost.Subscribe(topic!);
				abort.Register(unsubscribe);

				// initial event
				await WriteEventAsync(ctx, "heimdall:connected", $"topic:{topic}", null, abort);

				var heartbeatInterval = options.Value.BifrostHeartbeatInterval;
				if (heartbeatInterval <= TimeSpan.Zero)
					heartbeatInterval = TimeSpan.FromSeconds(15);

				try
				{
					await StreamReaderLoopAsync(ctx, reader, abort, heartbeatInterval);
				}
				catch (OperationCanceledException)
				{
					// Expected on disconnect
				}
				finally
				{
					unsubscribe();
				}
			};

		private static (bool valid, string? topic, IResult? error) TryValidateTopicAndToken(HttpContext ctx, BifrostSubscribeToken tokenSvc)
		{
			var topic = ctx.Request.Query["topic"].ToString()?.Trim();

			if (string.IsNullOrWhiteSpace(topic))
				return (false, null, Results.BadRequest("Querystring 'topic' is required."));

			var st = ctx.Request.Query["st"].ToString()?.Trim() ?? string.Empty;
			if (!tokenSvc.TryValidate(topic, st, ctx.User))
				return (false, null, Results.Unauthorized());

			return (true, topic, null);
		}

		private static void ConfigureSseHeaders(HttpContext ctx)
		{
			ctx.Response.Headers.CacheControl = "no-cache";
			ctx.Response.Headers["X-Accel-Buffering"] = "no";
			ctx.Response.ContentType = "text/event-stream";
		}

		private static async Task StreamReaderLoopAsync(HttpContext ctx, object readerObj, CancellationToken abort, TimeSpan heartbeatInterval)
		{
			var readerType = readerObj.GetType();

			var waitToRead = readerType.GetMethod("WaitToReadAsync", new[] { typeof(CancellationToken) })
				?? throw new InvalidOperationException("Reader does not support WaitToReadAsync(CancellationToken)");

			var tryRead = readerType.GetMethod("TryRead")
				?? throw new InvalidOperationException("Reader does not support TryRead(out T)");

			while (!abort.IsCancellationRequested)
			{
				var hasMessages = await WaitForMessagesAsync(readerObj, waitToRead, ctx, abort, heartbeatInterval);
				if (!hasMessages)
					break;

				await DrainReaderAsync(readerObj, tryRead, ctx, abort);
			}
		}

		private static async Task<bool> WaitForMessagesAsync(object readerObj, System.Reflection.MethodInfo waitToRead, HttpContext ctx, CancellationToken abort, TimeSpan heartbeatInterval)
		{
			using var idle = CancellationTokenSource.CreateLinkedTokenSource(abort);
			idle.CancelAfter(heartbeatInterval);

			try
			{
				var resultObj = waitToRead.Invoke(readerObj, new object[] { idle.Token });
				if (resultObj is null)
					return false;

				return await (dynamic)resultObj;
			}
			catch (OperationCanceledException) when (!abort.IsCancellationRequested)
			{
				await WriteCommentAsync(ctx, "ping", abort);
				return false;
			}
		}

		private static async Task DrainReaderAsync(object readerObj, System.Reflection.MethodInfo tryRead, HttpContext ctx, CancellationToken abort)
		{
			var args = new object[1];
			while (true)
			{
				var invoked = tryRead.Invoke(readerObj, args);
				if (!(invoked is bool ok) || !ok)
					break;

				var msg = args[0];
				var expires = (DateTimeOffset)msg.GetType().GetProperty("ExpiresUtc")!.GetValue(msg)!;
				if (expires <= DateTimeOffset.UtcNow)
					continue;

				var eventName = (string?)msg.GetType().GetProperty("EventName")!.GetValue(msg) ?? string.Empty;
				var html = (string?)msg.GetType().GetProperty("Html")!.GetValue(msg) ?? string.Empty;
				var id = (string?)msg.GetType().GetProperty("Id")!.GetValue(msg);

				await WriteEventAsync(ctx, eventName, html, id, abort);
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
