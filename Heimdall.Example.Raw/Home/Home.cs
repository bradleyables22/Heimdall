using Heimdall.Example.Raw.Home.Models;
using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using System.Net;
using System.Text;

namespace Heimdall.Example.Raw.Home
{
    public static class Home
    {
        [ContentInvocation]
        public static IHtmlContent Title()
        {
            return new HtmlString("Heimdall");
        }
        [ContentInvocation]
        public static IHtmlContent Header()
        {
            return new HtmlString($"Welcome to Heimdall Example Raw!");
        }
        
        [ContentInvocation]
        public static IHtmlContent PagedTable(HttpContext ctx, WeatherPageRequest req)
        {
            var page = Math.Max(0, req.Page);
            var pageSize = Math.Clamp(req.PageSize, 1, 50);

            var startDate = DateOnly.FromDateTime(DateTime.Now);
            var summaries = new[]
            {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild",
            "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

            var forecasts = Enumerable.Range(1 + (page * pageSize), pageSize)
                .Select(index => new WeatherForecast
                {
                    Date = startDate.AddDays(index),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = summaries[Random.Shared.Next(summaries.Length)]
                })
                .ToArray();

            var sb = new StringBuilder();

            foreach (var f in forecasts)
            {
                sb.AppendLine($"""
                <tr>
                    <td>{f.Date:MM/dd/yyyy}</td>
                    <td>{f.TemperatureC}</td>
                    <td>{f.TemperatureF}</td>
                    <td>{WebUtility.HtmlEncode(f.Summary ?? "")}</td>
                </tr>
                """);
            }

            return new HtmlString(sb.ToString());
        }


    }
}
