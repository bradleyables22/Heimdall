using Heimdall.Server.Helpers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
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

        // ---------------------------------------------------------------------
        // Make JSON binding as accommodating as possible (esp. "stringly" forms)
        // ---------------------------------------------------------------------
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                // Accept "123" into int/decimal/etc
                NumberHandling = JsonNumberHandling.AllowReadingFromString,

                // Debug/QoL (optional). You can remove these if you want strict JSON only.
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,

                // Web defaults already include:
                // PropertyNameCaseInsensitive = true,
                // PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            // Accept enum values as strings too (camelCase, etc.)
            o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            // Accept "true"/"false"/"1"/"0"/"on"/"off" as bool (and nullable bool)
            o.Converters.Add(new BoolFromStringConverter());
            o.Converters.Add(new NullableBoolFromStringConverter());

            return o;
        }

        // ---------------------------------------------------------------------
        // Endpoints
        // ---------------------------------------------------------------------
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

                var methodId = values.ToString();

                if (!registry.TryGet(methodId, out var method))
                    return Results.NotFound($"Unknown action '{methodId}'.");

                try
                {
                    var args = await BindArgumentsAsync(ctx, method);

                    object? invokeResult = method.Invoke(null, args);

                    if (invokeResult is null)
                        return Results.NoContent();

                    IHtmlContent? raw = invokeResult switch
                    {
                        IHtmlContent html => html,
                        Task<IHtmlContent> taskHtml => await taskHtml,
                        ValueTask<IHtmlContent> vtaskHtml => await vtaskHtml,
                        _ => throw new InvalidOperationException(
                            $"Heimdall action '{methodId}' returned unsupported type '{invokeResult.GetType().FullName}'. " +
                            "Expected IHtmlContent, Task<IHtmlContent>, or ValueTask<IHtmlContent>.")
                    };

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
            });

            return app;
        }

        private static async Task<object?[]> BindArgumentsAsync(HttpContext ctx, MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                return Array.Empty<object?>();

            var args = new object?[parameters.Length];
            JsonElement? bodyJson = null;
            bool bodyRead = false;
            bool payloadBound = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var pt = p.ParameterType;

                if (pt == typeof(HttpContext))
                {
                    args[i] = ctx;
                    continue;
                }

                if (pt == typeof(CancellationToken))
                {
                    args[i] = ctx.RequestAborted;
                    continue;
                }

                if (pt == typeof(System.Security.Claims.ClaimsPrincipal))
                {
                    args[i] = ctx.User;
                    continue;
                }

                var service = ctx.RequestServices.GetService(pt);
                if (service != null)
                {
                    args[i] = service;
                    continue;
                }

                if (payloadBound)
                {
                    throw new InvalidOperationException(
                        $"Action '{method.DeclaringType?.Name}.{method.Name}' " +
                        "has multiple non-DI parameters. Only one payload DTO is allowed.");
                }

                if (!bodyRead)
                {
                    bodyRead = true;

                    if (ctx.Request.ContentLength is null or 0)
                        throw new InvalidOperationException("Request body is empty.");

                    bodyJson = await JsonSerializer.DeserializeAsync<JsonElement>(
                        ctx.Request.Body,
                        JsonOptions);
                }

                args[i] = bodyJson!.Value.Deserialize(pt, JsonOptions)
                    ?? throw new InvalidOperationException(
                        $"Failed to bind payload parameter '{p.Name}'.");

                payloadBound = true;
            }

            return args;
        }

        // ---------------------------------------------------------------------
        // Converters
        // ---------------------------------------------------------------------

        private sealed class BoolFromStringConverter : JsonConverter<bool>
        {
            public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.True) return true;
                if (reader.TokenType == JsonTokenType.False) return false;

                if (reader.TokenType == JsonTokenType.Number)
                {
                    // Accept 0/1 as well
                    if (reader.TryGetInt64(out var n))
                        return n != 0;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString()?.Trim();

                    if (s is null) return false;

                    if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                    if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

                    if (s.Equals("on", StringComparison.OrdinalIgnoreCase)) return true;
                    if (s.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;

                    if (s == "1") return true;
                    if (s == "0") return false;
                }

                throw new JsonException($"Invalid boolean value (token: {reader.TokenType}).");
            }

            public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
                => writer.WriteBooleanValue(value);
        }

        private sealed class NullableBoolFromStringConverter : JsonConverter<bool?>
        {
            public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;

                // Reuse same logic as non-nullable
                var inner = new BoolFromStringConverter();
                return inner.Read(ref reader, typeof(bool), options);
            }

            public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
            {
                if (value is null) writer.WriteNullValue();
                else writer.WriteBooleanValue(value.Value);
            }
        }
    }
}
