using System.Globalization;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    private sealed class ClientInfoPayload
    {
        public string Value { get; set; } = string.Empty;
    }

    private static class ClientInfoTestActions
    {
        [ContentInvocation("tests.client-info.echo")]
        public static IHtmlContent Echo(
            ClientInfoPayload payload,
            HeimdallClientInfo client)
        {
            var summary = string.Join("|",
                payload.Value,
                client.IsAvailable,
                client.TimeZone,
                client.UtcOffsetMinutes,
                client.Locale,
                string.Join(',', client.Languages),
                FormattableString.Invariant($"{client.ViewportWidth}x{client.ViewportHeight}"),
                $"{client.ScreenWidth}x{client.ScreenHeight}",
                client.DevicePixelRatio.ToString(CultureInfo.InvariantCulture),
                client.Orientation,
                client.DeviceCategory,
                client.ColorScheme,
                client.PrefersReducedMotion,
                client.PrefersContrast,
                client.ForcedColors,
                client.Touch,
                client.MaxTouchPoints,
                client.Pointer,
                client.Hover,
                client.Online);

            return Html.Span(summary);
        }

        [ContentInvocation("tests.client-info.storage")]
        public static IHtmlContent Storage(HeimdallClientInfo client)
        {
            var local = client.Storage.Local.GetValueOrDefault("ui.theme") ?? string.Empty;
            var session = client.Storage.Session.GetValueOrDefault("checkout.state") ?? string.Empty;
            var extension = client.Extensions.TryGetValue("appMode", out var value)
                ? value.GetString() ?? value.GetRawText()
                : string.Empty;

            return Html.Span($"{local}|{session}|{extension}|{client.IsAvailable}");
        }
    }
}
