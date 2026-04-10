using Heimdall.Server.Helpers;
using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
            app.MapPost("__heimdall/v1/content/actions", async (
                HttpContext ctx,
                ContentRegistry registry,
                IOptions<HeimdallServiceSettings> options) =>
            {
                var settings = options.Value;

                var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
                await antiforgery.ValidateRequestAsync(ctx);

                if (!ctx.Request.Headers.TryGetValue(ActionHeader, out var values) ||
                    string.IsNullOrWhiteSpace(values))
                {
                    return Results.BadRequest($"Missing {ActionHeader} header.");
                }

                var actionId = values.ToString();

                if (!registry.TryGet(actionId, out var action))
                    return Results.NotFound($"Unknown action '{actionId}'.");

                try
                {
                    var args = await BindArgumentsAsync(ctx, action);
                    var raw = await action.InvokeAsync(args);

                    if (raw is null)
                        return Results.NoContent();

                    return Results.Content(raw.RenderHtml(), "text/html; charset=utf-8");
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
            }).ExcludeFromDescription();

            return app;
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

                    if (httpContext.Request.ContentLength is null or 0)
                        throw new InvalidOperationException("Request body is empty.");

                    bodyJson = await JsonSerializer.DeserializeAsync<JsonElement>(
                        httpContext.Request.Body,
                        JsonOptions);
                }

                return BindPayloadValue(bodyJson!.Value, parameter.Parameter);
            }
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