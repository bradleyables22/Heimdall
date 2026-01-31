using Heimdall.Example.Raw.Home.Models;
using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using System.Net;
using System.Text;

namespace Heimdall.Example.Raw.Home
{
	public static class Home
	{
		private const string CounterSessionKey = "heimdall:counter";


		[ContentInvocation]
		public static IHtmlContent Title()
			=> new HtmlString("Heimdall");

		[ContentInvocation]
		public static IHtmlContent Header()
			=> new HtmlString(
				"Playground: load • click • change • input • submit • keydown • blur • hover • visible • scroll"
			);

		[ContentInvocation]
		public static IHtmlContent WeatherTable(HttpContext ctx, WeatherTableRequest req)
		{
			var page = Math.Max(1, req.Page);
			var pageSize = Math.Clamp(req.PageSize, 1, 50);
			var offset = (page - 1) * pageSize;

			var q = (req.Q ?? "").Trim();
			var qLower = q.ToLowerInvariant();

			var summaries = new[]
			{
				"Freezing", "Bracing", "Chilly", "Cool", "Mild",
				"Warm", "Balmy", "Hot", "Sweltering", "Scorching"
			};

			if (!string.IsNullOrWhiteSpace(qLower))
			{
				summaries = summaries
					.Where(s => s.ToLowerInvariant().Contains(qLower))
					.ToArray();

				if (summaries.Length == 0)
				{
					return new HtmlString($"""
                    <tr>
                        <td colspan="4" class="p-4 text-center text-muted">
                            No results for <strong>{WebUtility.HtmlEncode(q)}</strong>.
                        </td>
                    </tr>
                    """);
				}
			}

			var startDate = DateOnly.FromDateTime(DateTime.Now);

			var rows = Enumerable.Range(offset + 1, pageSize)
				.Select(i => new WeatherRow
				{
					Date = startDate.AddDays(i),
					TemperatureC = Random.Shared.Next(-20, 55),
					Summary = summaries[Random.Shared.Next(summaries.Length)]
				});

			var sb = new StringBuilder();

			foreach (var r in rows)
			{
				sb.AppendLine($"""
                <tr>
                    <td>{r.Date:MM/dd/yyyy}</td>
                    <td>{r.TemperatureC}</td>
                    <td>{r.TemperatureF}</td>
                    <td>{WebUtility.HtmlEncode(r.Summary)}</td>
                </tr>
                """);
			}

			return new HtmlString(sb.ToString());
		}

		[ContentInvocation]
		public static IHtmlContent ProfileSave(HttpContext ctx, ProfileSaveRequest req)
		{
			var name = (req.DisplayName ?? "").Trim();
			var animal = (req.FavoriteAnimal ?? "").Trim();

			if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(animal))
			{
				return new HtmlString("""
                <div class="text-muted">
                    Nothing to save yet…
                </div>
                """);
			}

			return new HtmlString($"""
            <div class="d-flex align-items-center justify-content-between flex-wrap gap-2">
                <div>
                    <div class="fw-semibold">Saved</div>
                    <div class="small text-muted">
                        <span class="me-2">name:
                            <span class="fw-semibold">{WebUtility.HtmlEncode(name)}</span>
                        </span>
                        <span>animal:
                            <span class="fw-semibold">{WebUtility.HtmlEncode(animal)}</span>
                        </span>
                    </div>
                </div>
                <div class="small text-muted">
                    {DateTime.Now:hh:mm:ss tt}
                </div>
            </div>
            """);
		}


