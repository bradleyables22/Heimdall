using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace Heimdall.Server
{
	internal static class BifrostEndpoints
	{
		internal static WebApplication MapHeimdallBifrostEndpoints(this WebApplication app)
		{

            app.MapGet("__heimdall/v1/bifrost/token", async ( HttpContext ctx, IAntiforgery antiforgery, BifrostSubscribeToken tokenSvc) =>
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

                var st = tokenSvc.Create(topic, TimeSpan.FromMinutes(2));
                return Results.Json(new { token = st, expiresInSeconds = 120 });

            }).ExcludeFromDescription();



            app.MapGet("__heimdall/v1/bifrost", async (HttpContext ctx, Bifrost bifrost, BifrostSubscribeToken tokenSvc) =>
			{
				var topic = ctx.Request.Query["topic"].ToString()?.Trim();

				if (string.IsNullOrWhiteSpace(topic))
					return Results.BadRequest("Querystring 'topic' is required.");

				var st = ctx.Request.Query["st"].ToString()?.Trim() ?? string.Empty;
                if (!tokenSvc.TryValidate(topic, st))
                    return Results.Unauthorized();

                // SSE headers
                ctx.Response.Headers.CacheControl = "no-cache";
				ctx.Response.Headers.Connection = "keep-alive";
				ctx.Response.Headers["X-Accel-Buffering"] = "no"; 
				ctx.Response.ContentType = "text/event-stream";

				var abort = ctx.RequestAborted;

				// Subscribe to topic
				var (id, reader, unsubscribe) = bifrost.Subscribe(topic);
				abort.Register(unsubscribe);

				// Optional initial event (helps with debugging / client readiness)
				await WriteEventAsync(ctx, "heimdall:connected", $"topic:{topic}", null, abort);

				var heartbeatInterval = TimeSpan.FromSeconds(15);
				var nextHeartbeat = DateTimeOffset.UtcNow.Add(heartbeatInterval);

				try
				{
					while (!abort.IsCancellationRequested)
					{
						// Heartbeat
						if (DateTimeOffset.UtcNow >= nextHeartbeat)
						{
							await WriteCommentAsync(ctx, "ping", abort);
							nextHeartbeat = DateTimeOffset.UtcNow.Add(heartbeatInterval);
						}

						// Wait for messages (or cancellation)
						if (!await reader.WaitToReadAsync(abort))
							break;

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
