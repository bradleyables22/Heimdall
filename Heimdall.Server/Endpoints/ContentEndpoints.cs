using Heimdall.Server.Helpers;
using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Server
{
    internal static class ContentEndpoints
    {
        private const string ActionHeader = "X-Heimdall-Content-Action";
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
        private static readonly RequestDelegate EmptyAuthorizationPipeline = _ => Task.CompletedTask;

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                // Accept "123" into int/decimal/etc
                NumberHandling = JsonNumberHandling.AllowReadingFromString,

                // Debug/QoL
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            // Accept enum values as strings too
            o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            // Accept "true"/"false"/"1"/"0"/"on"/"off" as bool (and nullable bool)
            o.Converters.Add(new BoolFromStringConverter());
            o.Converters.Add(new NullableBoolFromStringConverter());

            return o;
        }

        internal static WebApplication MapHeimdallContentEndpoints(this WebApplication app)
        {
            var handler = BuildContentActionHandler();
            app.MapPost("__heimdall/v1/content/actions", handler).ExcludeFromDescription();

            return app;
        }

        internal static IApplicationBuilder MapHeimdallContentEndpoints(this IApplicationBuilder app)
        {
            var handler = BuildContentActionHandler();
            app.UseEndpoints(endpoints =>
                endpoints.MapPost("__heimdall/v1/content/actions", handler));

            return app;
        }

        private static RequestDelegate BuildContentActionHandler() =>
            async ctx =>
            {
                // Validate antiforgery and extract action id
                var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
                await antiforgery.ValidateRequestAsync(ctx);

                var (ok, actionId, extractError) = TryExtractActionId(ctx);
                if (!ok)
                {
                    await extractError!.ExecuteAsync(ctx);
                    return;
                }

                // Resolve action and perform authorization
                var (action, resolveError) = await TryResolveAndAuthorizeAsync(ctx, actionId!);
                if (resolveError is not null)
                {
                    await resolveError.ExecuteAsync(ctx);
                    return;
                }

                // Invoke action and produce a result to execute
                var result = await InvokeActionAsync(ctx, action!);
                if (result is not null)
                {
                    await result.ExecuteAsync(ctx);
                }
            };

        private static (bool success, string? actionId, IResult? error) TryExtractActionId(HttpContext ctx)
        {
            if (!ctx.Request.Headers.TryGetValue(ActionHeader, out var values) || string.IsNullOrWhiteSpace(values))
            {
                return (false, null, Results.BadRequest($"Missing {ActionHeader} header."));
            }

            return (true, values.ToString(), null);
        }

        private static async Task<(ContentActionDescriptor? action, IResult? error)> TryResolveAndAuthorizeAsync(
            HttpContext ctx, string actionId)
        {
            var registry = ctx.RequestServices.GetRequiredService<ContentRegistry>();
            if (!registry.TryGet(actionId, out var action))
                return (null, Results.NotFound($"Unknown action '{actionId}'."));

            var authorizationResult = await AuthorizeActionAsync(ctx, action);
            if (authorizationResult is not null)
                return (null, authorizationResult);

            return (action, null);
        }

        private static async Task<IResult?> InvokeActionAsync(HttpContext ctx, ContentActionDescriptor action)
        {
            var options = ctx.RequestServices.GetRequiredService<IOptions<HeimdallServiceSettings>>();
            var settings = options.Value;

            var timeoutScope = CreateRequestTimeoutScope(ctx, action);
            try
            {
                var args = await BindArgumentsAsync(ctx, action);
                var raw = await action.InvokeAsync(args);

                if (raw is null)
                    return Results.NoContent();

                return Results.Content(raw.RenderHtml(), "text/html; charset=utf-8");
            }
            catch (OperationCanceledException) when (timeoutScope.TimedOut)
            {
                return await CreateRequestTimeoutResultAsync(ctx, timeoutScope.Policy!);
            }
            catch (JsonException ex)
            {
                if (settings.EnableDetailedErrors)
                {
                    return Results.Problem(
                        detail: ex.ToString(),
                        title: "Invalid Heimdall action request body",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.BadRequest("Invalid JSON request body.");
            }
            catch (Exception ex)
            {
                if (settings.EnableDetailedErrors)
                {
                    var msg = ex is TargetInvocationException tie && tie.InnerException != null
                        ? tie.InnerException.ToString()
                        : ex.ToString();

                    return Results.Problem(
                        detail: msg,
                        title: "Heimdall action invocation failed",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                return Results.Problem(
                    title: "Heimdall action invocation failed",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            finally
            {
                timeoutScope.Dispose();
            }
        }

        private static async Task<object?[]> BindArgumentsAsync(HttpContext ctx, ContentActionDescriptor action)
        {
            if (action.Parameters.Count == 0)
                return Array.Empty<object?>();

            var args = new object?[action.Parameters.Count];
            JsonElement? bodyJson = null;
            bool bodyRead = false;

            foreach (var parameter in action.Parameters)
            {
                args[parameter.Index] = parameter.Kind switch
                {
                    ContentActionParameterKind.HttpContext => ctx,
                    ContentActionParameterKind.CancellationToken => ctx.RequestAborted,
                    ContentActionParameterKind.ClaimsPrincipal => ctx.User,
                    ContentActionParameterKind.Service => ResolveRequiredService(ctx, action, parameter),
                    ContentActionParameterKind.Payload => await BindPayloadParameterAsync(ctx, parameter),
                    _ => throw new InvalidOperationException(
                        $"Unsupported parameter kind '{parameter.Kind}' in action '{action.ActionId}'.")
                };
            }

            return args;

            async Task<object?> BindPayloadParameterAsync(
                HttpContext httpContext,
                ContentActionParameterDescriptor parameter)
            {
                if (!bodyRead)
                {
                    bodyRead = true;

                    if (httpContext.Request.ContentLength == 0)
                        throw new InvalidOperationException("Request body is empty.");

                    bodyJson = await JsonSerializer.DeserializeAsync<JsonElement>(
                        httpContext.Request.Body,
                        JsonOptions,
                        httpContext.RequestAborted);
                }

                return BindPayloadValue(bodyJson!.Value, parameter.Parameter);
            }
        }

        private static async Task<IResult?> AuthorizeActionAsync(
            HttpContext ctx,
            ContentActionDescriptor action)
        {
            if (!action.RequiresAuthorization)
                return null;

            var policyProvider = GetRequiredAuthorizationService<IAuthorizationPolicyProvider>(ctx);
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, action.AuthorizeData);

            if (policy is null)
                return null;

            var policyEvaluator = GetRequiredAuthorizationService<IPolicyEvaluator>(ctx);
            var authenticateResult = await policyEvaluator.AuthenticateAsync(policy, ctx);
            var authorizeResult = await policyEvaluator.AuthorizeAsync(policy, authenticateResult, ctx, ctx);

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
                       $"Heimdall content action authorization requires '{typeof(T).FullName}'. " +
                       "Register authorization services with services.AddAuthorization(...).");
        }

        private static ContentActionTimeoutScope CreateRequestTimeoutScope(
            HttpContext ctx,
            ContentActionDescriptor action)
        {
            if (action.DisableRequestTimeout)
            {
                ctx.Features.Get<IHttpRequestTimeoutFeature>()?.DisableTimeout();
                return ContentActionTimeoutScope.None(ctx);
            }

            var policy = ResolveRequestTimeoutPolicy(ctx, action);
            if (policy is null)
                return ContentActionTimeoutScope.None(ctx);

            ctx.Features.Get<IHttpRequestTimeoutFeature>()?.DisableTimeout();
            return ContentActionTimeoutScope.Start(ctx, policy);
        }

        private static RequestTimeoutPolicy? ResolveRequestTimeoutPolicy(
            HttpContext ctx,
            ContentActionDescriptor action)
        {
            var attr = action.RequestTimeout;
            if (attr is null)
                return null;

            if (attr.Timeout.HasValue)
            {
                return new RequestTimeoutPolicy
                {
                    Timeout = attr.Timeout.Value
                };
            }

            if (string.IsNullOrWhiteSpace(attr.PolicyName))
            {
                throw new InvalidOperationException(
                    $"Request timeout metadata for Heimdall action '{action.ActionId}' does not specify a timeout or policy name.");
            }

            var options = ctx.RequestServices.GetService<IOptions<RequestTimeoutOptions>>()?.Value;
            if (options is null || !options.Policies.TryGetValue(attr.PolicyName, out var policy))
            {
                throw new InvalidOperationException(
                    $"Request timeout policy '{attr.PolicyName}' was not found for Heimdall action '{action.ActionId}'. " +
                    "Register it with services.AddRequestTimeouts(...).");
            }

            return policy;
        }

        private static async Task<IResult> CreateRequestTimeoutResultAsync(
            HttpContext ctx,
            RequestTimeoutPolicy policy)
        {
            if (ctx.Response.HasStarted)
                return Results.Empty;

            var statusCode = policy.TimeoutStatusCode ?? StatusCodes.Status504GatewayTimeout;
            ctx.Response.StatusCode = statusCode;

            if (policy.WriteTimeoutResponse is not null)
            {
                await policy.WriteTimeoutResponse(ctx);
                return Results.Empty;
            }

            return Results.StatusCode(statusCode);
        }

        private static object? BindPayloadValue(JsonElement bodyJson, ParameterInfo parameter)
        {
            var targetType = parameter.ParameterType;

            if (bodyJson.ValueKind == JsonValueKind.Null)
                return GetMissingValue(parameter, targetType);

            if (bodyJson.ValueKind != JsonValueKind.Object)
                return DeserializeRequired(bodyJson, targetType, parameter);

            if (IsSimplePayloadType(targetType))
            {
                if (TryGetPropertyCaseInsensitive(bodyJson, parameter.Name!, out var propertyJson))
                    return DeserializeRequired(propertyJson, targetType, parameter);

                return GetMissingValue(parameter, targetType);
            }

            if (TryGetPropertyCaseInsensitive(bodyJson, parameter.Name!, out var nestedPropertyJson) &&
                nestedPropertyJson.ValueKind == JsonValueKind.Object)
            {
                return DeserializeRequired(nestedPropertyJson, targetType, parameter);
            }

            if (bodyJson.EnumerateObject().MoveNext())
                return DeserializeRequired(bodyJson, targetType, parameter);

            return GetMissingValue(parameter, targetType);
        }

        private static object? DeserializeRequired(
            JsonElement json,
            Type targetType,
            ParameterInfo parameter)
        {
            var value = json.Deserialize(targetType, JsonOptions);

            if (value is not null)
                return value;

            if (AllowsNull(targetType))
                return null;

            if (parameter.HasDefaultValue)
                return parameter.DefaultValue;

            throw new InvalidOperationException(
                $"Failed to bind payload parameter '{parameter.Name}'.");
        }

        private static object? GetMissingValue(ParameterInfo parameter, Type targetType)
        {
            if (parameter.HasDefaultValue)
                return parameter.DefaultValue;

            if (AllowsNull(targetType))
                return null;

            throw new InvalidOperationException(
                $"Missing required payload value for parameter '{parameter.Name}'.");
        }

        private static bool AllowsNull(Type type)
        {
            if (!type.IsValueType)
                return true;

            return Nullable.GetUnderlyingType(type) is not null;
        }

        private static bool IsSimplePayloadType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying.IsEnum)
                return true;

            if (underlying.IsPrimitive)
                return true;

            return underlying == typeof(string)
                   || underlying == typeof(decimal)
                   || underlying == typeof(Guid)
                   || underlying == typeof(DateTime)
                   || underlying == typeof(DateTimeOffset)
                   || underlying == typeof(TimeSpan);
        }

        private static bool TryGetPropertyCaseInsensitive(
            JsonElement jsonObject,
            string propertyName,
            out JsonElement value)
        {
            if (jsonObject.TryGetProperty(propertyName, out value))
                return true;

            foreach (var property in jsonObject.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private sealed class ContentActionTimeoutScope : IDisposable
        {
            private readonly HttpContext _ctx;
            private readonly CancellationToken _originalRequestAborted;
            private readonly CancellationTokenSource? _timeoutCts;
            private readonly CancellationTokenSource? _linkedCts;
            private bool _disposed;

            private ContentActionTimeoutScope(
                HttpContext ctx,
                CancellationToken originalRequestAborted,
                RequestTimeoutPolicy? policy,
                CancellationTokenSource? timeoutCts,
                CancellationTokenSource? linkedCts)
            {
                _ctx = ctx;
                _originalRequestAborted = originalRequestAborted;
                Policy = policy;
                _timeoutCts = timeoutCts;
                _linkedCts = linkedCts;
            }

            public RequestTimeoutPolicy? Policy { get; }

            public bool TimedOut =>
                _timeoutCts?.IsCancellationRequested == true &&
                !_originalRequestAborted.IsCancellationRequested;

            public static ContentActionTimeoutScope None(HttpContext ctx)
                => new(ctx, ctx.RequestAborted, policy: null, timeoutCts: null, linkedCts: null);

            public static ContentActionTimeoutScope Start(HttpContext ctx, RequestTimeoutPolicy policy)
            {
                var originalRequestAborted = ctx.RequestAborted;
                var timeout = policy.Timeout;

                if (!timeout.HasValue || timeout.Value == System.Threading.Timeout.InfiniteTimeSpan)
                    return new ContentActionTimeoutScope(ctx, originalRequestAborted, policy, timeoutCts: null,
                        linkedCts: null);

                var timeoutCts = new CancellationTokenSource();
                timeoutCts.CancelAfter(timeout.Value);

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    originalRequestAborted,
                    timeoutCts.Token);

                ctx.RequestAborted = linkedCts.Token;

                return new ContentActionTimeoutScope(
                    ctx,
                    originalRequestAborted,
                    policy,
                    timeoutCts,
                    linkedCts);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _ctx.RequestAborted = _originalRequestAborted;
                _linkedCts?.Dispose();
                _timeoutCts?.Dispose();
                _disposed = true;
            }
        }

        private static object ResolveRequiredService(
            HttpContext ctx,
            ContentActionDescriptor action,
            ContentActionParameterDescriptor parameter)
        {
            var service = ctx.RequestServices.GetService(parameter.ParameterType);
            if (service is not null)
                return service;

            throw new InvalidOperationException(
                $"Failed to resolve DI service '{parameter.ParameterType.FullName}' " +
                $"for Heimdall action '{action.Method.DeclaringType?.FullName}.{action.Method.Name}' " +
                $"parameter '{parameter.Parameter.Name}'.");
        }
    }
}