		[ContentInvocation]
		public static IHtmlContent HoverCard(HttpContext ctx, HoverCardRequest req)
		{
			var topic = (req.Topic ?? "unknown").ToLowerInvariant();

			(string title, string body) = topic switch
			{
				"caching" => (
					"CSRF caching",
					"Heimdall caches the antiforgery token client-side and retries once if validation fails."
				),
				"csrf" => (
					"Antiforgery",
					"Requests include a RequestVerificationToken header and are retried once if invalid."
				),
				"swap" => (
					"Swap modes",
					"inner replaces content; outer replaces the node; beforeend/afterbegin append or prepend."
				),
				_ => (
					"Hover preview",
					"Hover triggers can fetch server HTML without writing custom JS."
				)
			};

			return new HtmlString($"""
            <div class="d-flex align-items-start justify-content-between gap-3">
                <div>
                    <div class="fw-semibold">{WebUtility.HtmlEncode(title)}</div>
                    <div class="text-muted small">{WebUtility.HtmlEncode(body)}</div>
                </div>
                <span class="badge text-bg-light border">
                    topic: {WebUtility.HtmlEncode(topic)}
                </span>
            </div>
            """);
		}

		// ============================================================
		// LAZY / VISIBLE
		// Demonstrates:
		// IntersectionObserver trigger
		// ============================================================

		[ContentInvocation]
		public static IHtmlContent LazyStats(HttpContext ctx)
		{
			var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
			var uptimeText = uptime.ToString(@"hh\:mm\:ss");
			return new HtmlString($"""
            <div class="d-flex align-items-center justify-content-between flex-wrap gap-2">
                <div>
                    <div class="fw-semibold">Lazy-loaded stats</div>
                    <div class="small text-muted">
                        server time:
                        <span class="fw-semibold">{DateTime.Now:hh:mm:ss tt}</span>
                        <span class="ms-2">
                            uptime-ish:
                            <span class="fw-semibold">{uptimeText}</span>
                        </span>
                    </div>
                </div>
                <span class="badge text-bg-success">visible trigger</span>
            </div>
            """);
		}

		// ============================================================
		// INFINITE SCROLL
		// Demonstrates:
		// scroll trigger + append
		// ============================================================

		public sealed class ActivityMoreRequest
		{
			public int Cursor { get; set; }
			public int Batch { get; set; } = 10;
		}

		[ContentInvocation]
		public static IHtmlContent ActivityMore(HttpContext ctx, ActivityMoreRequest req)
		{
			var cursor = Math.Max(0, req.Cursor);
			var batch = Math.Clamp(req.Batch, 1, 50);

			var sb = new StringBuilder();

			for (var i = 0; i < batch; i++)
			{
				var idx = cursor + i + 1;
				sb.AppendLine($"""
                <li class="list-group-item d-flex align-items-center justify-content-between">
                    <div>
                        <div class="fw-semibold">Event #{idx}</div>
                        <div class="small text-muted">
                            Generated at {DateTime.Now:hh:mm:ss tt}
                        </div>
                    </div>
                    <span class="badge text-bg-light border">append</span>
                </li>
                """);
			}

			// metadata marker (read by client, then removed)
			sb.AppendLine($"""
            <li class="d-none" data-next-cursor="{cursor + batch}"></li>
            """);

			return new HtmlString(sb.ToString());
		}


		[ContentInvocation]
		public static IHtmlContent CounterShow(HttpContext ctx)
		{
			var value = GetCounter(ctx);
			return RenderCounter(value);
		}

		[ContentInvocation]
		public static IHtmlContent CounterOp(HttpContext ctx, CounterOpRequest req)
		{
			var value = GetCounter(ctx);
			var op = (req.Op ?? "get").ToLowerInvariant();

			switch (op)
			{
				case "inc":
					value += Math.Max(1, req.Step);
					break;
				case "dec":
					value -= Math.Max(1, req.Step);
					break;
				case "reset":
					value = 0;
					break;
			}

			SetCounter(ctx, value);
			return RenderCounter(value);
		}

		private static int GetCounter(HttpContext ctx)
			=> ctx.Session.GetInt32(CounterSessionKey) ?? 0;

		private static void SetCounter(HttpContext ctx, int value)
			=> ctx.Session.SetInt32(CounterSessionKey, value);

		private static IHtmlContent RenderCounter(int value)
		{
			return new HtmlString($"""
            <div class="d-flex align-items-center justify-content-between">
                <div class="fw-semibold fs-4" data-counter-value="{value}">
                    {value}
                </div>
                <div class="small text-muted">server-owned</div>
            </div>
            """);
		}
	}
}
