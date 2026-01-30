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
            => new HtmlString("Welcome to Heimdall Example Raw!");

        [ContentInvocation]
        public static IHtmlContent PagedTable(HttpContext ctx, WeatherPageRequest req)
        {
            var page = Math.Max(1, req.Page);
            var pageSize = Math.Clamp(req.PageSize, 1, 50);

            var offset = (page - 1) * pageSize;

            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild",
                "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            var startDate = DateOnly.FromDateTime(DateTime.Now);

            var rows = Enumerable.Range(offset + 1, pageSize)
                .Select(i => new WeatherForecast
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
            var step = Math.Max(1, req.Step);

            switch (op)
            {
                case "inc":
                    value += step;
                    break;
                case "dec":
                    value -= step;
                    break;
                case "reset":
                    value = 0;
                    break;
                case "get":
                default:
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
