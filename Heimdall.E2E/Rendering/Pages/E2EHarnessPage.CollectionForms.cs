using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.E2E.Rendering.Pages;

public static partial class E2EHarnessPage
{
    public sealed class CollectionFormRequest
    {
        public string[] Names { get; set; } = [];
        public List<string> Aliases { get; set; } = [];
    }

    [ContentInvocation("e2e.form.collections.properties")]
    public static IHtmlContent CollectionFormProperties([ContentPayload] CollectionFormRequest request)
        => Html.Span($"{request.Names.Length}:{string.Join('|', request.Names)};{request.Aliases.Count}:{string.Join('|', request.Aliases)}");

    [ContentInvocation("e2e.form.collections.parameter")]
    public static IHtmlContent CollectionFormParameter([ContentPayload] string[] names)
        => Html.Span($"{names.Length}:{string.Join('|', names)}");
}
