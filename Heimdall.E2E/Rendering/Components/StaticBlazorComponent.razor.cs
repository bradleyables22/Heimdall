using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.E2E.Rendering.Components;

[ContentInvocationPrefix("e2e.blazor")]
public partial class StaticBlazorComponent
{
    public const string Action_Render = "e2e.blazor.render";

    public sealed class Payload
    {
        public string Name { get; set; } = string.Empty;
    }

    [ContentInvocation("render")]
    public static Task<IHtmlContent> Render(
        [FromServices] IHeimdallBlazorRenderer renderer,
        [ContentPayload] Payload payload,
        CancellationToken cancellationToken)
        => renderer.RenderAsync<StaticBlazorResult>(
            new { Name = payload?.Name ?? string.Empty },
            cancellationToken);
}
