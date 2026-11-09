using Heimdall.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Heimdall.Functions;

/// <summary>Registers ASP.NET Core page route patterns for one Functions HTTP trigger.</summary>
public sealed class HeimdallFunctionPages
{
    private static readonly HashSet<string> ReservedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "__heimdall", "assets", "_content"
    };
    private readonly List<HeimdallFunctionPageBuilder> _pages = [];
    private readonly HashSet<string> _patterns = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps an asynchronous page with request services and route values.</summary>
    public HeimdallFunctionPageBuilder MapPage(string pattern,
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync) => Add(pattern, renderAsync);

    /// <summary>Maps an asynchronous page while preserving its concrete HTML type.</summary>
    public HeimdallFunctionPageBuilder MapPage<THtml>(string pattern,
        Func<IServiceProvider, HttpContext, Task<THtml>> renderAsync) where THtml : IHtmlContent
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        return Add(pattern, async (services, context) => await renderAsync(services, context));
    }

    /// <summary>Maps a value-task page while preserving its concrete HTML type.</summary>
    public HeimdallFunctionPageBuilder MapPage<THtml>(string pattern,
        Func<IServiceProvider, HttpContext, ValueTask<THtml>> renderAsync) where THtml : IHtmlContent
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        return Add(pattern, async (services, context) => await renderAsync(services, context));
    }

    /// <summary>Maps a synchronous page with request services and route values.</summary>
    public HeimdallFunctionPageBuilder MapPage(string pattern,
        Func<IServiceProvider, HttpContext, IHtmlContent> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return Add(pattern, (services, context) => Task.FromResult(render(services, context)));
    }

    /// <summary>Maps an asynchronous page with access to the HTTP context.</summary>
    public HeimdallFunctionPageBuilder MapPage(string pattern, Func<HttpContext, Task<IHtmlContent>> renderAsync)
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        return Add(pattern, (_, context) => renderAsync(context));
    }

    /// <summary>Maps an asynchronous page with access to the HTTP context.</summary>
    public HeimdallFunctionPageBuilder MapPage<THtml>(string pattern,
        Func<HttpContext, Task<THtml>> renderAsync) where THtml : IHtmlContent
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        return Add(pattern, async (_, context) => await renderAsync(context));
    }

    /// <summary>Maps a value-task page with access to the HTTP context.</summary>
    public HeimdallFunctionPageBuilder MapPage<THtml>(string pattern,
        Func<HttpContext, ValueTask<THtml>> renderAsync) where THtml : IHtmlContent
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        return Add(pattern, async (_, context) => await renderAsync(context));
    }

    /// <summary>Maps a synchronous page with access to the HTTP context.</summary>
    public HeimdallFunctionPageBuilder MapPage(string pattern, Func<HttpContext, IHtmlContent> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return Add(pattern, (_, context) => Task.FromResult(render(context)));
    }

    /// <summary>Maps a page that needs no request state.</summary>
    public HeimdallFunctionPageBuilder MapPage(string pattern, Func<IHtmlContent> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return Add(pattern, (_, _) => Task.FromResult(render()));
    }

    /// <summary>Maps an asynchronous page that needs no request state.</summary>
    public HeimdallFunctionPageBuilder MapPage<THtml>(string pattern, Func<Task<THtml>> renderAsync)
        where THtml : IHtmlContent
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        return Add(pattern, async (_, _) => await renderAsync());
    }

    /// <summary>Maps a value-task page that needs no request state.</summary>
    public HeimdallFunctionPageBuilder MapPage<THtml>(string pattern, Func<ValueTask<THtml>> renderAsync)
        where THtml : IHtmlContent
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        return Add(pattern, async (_, _) => await renderAsync());
    }

    /// <summary>Maps an asynchronous page protected by the default or a named policy.</summary>
    public HeimdallFunctionPageBuilder MapAuthorizedPage(string pattern,
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync, string? policyName = null)
        => Protect(Add(pattern, renderAsync), policyName);

    /// <summary>Maps a synchronous page protected by the default or a named policy.</summary>
    public HeimdallFunctionPageBuilder MapAuthorizedPage(string pattern,
        Func<HttpContext, IHtmlContent> render, string? policyName = null)
        => Protect(MapPage(pattern, render), policyName);

    /// <summary>Maps a synchronous protected page with request services.</summary>
    public HeimdallFunctionPageBuilder MapAuthorizedPage(string pattern,
        Func<IServiceProvider, HttpContext, IHtmlContent> render, string? policyName = null)
        => Protect(MapPage(pattern, render), policyName);

    /// <summary>Maps an asynchronous protected page with access to the HTTP context.</summary>
    public HeimdallFunctionPageBuilder MapAuthorizedPage(string pattern,
        Func<HttpContext, Task<IHtmlContent>> renderAsync, string? policyName = null)
        => Protect(MapPage(pattern, renderAsync), policyName);

    /// <summary>Maps a protected page that needs no request state.</summary>
    public HeimdallFunctionPageBuilder MapAuthorizedPage(string pattern,
        Func<IHtmlContent> render, string? policyName = null)
        => Protect(MapPage(pattern, render), policyName);

    private static HeimdallFunctionPageBuilder Protect(HeimdallFunctionPageBuilder page, string? policyName)
        => policyName is null ? page.RequireAuthorization() : page.RequireAuthorization(policyName);

    internal IReadOnlyList<HeimdallFunctionPageBuilder> Registrations => _pages;

    private HeimdallFunctionPageBuilder Add(string pattern,
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync)
    {
        ArgumentNullException.ThrowIfNull(renderAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        if (pattern[0] != '/' || pattern.Contains('\\') || pattern.Contains('#') ||
            pattern.Contains("//", StringComparison.Ordinal) ||
            pattern.Split('/').Any(segment => segment is "." or ".." || segment.Any(char.IsControl)))
            throw new ArgumentException("Map an absolute ASP.NET Core page route pattern.", nameof(pattern));

        RouteTemplate template;
        try { template = TemplateParser.Parse(pattern); }
        catch (Exception ex) when (ex is ArgumentException or RouteCreationException)
        {
            throw new ArgumentException($"Invalid page route pattern '{pattern}'.", nameof(pattern), ex);
        }
        var first = template.Segments.FirstOrDefault()?.Parts;
        if (first is { Count: 1 } && first[0].IsLiteral && ReservedRoots.Contains(first[0].Text!))
            throw new ArgumentException($"The page route '{pattern}' is reserved by the Functions host or Heimdall.", nameof(pattern));
        if (!_patterns.Add(pattern.TrimEnd('/')))
            throw new InvalidOperationException($"The page route '{pattern}' is already mapped.");
        var page = new HeimdallFunctionPageBuilder(template, renderAsync);
        _pages.Add(page);
        return page;
    }
}

