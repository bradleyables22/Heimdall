using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Antiforgery;

namespace Heimdall.Functions;

/// <summary>Adapts HTTP-triggered Functions to the shared Heimdall request pipeline.</summary>
/// <remarks>Requires the isolated worker's ASP.NET Core integration. The application supplies HTTP triggers.</remarks>
public sealed class HeimdallFunctions
{
    /// <summary>Handles a route relative to __heimdall/v1, such as content/invocations or bifrost.</summary>
    /// <param name="request">The live ASP.NET Core request supplied by the Functions worker.</param>
    /// <param name="path">The catch-all route value, excluding __heimdall/v1 and any Functions route prefix.</param>
    /// <returns>An MVC result that executes the shared HTTP result on the live response.</returns>
    public async Task<IActionResult> HandleAsync(HttpRequest request, string? path)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = request.HttpContext;
        using var ambient = EnterContext(context);
        var route = (path ?? string.Empty).Trim('/');
        var isContent = route.Equals("content/invocations", StringComparison.OrdinalIgnoreCase) ||
            route.Equals("content/actions", StringComparison.OrdinalIgnoreCase);
        var isCsrf = route.Equals("csrf", StringComparison.OrdinalIgnoreCase);
        var isToken = route.Equals("bifrost/token", StringComparison.OrdinalIgnoreCase);
        var isStream = route.Equals("bifrost", StringComparison.OrdinalIgnoreCase);
        if (!isContent && !isCsrf && !isToken && !isStream)
            return Adapt(Results.NotFound());

        var method = isContent ? HttpMethods.Post : HttpMethods.Get;
        if (!string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.Allow = method;
            return Adapt(Results.StatusCode(StatusCodes.Status405MethodNotAllowed));
        }

