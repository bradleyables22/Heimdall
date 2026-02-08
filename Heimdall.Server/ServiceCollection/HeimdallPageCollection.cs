using Heimdall.Server.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Text;

namespace Heimdall.Server
{
    public static class HeimdallPageCollection
	{
		/// <summary>
        /// Registers an HTML-first Heimdall page as an endpoint in the ASP.NET routing system.
        /// </summary>
        /// <param name="app">
        /// The endpoint route builder used to register the route.
        /// </param>
        /// <param name="configure">
        /// A configuration delegate that defines how the page is rendered, including
        /// the route pattern, source HTML files, layout behavior, and dynamic layout components.
        /// </param>
        /// <returns>
        /// A <see cref="RouteHandlerBuilder"/> allowing additional endpoint configuration
        /// such as authorization, filters, and metadata.
        /// </returns>
		public static RouteHandlerBuilder MapHeimdallPage(
            this IEndpointRouteBuilder app,
            Action<HeimdallPageSettings> configure)
        {
            var settings = new HeimdallPageSettings();
            configure(settings);

            if (string.IsNullOrWhiteSpace(settings.Pattern))
                throw new InvalidOperationException("Pattern is required.");

            if (string.IsNullOrWhiteSpace(settings.PagePath))
                throw new InvalidOperationException("PagePath is required.");

            if (!string.IsNullOrWhiteSpace(settings.LayoutPath) &&
                string.IsNullOrWhiteSpace(settings.LayoutPlaceholder))
                throw new InvalidOperationException("LayoutPlaceholder is required when LayoutPath is set.");

            

            var pageRel = HeimdallHelpers.NormalizeWebRootPath(settings.PagePath);
            var layoutRel = string.IsNullOrWhiteSpace(settings.LayoutPath)
                ? null
                : HeimdallHelpers.NormalizeWebRootPath(settings.LayoutPath);

            var b = app.MapGet(settings.Pattern, async (HttpContext ctx) =>
            {
                var env = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var webRoot = env.WebRootFileProvider;

                IFileInfo pageFile = webRoot.GetFileInfo(pageRel);
                if (!pageFile.Exists)
                    return Results.NotFound();

                // Fast path: no layout
                if (layoutRel is null)
                    return Results.File(pageFile.PhysicalPath!, "text/html; charset=utf-8");

                IFileInfo layoutFile = webRoot.GetFileInfo(layoutRel);
                if (!layoutFile.Exists)
                    return Results.Problem($"Layout not found: {settings.LayoutPath}", statusCode: 500);

                static async Task<string> ReadFileAsync(IFileInfo file, CancellationToken ct)
                {
                    await using var stream = file.CreateReadStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    return await reader.ReadToEndAsync(ct);
                }

                var ct = ctx.RequestAborted;

                var layoutTask = ReadFileAsync(layoutFile, ct);
                var pageTask = ReadFileAsync(pageFile, ct);
                await Task.WhenAll(layoutTask, pageTask);

                var layoutHtml = layoutTask.Result;
                var pageHtml = pageTask.Result;

                if (!layoutHtml.Contains(settings.LayoutPlaceholder, StringComparison.Ordinal))
                {
                    return Results.Problem(
                        $"Layout placeholder not found. Expected: {settings.LayoutPlaceholder}",
                        statusCode: 500);
                }

                // 1) Inject page
                var fullHtml = layoutHtml.Replace(settings.LayoutPlaceholder, pageHtml, StringComparison.Ordinal);

                // 2) Inject layout components (prerendered placeholders)
                if (settings.LayoutComponents.Count > 0)
                {
                    var sp = ctx.RequestServices;

                    foreach (var (placeholder, renderer) in settings.LayoutComponents)
                    {
                        if (string.IsNullOrWhiteSpace(placeholder) || renderer is null)
                            continue;

                        // Optional: only render if the placeholder exists in the HTML
                        if (!fullHtml.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                            continue;

                        IHtmlContent html;
                        try
                        {
                            html = renderer(sp,ctx) ?? HtmlString.Empty;
                        }
                        catch (Exception ex)
                        {
                            return Results.Problem(
                                $"Error rendering layout component '{placeholder}': {ex.Message}",
                                statusCode: 500);
                        }

                        var renderedHtml = html.RenderHtml();
                        fullHtml = fullHtml.Replace(placeholder, renderedHtml, StringComparison.OrdinalIgnoreCase);
                    }
                }

                return Results.Text(fullHtml, "text/html; charset=utf-8");
            });

            b.ExcludeFromDescription();
            return b;
        }

    }
}
