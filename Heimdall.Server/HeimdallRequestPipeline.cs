using Heimdall.Server.Endpoints;
using Heimdall.Server.Helpers;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Server;

/// <summary>Executes Heimdall requests in hosts that supply an ASP.NET Core HTTP context without endpoint routing.</summary>
/// <remarks>
/// Register Heimdall, antiforgery, and any authentication/authorization services before use.
/// The host owns routing, HTTP method checks, authentication, CORS, and infrastructure request limits.
/// These handlers use the request's service scope and cancellation token. They do not create a web server.
/// </remarks>
public static class HeimdallRequestPipeline
{
    /// <summary>Response header marking antiforgery rejection before application execution. Expose through CORS for cross-origin clients.</summary>
    public const string AntiforgeryFailureHeader = "X-Heimdall-Antiforgery";

    /// <summary>Executes a content invocation, including binding, authorization, antiforgery, and rendering.</summary>
    /// <remarks>Endpoint rate-limit policies require ASP.NET Core middleware and are rejected by this entry point.</remarks>
    public static Task<IResult> InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var registry = context.RequestServices.GetRequiredService<ContentInvocationRegistry>();
        if (registry.TryGet(ContentEndpoints.GetInvocationId(context.Request), out var action) &&
            action.RateLimitingMetadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            return Task.FromResult(Results.Problem(
                title: "Endpoint rate limiting is unavailable in this host.",
                detail: "Enforce rate limits at the host or gateway and remove EnableRateLimiting metadata, or use ASP.NET Core endpoint hosting.",
                statusCode: StatusCodes.Status501NotImplemented));
        }

        return ContentEndpoints.InvokeAsync(context);
    }

    /// <summary>Issues and stores an antiforgery token using the current request identity.</summary>
    public static IResult GetAntiforgeryToken(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SecurityEndpoints.GetToken(context);
    }

    /// <summary>Authorizes a topic and issues a signed Bifrost subscription token.</summary>
    public static Task<IResult> GetBifrostTokenAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return BifrostEndpoints.GetTokenAsync(context);
    }

    /// <summary>Validates a subscription token and streams Bifrost events until disconnection.</summary>
    /// <remarks>Await this method for the entire stream lifetime; never run it as background work.</remarks>
    public static Task<IResult> StreamBifrostAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return BifrostEndpoints.StreamAsync(context);
    }

    /// <summary>Renders a page using the request's services and cancellation token.</summary>
    public static async Task<IResult> RenderPageAsync(
        HttpContext context,
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(renderAsync);
        try
        {
            var html = await renderAsync(context.RequestServices, context).ConfigureAwait(false);
            return Results.Content(
                await (html ?? HtmlString.Empty).RenderHtmlAsync(context.RequestAborted).ConfigureAwait(false),
                "text/html; charset=utf-8");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(499);
        }
        catch (Exception ex)
        {
            context.RequestServices.GetService<ILoggerFactory>()?
                .CreateLogger("Heimdall.Server.PageEndpoints")
                .LogError(ex,
                    "Heimdall page render failed on {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
                    context.Request.Method,
                    context.Request.Path,
                    context.TraceIdentifier);

            return Results.Problem(
                title: "Heimdall page render failed.",
                detail: context.RequestServices.GetService<IOptions<HeimdallServiceSettings>>()?.Value.EnableDetailedErrors == true
                    ? ex.Message : null,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