/// <summary>Supports normal ASP.NET Core endpoint conventions on a Functions page.</summary>
public sealed class HeimdallFunctionPageBuilder : IEndpointConventionBuilder
{
    private readonly List<Action<EndpointBuilder>> _conventions = [];
    private readonly List<Action<EndpointBuilder>> _finalConventions = [];

    internal HeimdallFunctionPageBuilder(RouteTemplate template,
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> renderAsync)
    {
        Template = template;
        RenderAsync = renderAsync;
    }

    internal RouteTemplate Template { get; }
    internal Func<IServiceProvider, HttpContext, Task<IHtmlContent>> RenderAsync { get; }

    /// <summary>Adds an endpoint convention to this page.</summary>
    public void Add(Action<EndpointBuilder> convention)
    {
        ArgumentNullException.ThrowIfNull(convention);
        _conventions.Add(convention);
    }

    /// <summary>Adds an endpoint convention applied after ordinary conventions.</summary>
    public void Finally(Action<EndpointBuilder> finalConvention)
    {
        ArgumentNullException.ThrowIfNull(finalConvention);
        _finalConventions.Add(finalConvention);
    }

    internal RouteEndpoint BuildEndpoint()
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, Template.ToRoutePattern(), 0)
        {
            DisplayName = $"Heimdall Functions page {Template.TemplateText}"
        };
        foreach (var convention in _conventions) convention(builder);
        foreach (var convention in _finalConventions) convention(builder);
        return (RouteEndpoint)builder.Build();
    }
}

/// <summary>Dispatches the protocol, assets, and configured pages from one Functions HTTP trigger.</summary>
public sealed class HeimdallFunctionRouter
{
    private readonly HeimdallFunctions _heimdall;
    private readonly HeimdallFunctionAssets _assets;
    private readonly HeimdallFunctionStaticWebAssets _staticWebAssets;
    private readonly PageRoute[] _pages;

