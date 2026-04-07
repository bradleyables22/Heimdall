using Heimdall.Server.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Heimdall.Server
{
    /// <summary>
    /// Provides extension methods for mapping Heimdall HTML-first page endpoints to an ASP.NET Core application's
    /// routing system.
    /// </summary>
    /// <remarks>These methods enable the registration of endpoints that render HTML content using
    /// user-supplied rendering delegates. Multiple overloads are provided to support both synchronous and asynchronous
    /// rendering, as well as varying levels of dependency injection. Layouts, partials, and page composition are
    /// handled by user code. Endpoints mapped with these methods are excluded from OpenAPI/Swagger documentation by
    /// default.</remarks>
    public static class HeimdallPageCollection
    {
        private const string DefaultContentType = "text/html; charset=utf-8";

        /// <summary>
        /// Maps a Heimdall HTML-first page endpoint.
        /// 
        /// Core primitive:
        /// Pattern → (IServiceProvider, HttpContext) → IHtmlContent → HTML response.
        /// 
        /// Layouts, partials, and composition are handled by user code.
        /// </summary>
        public static RouteHandlerBuilder MapHeimdallPage(
            this IEndpointRouteBuilder app,
           [StringSyntax("Route")] string pattern,
            Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync)
        {
            if (app is null)
                throw new ArgumentNullException(nameof(app));

            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern is required.", nameof(pattern));

            if (renderAsync is null)
                throw new ArgumentNullException(nameof(renderAsync));

            var builder = app.MapGet(pattern, async (HttpContext ctx) =>
            {
                var sp = ctx.RequestServices;

                try
                {
                    var html = await renderAsync(sp, ctx).ConfigureAwait(false);
                    html ??= HtmlString.Empty;

                    var rendered = html.RenderHtml();
                    return Results.Text(rendered, DefaultContentType, Encoding.UTF8);
                }
                catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
                {
                    // Client disconnected (tab closed, navigation away, proxy timeout).
                    return Results.StatusCode(499);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        title: "Heimdall page render failed.",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            builder.ExcludeFromDescription();
            return builder;
        }

        /// <summary>
        /// Synchronous overload.
        /// </summary>
        public static RouteHandlerBuilder MapHeimdallPage(
            this IEndpointRouteBuilder app,
            [StringSyntax("Route")] string pattern,
            Func<IServiceProvider, HttpContext, IHtmlContent> render)
        {
            if (render is null)
                throw new ArgumentNullException(nameof(render));

            return app.MapHeimdallPage(pattern, (sp, ctx) => Task.FromResult(render(sp, ctx)));
        }

        /// <summary>
        /// Convenience overload when only HttpContext is needed.
        /// </summary>
        public static RouteHandlerBuilder MapHeimdallPage(
            this IEndpointRouteBuilder app,
            [StringSyntax("Route")] string pattern,
            Func<HttpContext, Task<IHtmlContent>> renderAsync)
        {
            if (renderAsync is null)
                throw new ArgumentNullException(nameof(renderAsync));

            return app.MapHeimdallPage(pattern, (_, ctx) => renderAsync(ctx));
        }

        /// <summary>
        /// Convenience overload when only HttpContext is needed (sync).
        /// </summary>
        public static RouteHandlerBuilder MapHeimdallPage(
            this IEndpointRouteBuilder app,
            [StringSyntax("Route")] string pattern,
            Func<HttpContext, IHtmlContent> render)
        {
            if (render is null)
                throw new ArgumentNullException(nameof(render));

            return app.MapHeimdallPage(pattern, (_, ctx) => render(ctx));
        }

        /// <summary>
        /// Convenience overload for fully static pages.
        /// </summary>
        public static RouteHandlerBuilder MapHeimdallPage(
            this IEndpointRouteBuilder app,
            [StringSyntax("Route")] string pattern,
            Func<IHtmlContent> render)
        {
            if (render is null)
                throw new ArgumentNullException(nameof(render));

            return app.MapHeimdallPage(pattern, (_, __) => render());
        }
    }
}
