using Heimdall.Server.Helpers;
using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
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

        internal static IEndpointRouteBuilder MapHeimdallContentEndpoints(this IEndpointRouteBuilder app)
        {
            var logger = app.ServiceProvider
                .GetService<ILoggerFactory>()
                ?.CreateLogger("Heimdall.Server.ContentEndpoints");

            app.MapPost("__heimdall/v1/content/actions", async (
                HttpContext ctx,
                [FromServices] ContentRegistry registry,
                [FromServices] IOptions<HeimdallServiceSettings> options) =>
            {
                using var telemetry = HeimdallTelemetry.StartContentAction(ctx);
                var settings = options.Value;

                var hasActionHeader = ctx.Request.Headers.TryGetValue(ActionHeader, out var actionValues) &&
                    !string.IsNullOrWhiteSpace(actionValues);
                var actionId = hasActionHeader ? actionValues.ToString() : string.Empty;
                ContentActionDescriptor? action = null;

                if (hasActionHeader && registry.TryGet(actionId, out var resolvedAction))
                {
                    action = resolvedAction;
                    telemetry.SetActionId(action.ActionId);

                    var requestLimitResult = ApplyRequestLimits(ctx, action, settings);
                    if (requestLimitResult is not null)
                    {
                        telemetry.Complete(StatusCodes.Status413PayloadTooLarge);
                        return requestLimitResult;
                    }
                }

                var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
                try
                {
                    await antiforgery.ValidateRequestAsync(ctx);
                }
                catch (Exception ex) when (IsRequestTooLargeException(ex))
                {
                    telemetry.RecordException(ex, StatusCodes.Status413PayloadTooLarge);
                    logger?.LogWarning(
                        ex,
                        "Heimdall content action request body exceeded an ASP.NET Core request or form limit for {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
                        ctx.Request.Method,
                        ctx.Request.Path,
                        ctx.TraceIdentifier);

                    return CreateRequestTooLargeResult(settings, ex);
                }
                catch (AntiforgeryValidationException ex)
                {
                    telemetry.RecordException(ex, StatusCodes.Status400BadRequest);
                    logger?.LogWarning(
                        ex,
                        "Heimdall content action request failed antiforgery validation for {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
                        ctx.Request.Method,
                        ctx.Request.Path,
                        ctx.TraceIdentifier);

                    if (settings.EnableDetailedErrors)
                    {
                        return Results.Problem(
                            detail: ex.ToString(),
                            title: "Invalid Heimdall antiforgery token",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    return Results.BadRequest("Invalid Heimdall antiforgery token.");
                }

                if (!hasActionHeader)
                {
                    telemetry.SetActionId("missing");
                    telemetry.Complete(StatusCodes.Status400BadRequest);
                    return Results.BadRequest($"Missing {ActionHeader} header.");
                }

                if (action is null)
                {
                    telemetry.SetActionId("unknown");
                    telemetry.Complete(StatusCodes.Status404NotFound);
                    return Results.NotFound($"Unknown action '{actionId}'.");
                }

                telemetry.SetActionId(action.ActionId);

                IResult? authorizationResult;
                try
                {
                    authorizationResult = await AuthorizeActionAsync(ctx, action);
                }
                catch (Exception ex)
                {
                    telemetry.RecordException(ex, StatusCodes.Status500InternalServerError);
                    throw;
                }

                if (authorizationResult is not null)
                {
                    var authorizationStatusCode = ctx.Response.StatusCode;
                    telemetry.Complete(
                        authorizationStatusCode,
                        outcome: authorizationStatusCode is >= 300 and < 400 ? "redirect" : null);
                    return authorizationResult;
                }

                ContentActionTimeoutScope timeoutScope;
                try
                {
                    timeoutScope = CreateRequestTimeoutScope(ctx, action);
                }
                catch (Exception ex)
                {
                    telemetry.RecordException(ex, StatusCodes.Status500InternalServerError);
                    throw;
                }

                try
                {
                    var args = await BindArgumentsAsync(ctx, action, telemetry);
                    var raw = await action.InvokeAsync(ctx.RequestServices, args);

                    if (raw is null)
                    {
                        telemetry.Complete(StatusCodes.Status204NoContent, responseBodySize: 0);
                        return Results.NoContent();
                    }

                    var html = raw.RenderHtml();
                    long? responseBodySize = telemetry.ShouldMeasureResponseBodySize
                        ? Encoding.UTF8.GetByteCount(html)
                        : null;
                    telemetry.Complete(
                        StatusCodes.Status200OK,
                        responseBodySize);
                    return Results.Content(html, "text/html; charset=utf-8");
                }
                catch (OperationCanceledException) when (timeoutScope.TimedOut)
                {
                    var statusCode = timeoutScope.Policy?.TimeoutStatusCode ?? StatusCodes.Status504GatewayTimeout;
                    logger?.LogWarning(
                        "Heimdall action {ActionId} timed out for {Method} {Path}; returning status {StatusCode}. TraceIdentifier: {TraceIdentifier}.",
                        actionId,
                        ctx.Request.Method,
                        ctx.Request.Path,
                        statusCode,
                        ctx.TraceIdentifier);

                    var result = await CreateRequestTimeoutResultAsync(ctx, timeoutScope.Policy!);
                    telemetry.RecordCancellation("timeout", statusCode);
                    return result;
                }
                catch (ContentActionBindingException ex)
                {
                    telemetry.RecordException(ex, ex.StatusCode);
                    logger?.LogWarning(
                        ex,
                        "Invalid request body for Heimdall action {ActionId} on {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
                        actionId,
                        ctx.Request.Method,
                        ctx.Request.Path,
                        ctx.TraceIdentifier);

                    var title = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                        ? "Heimdall action request body is too large"
                        : "Invalid Heimdall action request body";

                    if (settings.EnableDetailedErrors)
                    {
                        return Results.Problem(
                            detail: ex.ToString(),
                            title: title,
                            statusCode: ex.StatusCode);
                    }

                    return Results.Problem(
                        title: title,
                        detail: ex.Message,
                        statusCode: ex.StatusCode);
                }
                catch (JsonException ex)
                {
                    telemetry.RecordException(ex, StatusCodes.Status400BadRequest);
                    logger?.LogWarning(
                        ex,
                        "Invalid JSON body for Heimdall action {ActionId} on {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
                        actionId,
                        ctx.Request.Method,
                        ctx.Request.Path,
                        ctx.TraceIdentifier);

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
                    var loggedException = UnwrapInvocationException(ex);
                    if (loggedException is OperationCanceledException)
                    {
                        var cancellationReason = ctx.RequestAborted.IsCancellationRequested
                            ? "request_aborted"
                            : "operation_cancelled";
                        telemetry.RecordCancellation(
                            cancellationReason,
                            StatusCodes.Status500InternalServerError);
                    }
                    else
                    {
                        telemetry.RecordException(
                            loggedException,
                            StatusCodes.Status500InternalServerError);
                    }

                    logger?.LogError(
                        loggedException,
                        "Heimdall action {ActionId} invocation failed for {Method} {Path}. TraceIdentifier: {TraceIdentifier}.",
                        actionId,
                        ctx.Request.Method,
                        ctx.Request.Path,
                        ctx.TraceIdentifier);

                    if (settings.EnableDetailedErrors)
                    {
                        return Results.Problem(
                            detail: loggedException.ToString(),
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
            }).ExcludeFromDescription();

            return app;
        }

        private static Exception UnwrapInvocationException(Exception ex)
            => ex is TargetInvocationException { InnerException: Exception inner }
                ? inner
                : ex;

        private static IResult? ApplyRequestLimits(
            HttpContext context,
            ContentActionDescriptor action,
            HeimdallServiceSettings settings)
        {
            var requestSizeLimit = action.RequestSizeLimit;
            if (requestSizeLimit is not null)
            {
                var maxRequestBodySize = requestSizeLimit.MaxRequestBodySize;
                if (maxRequestBodySize is long maximum &&
                    context.Request.ContentLength is long contentLength &&
                    contentLength > maximum)
                {
                    return CreateRequestTooLargeResult(settings);
                }

                var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false })
                    feature.MaxRequestBodySize = maxRequestBodySize;
            }

            if (action.FormOptions is not null && context.Request.HasFormContentType)
            {
                var formOptions = ResolveFormOptions(context, action.FormOptions);
                context.Features.Set<IFormFeature>(new FormFeature(context.Request, formOptions));
            }

            return null;
        }

        private static FormOptions ResolveFormOptions(
            HttpContext context,
            IFormOptionsMetadata metadata)
        {
            var defaults = context.RequestServices.GetService<IOptions<FormOptions>>()?.Value
                ?? new FormOptions();

            return new FormOptions
            {
                BufferBody = metadata.BufferBody ?? defaults.BufferBody,
                MemoryBufferThreshold = metadata.MemoryBufferThreshold ?? defaults.MemoryBufferThreshold,
                BufferBodyLengthLimit = metadata.BufferBodyLengthLimit ?? defaults.BufferBodyLengthLimit,
                ValueCountLimit = metadata.ValueCountLimit ?? defaults.ValueCountLimit,
                KeyLengthLimit = metadata.KeyLengthLimit ?? defaults.KeyLengthLimit,
                ValueLengthLimit = metadata.ValueLengthLimit ?? defaults.ValueLengthLimit,
                MultipartBoundaryLengthLimit = metadata.MultipartBoundaryLengthLimit ?? defaults.MultipartBoundaryLengthLimit,
                MultipartHeadersCountLimit = metadata.MultipartHeadersCountLimit ?? defaults.MultipartHeadersCountLimit,
                MultipartHeadersLengthLimit = metadata.MultipartHeadersLengthLimit ?? defaults.MultipartHeadersLengthLimit,
                MultipartBodyLengthLimit = metadata.MultipartBodyLengthLimit ?? defaults.MultipartBodyLengthLimit
            };
        }

        private static bool IsRequestTooLargeException(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
                    return true;

                if (current is InvalidDataException &&
                    current.Message.Contains("limit", StringComparison.OrdinalIgnoreCase) &&
                    current.Message.Contains("exceed", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IResult CreateRequestTooLargeResult(
            HeimdallServiceSettings settings,
            Exception? exception = null)
            => Results.Problem(
                detail: settings.EnableDetailedErrors && exception is not null
                    ? exception.ToString()
                    : "The request body exceeded the configured ASP.NET Core request or form limits.",
                title: "Heimdall action request body is too large",
                statusCode: StatusCodes.Status413PayloadTooLarge);

        private static async Task<object?[]> BindArgumentsAsync(
            HttpContext ctx,
            ContentActionDescriptor action,
            HeimdallTelemetry.ContentActionTelemetryScope telemetry)
        {
            if (action.Parameters.Count == 0)
                return Array.Empty<object?>();

            var args = new object?[action.Parameters.Count];
            JsonElement? bodyJson = null;
            IFormCollection? requestForm = null;
            bool bodyRead = false;
            bool formRead = false;

            foreach (var parameter in action.Parameters)
            {
                args[parameter.Index] = parameter.Kind switch
                {
                    ContentActionParameterKind.HttpContext => ctx,
                    ContentActionParameterKind.CancellationToken => ctx.RequestAborted,
                    ContentActionParameterKind.ClaimsPrincipal => ctx.User,
                    ContentActionParameterKind.Service => ResolveRequiredService(ctx, action, parameter),
                    ContentActionParameterKind.Payload => await BindPayloadParameterAsync(ctx, parameter),
                    ContentActionParameterKind.FormPayload => await BindFormPayloadParameterAsync(ctx, parameter),
                    ContentActionParameterKind.FormFile => await BindFormFileParameterAsync(ctx, parameter),
                    _ => throw new InvalidOperationException(
                        $"Unsupported parameter kind '{parameter.Kind}' in action '{action.ActionId}'.")
                };
            }

            return args;

            async Task<object?> BindPayloadParameterAsync(
                HttpContext httpContext,
                ContentActionParameterDescriptor parameter)
            {
                if (httpContext.Request.HasFormContentType)
                {
                    var form = await ReadFormAsync(httpContext);
                    return BindFormPayloadValue(form, parameter.Parameter, parameter.BindingName);
                }

                if (!bodyRead)
                {
                    bodyRead = true;

                    if (httpContext.Request.ContentLength == 0)
                        throw new InvalidOperationException("Request body is empty.");

                    bodyJson = await JsonSerializer.DeserializeAsync<JsonElement>(
                        httpContext.Request.Body,
                        JsonOptions,
                        httpContext.RequestAborted);
                    if (telemetry.ShouldMeasureRequestBodySize)
                    {
                        telemetry.SetRequestBodySize(
                            Encoding.UTF8.GetByteCount(bodyJson.Value.GetRawText()));
                    }
                }

                return BindPayloadValue(bodyJson!.Value, parameter.Parameter);
            }

            async Task<object?> BindFormPayloadParameterAsync(
                HttpContext httpContext,
                ContentActionParameterDescriptor parameter)
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    throw new ContentActionBindingException(
                        StatusCodes.Status415UnsupportedMediaType,
                        $"Form parameter '{parameter.Parameter.Name}' requires a form content type.");
                }

                var form = await ReadFormAsync(httpContext);
                return BindFormPayloadValue(form, parameter.Parameter, parameter.BindingName);
            }

            async Task<object?> BindFormFileParameterAsync(
                HttpContext httpContext,
                ContentActionParameterDescriptor parameter)
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    throw new ContentActionBindingException(
                        StatusCodes.Status415UnsupportedMediaType,
                        $"File parameter '{parameter.Parameter.Name}' requires a multipart/form-data request.");
                }

                var form = await ReadFormAsync(httpContext);
                return BindFormFileValue(form.Files, parameter.Parameter, parameter.BindingName);
            }

            async Task<IFormCollection> ReadFormAsync(HttpContext httpContext)
            {
                if (!formRead)
                {
                    formRead = true;
                    try
                    {
                        requestForm = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
                    }
                    catch (InvalidDataException ex)
                    {
                        var statusCode = IsRequestTooLargeException(ex)
                            ? StatusCodes.Status413PayloadTooLarge
                            : StatusCodes.Status400BadRequest;
                        throw new ContentActionBindingException(
                            statusCode,
                            statusCode == StatusCodes.Status413PayloadTooLarge
                                ? "The multipart form body exceeded the configured ASP.NET Core form limits."
                                : "The multipart form body is invalid.",
                            ex);
                    }
                }

                return requestForm!;
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

        private static object? BindFormPayloadValue(
            IFormCollection form,
            ParameterInfo parameter,
            string bindingName)
        {
            if (TryBindEmbeddedJsonFormValue(form, parameter, bindingName, out var embeddedValue))
                return embeddedValue;

            var prefix = $"{bindingName}.";
            var bracketPrefix = $"{bindingName}[";
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in form)
            {
                var key = field.Key;
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    key = key[prefix.Length..];
                }
                else if (key.StartsWith(bracketPrefix, StringComparison.OrdinalIgnoreCase) &&
                    key.EndsWith(']'))
                {
                    key = key[bracketPrefix.Length..^1];
                }

                values[key] = field.Value.Count switch
                {
                    0 => string.Empty,
                    1 => field.Value[0],
                    _ => field.Value.ToArray()
                };
            }

            var formJson = JsonSerializer.SerializeToElement(values, JsonOptions);
            return BindPayloadValue(formJson, parameter);
        }

        private static bool TryBindEmbeddedJsonFormValue(
            IFormCollection form,
            ParameterInfo parameter,
            string bindingName,
            out object? value)
        {
            value = null;
            if (!form.TryGetValue(bindingName, out var fieldValues) || fieldValues.Count != 1)
                return false;

            var raw = fieldValues[0];
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            try
            {
                using var document = JsonDocument.Parse(raw);
                value = DeserializeRequired(document.RootElement, parameter.ParameterType, parameter);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static object? BindFormFileValue(
            IFormFileCollection files,
            ParameterInfo parameter,
            string bindingName)
        {
            var matches = files
                .Where(file =>
                    string.Equals(file.Name, bindingName, StringComparison.OrdinalIgnoreCase) &&
                    (file.Length > 0 || !string.IsNullOrEmpty(file.FileName)))
                .ToArray();
            var targetType = parameter.ParameterType;

            if (targetType == typeof(IFormFile))
            {
                if (matches.Length > 0)
                    return matches[0];

                if (parameter.HasDefaultValue)
                    return parameter.DefaultValue;

                if (AllowsNull(parameter))
                    return null;

                throw new ContentActionBindingException(
                    StatusCodes.Status400BadRequest,
                    $"Missing required uploaded file for parameter '{parameter.Name}'.");
            }

            if (targetType == typeof(IFormFileCollection))
            {
                var collection = new FormFileCollection();
                foreach (var file in matches)
                    collection.Add(file);
                return collection;
            }

            if (targetType.IsArray)
                return matches;

            return matches.ToList();
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

        private static bool AllowsNull(ParameterInfo parameter)
        {
            if (parameter.ParameterType.IsValueType)
                return AllowsNull(parameter.ParameterType);

            return new NullabilityInfoContext()
                .Create(parameter)
                .ReadState != NullabilityState.NotNull;
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
                    return new ContentActionTimeoutScope(ctx, originalRequestAborted, policy, timeoutCts: null, linkedCts: null);

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

        private sealed class ContentActionBindingException : Exception
        {
            public ContentActionBindingException(int statusCode, string message, Exception? innerException = null)
                : base(message, innerException)
            {
                StatusCode = statusCode;
            }

            public int StatusCode { get; }
        }
    }
}