    /// <summary>Creates a router from the registered page patterns.</summary>
    public HeimdallFunctionRouter(HeimdallFunctions heimdall, HeimdallFunctionAssets assets,
        HeimdallFunctionStaticWebAssets staticWebAssets,
        HeimdallFunctionPages pages, IInlineConstraintResolver constraintResolver)
    {
        _heimdall = heimdall;
        _assets = assets;
        _staticWebAssets = staticWebAssets;
        _pages = pages.Registrations.Select(page => new PageRoute(page, constraintResolver))
            .OrderBy(page => page.Precedence).ToArray();
    }

    /// <summary>Handles a catch-all route value from the application-owned HTTP trigger.</summary>
    public async Task<IActionResult> HandleAsync(HttpRequest request, string? path)
    {
        ArgumentNullException.ThrowIfNull(request);
        var route = (path ?? string.Empty).Trim('/');
        if (route.StartsWith("__heimdall/v1/", StringComparison.OrdinalIgnoreCase))
            return await _heimdall.HandleAsync(request, route["__heimdall/v1/".Length..]);
        if (route.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            return _assets.Serve(request, route["assets/".Length..]);

        var staticAsset = await _staticWebAssets.TryServeAsync(request, route);
        if (staticAsset is not null) return staticAsset;

        var context = request.HttpContext;
        PageRoute? selected = null;
        RouteValueDictionary? values = null;
        foreach (var page in _pages)
        {
            if (selected is not null && page.Precedence > selected.Precedence) break;
            var candidate = new RouteValueDictionary();
            if (!page.Matcher.TryMatch(new PathString('/' + route), candidate) ||
                !page.MatchesConstraints(context, candidate)) continue;
            if (selected is not null && page.Precedence == selected.Precedence)
                throw new InvalidOperationException($"Multiple Heimdall Functions pages match '{request.Path}'.");
            selected = page;
            values = candidate;
        }
        if (selected is null) return new NotFoundResult();
        if (!HttpMethods.IsGet(request.Method))
        {
            context.Response.Headers.Allow = "GET";
            return new StatusCodeResult(StatusCodes.Status405MethodNotAllowed);
        }

        var previousEndpoint = context.GetEndpoint();
        var previousValues = request.RouteValues;
        context.SetEndpoint(selected.Endpoint);
        request.RouteValues = values!;
        try
        {
            var authorize = selected.Endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            if (authorize.Count > 0 && selected.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            {
                var denied = await _heimdall.AuthorizeAsync(request, authorize);
                if (denied is not null) return denied;
            }
            return await _heimdall.RenderPageAsync(request, selected.Registration.RenderAsync);
        }
        finally
        {
            context.SetEndpoint(previousEndpoint);
            request.RouteValues = previousValues;
        }
    }

    private sealed class PageRoute
    {
        private readonly (string Name, IRouteConstraint Constraint)[] _constraints;

        public PageRoute(HeimdallFunctionPageBuilder registration, IInlineConstraintResolver resolver)
        {
            Registration = registration;
            Endpoint = registration.BuildEndpoint();
            var defaults = new RouteValueDictionary();
            foreach (var parameter in registration.Template.Parameters)
                if (parameter.DefaultValue is not null) defaults[parameter.Name!] = parameter.DefaultValue;
            Matcher = new TemplateMatcher(registration.Template, defaults);
            Precedence = RoutePrecedence.ComputeInbound(registration.Template);
            _constraints = registration.Template.Parameters
                .SelectMany(parameter => parameter.InlineConstraints.Select(inline =>
                {
                    var constraint = resolver.ResolveConstraint(inline.Constraint)
                        ?? throw new InvalidOperationException($"Unknown route constraint '{inline.Constraint}' in '{registration.Template.TemplateText}'.");
                    return (parameter.Name!, constraint);
                })).ToArray();
        }

        public HeimdallFunctionPageBuilder Registration { get; }
        public RouteEndpoint Endpoint { get; }
        public TemplateMatcher Matcher { get; }
        public decimal Precedence { get; }

        public bool MatchesConstraints(HttpContext context, RouteValueDictionary values)
            => _constraints.All(item => item.Constraint.Match(context, null!, item.Name, values, RouteDirection.IncomingRequest));
    }
}
