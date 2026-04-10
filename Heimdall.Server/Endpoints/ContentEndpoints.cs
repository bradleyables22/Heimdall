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
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
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
            object? payloadValue = null;

            if (action.HasPayload)
            {
                if (ctx.Request.ContentLength is null or 0)
                    throw new InvalidOperationException("Request body is empty.");

                var bodyJson = await JsonSerializer.DeserializeAsync<JsonElement>(
                    ctx.Request.Body,
                    JsonOptions);

                payloadValue = BindPayloadValue(bodyJson, action);
            }

            foreach (var parameter in action.Parameters)
            {
                args[parameter.Index] = parameter.Kind switch
                {
                    ContentActionParameterKind.HttpContext => ctx,
                    ContentActionParameterKind.CancellationToken => ctx.RequestAborted,
                    ContentActionParameterKind.ClaimsPrincipal => ctx.User,
                    ContentActionParameterKind.Service => ResolveRequiredService(ctx, action, parameter),
                    ContentActionParameterKind.Payload => payloadValue,
                    _ => throw new InvalidOperationException(
                        $"Unsupported parameter kind '{parameter.Kind}' in action '{action.ActionId}'.")
                };
            }

            return args;
        }

        private static object? BindPayloadValue(JsonElement bodyJson, ContentActionDescriptor action)
        {
            var payloadParameter = action.PayloadParameter!.Parameter;
            var payloadType = action.PayloadType!;

            if (bodyJson.ValueKind == JsonValueKind.Object)
            {
                if (bodyJson.TryGetProperty(payloadParameter.Name!, out var propertyJson))
                {
                    return DeserializeJsonValue(propertyJson, payloadType, payloadParameter);
                }

                if (payloadParameter.HasDefaultValue)
                    return payloadParameter.DefaultValue;

                return GetDefaultValue(payloadType);
            }

            if (bodyJson.ValueKind == JsonValueKind.Null)
            {
                if (payloadParameter.HasDefaultValue)
                    return payloadParameter.DefaultValue;

                return GetDefaultValue(payloadType);
            }

            return DeserializeJsonValue(bodyJson, payloadType, payloadParameter);
        }

        private static object? DeserializeJsonValue(
            JsonElement json,
            Type targetType,
            ParameterInfo parameter)
        {
            var value = json.Deserialize(targetType, JsonOptions);

            if (value is not null)
                return value;

            if (parameter.HasDefaultValue)
                return parameter.DefaultValue;

            return GetDefaultValue(targetType);
        }

        private static object? GetDefaultValue(Type type)
        {
            if (!type.IsValueType)
                return null;

            return Nullable.GetUnderlyingType(type) is not null
                ? null
                : Activator.CreateInstance(type);
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