using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    private static class AntiforgeryTestActions
    {
        [RequireAntiforgeryToken(false)]
        [ContentInvocation("tests.antiforgery.method-disabled")]
        public static IHtmlContent MethodDisabled()
            => Html.Span("method antiforgery disabled");

        [ContentInvocation("tests.antiforgery.default")]
        public static IHtmlContent Default()
            => Html.Span("antiforgery required");
    }

    [RequireAntiforgeryToken(false)]
    private static class TypeAntiforgeryDisabledActions
    {
        [ContentInvocation("tests.antiforgery.type-disabled")]
        public static IHtmlContent TypeDisabled()
            => Html.Span("type antiforgery disabled");

        [RequireAntiforgeryToken(true)]
        [ContentInvocation("tests.antiforgery.method-enabled")]
        public static IHtmlContent MethodEnabled()
            => Html.Span("method antiforgery enabled");
    }
}