        await AuthenticateAsync(context);
        var result = isContent ? await HeimdallRequestPipeline.InvokeAsync(context)
            : isCsrf ? HeimdallRequestPipeline.GetAntiforgeryToken(context)
            : isToken ? await HeimdallRequestPipeline.GetBifrostTokenAsync(context)
            : await HeimdallRequestPipeline.StreamBifrostAsync(context);
        return Adapt(result);
    }

    /// <summary>Renders a page inside a user-owned HTTP trigger.</summary>
    /// <remarks>Authenticate and authorize the page in the host before calling this method.</remarks>
    public async Task<IActionResult> RenderPageAsync(HttpRequest request,
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var ambient = EnterContext(request.HttpContext);
        await AuthenticateAsync(request.HttpContext);
        return Adapt(await HeimdallRequestPipeline.RenderPageAsync(request.HttpContext, renderAsync));
    }

    /// <summary>Authorizes a page with the default policy or a named policy before invoking its renderer.</summary>
    public async Task<IActionResult> RenderAuthorizedPageAsync(HttpRequest request,
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync, string? policyName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(renderAsync);
        var denied = await AuthorizeAsync(request, policyName);
        if (denied is not null) return denied;
        using var ambient = EnterContext(request.HttpContext);
        return Adapt(await HeimdallRequestPipeline.RenderPageAsync(request.HttpContext, renderAsync));
    }

    /// <summary>Authorizes a request with the default or named policy. Returns null on success or a completed challenge/forbid result.</summary>
    public async Task<IActionResult?> AuthorizeAsync(HttpRequest request, string? policyName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = request.HttpContext;
        var provider = context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = policyName is null ? await provider.GetDefaultPolicyAsync()
            : await provider.GetPolicyAsync(policyName)
                ?? throw new InvalidOperationException($"Authorization policy '{policyName}' was not found.");
        return await AuthorizePolicyAsync(request, policy);
    }

    internal async Task<IActionResult?> AuthorizeAsync(HttpRequest request, IEnumerable<IAuthorizeData> authorizeData)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = request.HttpContext.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(provider, authorizeData)
            ?? throw new InvalidOperationException("Page authorization metadata did not produce a policy.");
        return await AuthorizePolicyAsync(request, policy);
    }

    private static async Task<IActionResult?> AuthorizePolicyAsync(HttpRequest request, AuthorizationPolicy policy)
    {
        var context = request.HttpContext;
        using var ambient = EnterContext(context);
        await AuthenticateAsync(context);
        var evaluator = context.RequestServices.GetRequiredService<IPolicyEvaluator>();
        var authenticated = await evaluator.AuthenticateAsync(policy, context);
        var authorized = await evaluator.AuthorizeAsync(policy, authenticated, context, context);
        if (!authorized.Succeeded)
        {
            await context.RequestServices.GetRequiredService<IAuthorizationMiddlewareResultHandler>()
                .HandleAsync(_ => Task.CompletedTask, context, policy, authorized);
            return Adapt(Results.Empty);
        }
        return null;
    }

    /// <summary>Executes remote authentication callbacks, including OIDC correlation and state validation.</summary>
    /// <remarks>Forward the configured callback paths to this method using GET and POST HTTP triggers.</remarks>
    public async Task<IActionResult> HandleAuthenticationAsync(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = request.HttpContext;
        using var ambient = EnterContext(context);
        context.Features.Set<IAuthenticationFeature>(new AuthenticationFeature
        {
            OriginalPath = request.Path,
            OriginalPathBase = request.PathBase
        });
        var schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        foreach (var scheme in await schemes.GetRequestHandlerSchemesAsync())
        {
            if (await handlers.GetHandlerAsync(context, scheme.Name) is IAuthenticationRequestHandler handler &&
                await handler.HandleRequestAsync())
                return Adapt(Results.Empty);
        }
        return Adapt(Results.NotFound());
    }

    /// <summary>Starts sign-in using a registered authentication scheme and a local return URL.</summary>
    public async Task<IActionResult> ChallengeAsync(HttpRequest request, string scheme, string returnUrl = "/")
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ValidateReturnUrl(returnUrl);
        using var ambient = EnterContext(request.HttpContext);
        await AuthenticateAsync(request.HttpContext);
        return Adapt(Results.Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, [scheme]));
    }

    /// <summary>Validates a POST antiforgery token, then signs out the supplied cookie and optional remote schemes.</summary>
    public async Task<IActionResult> SignOutAsync(HttpRequest request, string returnUrl = "/", params string[] schemes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateReturnUrl(returnUrl);
        using var ambient = EnterContext(request.HttpContext);
        if (schemes.Length == 0 || schemes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Supply the authentication schemes to sign out.", nameof(schemes));
        if (!HttpMethods.IsPost(request.Method))
        {
            request.HttpContext.Response.Headers.Allow = "POST";
            return Adapt(Results.StatusCode(StatusCodes.Status405MethodNotAllowed));
        }
        await AuthenticateAsync(request.HttpContext);
        try
        {
            await request.HttpContext.RequestServices.GetRequiredService<IAntiforgery>()
                .ValidateRequestAsync(request.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            request.HttpContext.Response.Headers[HeimdallRequestPipeline.AntiforgeryFailureHeader] = "invalid";
            return Adapt(Results.BadRequest("Invalid logout antiforgery token."));
        }
        return Adapt(Results.SignOut(new AuthenticationProperties { RedirectUri = returnUrl }, schemes));
    }

    private static void ValidateReturnUrl(string returnUrl)
    {
        // Match the local absolute-path form without accepting scheme-relative URLs or control characters.
        if (string.IsNullOrEmpty(returnUrl) || returnUrl[0] != '/' ||
            (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')) ||
            returnUrl.Any(char.IsControl))
            throw new ArgumentException("Return URL must be a local absolute path.", nameof(returnUrl));
    }

    private static IDisposable? EnterContext(HttpContext context)
    {
        // The isolated worker supplies a live request but does not populate its own
        // IHttpContextAccessor. MVC/Razor and Blazor renderers need this ambient context.
        var accessor = context.RequestServices.GetRequiredService<IHttpContextAccessor>();
        if (ReferenceEquals(accessor.HttpContext, context)) return null;
        var previous = accessor.HttpContext;
        accessor.HttpContext = context;
        return new RestoreContext(accessor, previous);
    }

    private sealed class RestoreContext(IHttpContextAccessor accessor, HttpContext? previous) : IDisposable
    {
        public void Dispose() => accessor.HttpContext = previous;
    }

    private static async Task AuthenticateAsync(HttpContext context)
    {
        // Functions does not run ASP.NET Core authentication middleware. Resolve the configured
        // default scheme before minting identity-bound CSRF and Bifrost tokens or running content invocations.
        var schemes = context.RequestServices.GetService<IAuthenticationSchemeProvider>();
        if (schemes is not null && await schemes.GetDefaultAuthenticateSchemeAsync() is { } scheme)
        {
            var authentication = await context.AuthenticateAsync(scheme.Name);
            if (authentication.Succeeded && authentication.Principal is not null)
                context.User = authentication.Principal;
        }
    }

    internal static IActionResult Adapt(IResult result) => new HttpResult(result);

    private sealed class HttpResult(IResult result) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            using var ambient = EnterContext(context.HttpContext);
            await result.ExecuteAsync(context.HttpContext);
        }
    }
}
