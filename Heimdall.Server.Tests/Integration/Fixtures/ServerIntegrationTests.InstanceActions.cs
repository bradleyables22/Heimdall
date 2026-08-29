using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    private sealed class InstanceContentActions(GreetingService greeting)
    {
        [ContentInvocation("tests.instance.greeting")]
        public IHtmlContent Greeting(InstancePayload payload)
        {
            return Html.Span($"{greeting.Message}:{payload.Name}");
        }
    }

    private sealed class RegisteredInstanceContentActions(string message)
    {
        [ContentInvocation("tests.instance.registered")]
        public IHtmlContent Registered()
        {
            return Html.Span(message);
        }
    }

    private sealed class InstanceDiscoveryActions(ConstructedService service)
    {
        [ContentInvocation("tests.instance.discovery")]
        public IHtmlContent Render()
        {
            return Html.Span(service.GetType().Name);
        }
    }

    private sealed class CountingInstanceContentActions
    {
        private static int constructionCount;
        private readonly int instanceNumber;

        public CountingInstanceContentActions()
        {
            instanceNumber = Interlocked.Increment(ref constructionCount);
        }

        public static int ConstructionCount => constructionCount;

        public static void Reset()
        {
            Interlocked.Exchange(ref constructionCount, 0);
        }

        [ContentInvocation("tests.instance.activation-count")]
        public IHtmlContent Count()
        {
            return Html.Span(instanceNumber);
        }
    }

    private sealed class StatefulRegisteredInstanceContentActions
    {
        private int calls;

        [ContentInvocation("tests.instance.registered-state")]
        public IHtmlContent Next()
        {
            return Html.Span(Interlocked.Increment(ref calls));
        }
    }

    [Authorize]
    private sealed class InstanceAuthorizedActions
    {
        [ContentInvocation("tests.instance.auth.secure")]
        public IHtmlContent Secure(ClaimsPrincipal user)
        {
            return Html.Span(user.Identity?.Name ?? "anonymous");
        }

        [AllowAnonymous]
        [ContentInvocation("tests.instance.auth.public")]
        public IHtmlContent Public()
        {
            return Html.Span("instance public");
        }
    }

    [RequestTimeout(50)]
    private sealed class InstanceTimeoutActions
    {
        [ContentInvocation("tests.instance.timeout.slow")]
        public async Task<IHtmlContent> Slow(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Html.Span("done");
        }
    }

    [ContentInvocationPrefix("tests.mvc")]
    private sealed class MvcContentActions(IHeimdallMvcRenderer views)
    {
        [ContentInvocation("partial")]
        public Task<IHtmlContent> Partial(MvcPartialPayload payload, CancellationToken cancellationToken)
        {
            return views.PartialAsync(
                payload.ViewName ?? "_Greeting",
                payload,
                viewData => viewData["source"] = "heimdall",
                cancellationToken);
        }

        [ContentInvocation("missing")]
        public Task<IHtmlContent> Missing(CancellationToken cancellationToken)
            => views.PartialAsync("missing", cancellationToken: cancellationToken);
    }

    [ContentInvocationPrefix("tests.prefix.static")]
    private static class PrefixedStaticContentActions
    {
        [ContentInvocation("refresh")]
        public static IHtmlContent Refresh()
        {
            return Html.Span("prefixed static");
        }
    }

    [ContentInvocationPrefix("tests.prefix.instance")]
    private sealed class PrefixedInstanceContentActions(GreetingService greeting)
    {
        [ContentInvocation]
        public IHtmlContent Render()
        {
            return Html.Span(greeting.Message);
        }
    }

    [ContentInvocationPrefix(".tests.prefix.normalized.")]
    private static class NormalizedPrefixContentActions
    {
        [ContentInvocation(".refresh.")]
        public static IHtmlContent Refresh()
        {
            return Html.Span("normalized prefix");
        }
    }

    private static class DefaultContentActions
    {
        [ContentInvocation]
        public static IHtmlContent Ping()
        {
            return Html.Span("default action id");
        }
    }

    [Authorize]
    private static class TypeAuthorizedActions
    {
        [AllowAnonymous]
        [ContentInvocation("tests.auth.allow-anonymous")]
        public static IHtmlContent Public()
        {
            return Html.Span("public");
        }
    }

    [RequestTimeout(50)]
    private static class TypeTimeoutActions
    {
        [DisableRequestTimeout]
        [ContentInvocation("tests.timeout.disabled")]
        public static async Task<IHtmlContent> Disabled(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(125), cancellationToken);
            return Html.Span("not timed out");
        }
    }

    [RequestSizeLimit(256)]
    private static class TypeRequestSizeLimitedUploadActions
    {
        [ContentInvocation("tests.upload.type-request-size-limited")]
        public static IHtmlContent Limited(IFormFile upload)
            => Html.Span(upload.Length);

        [DisableRequestSizeLimit]
        [ContentInvocation("tests.upload.request-size-disabled")]
        public static IHtmlContent Disabled(IFormFile upload)
            => Html.Span(upload.Length);

        [RequestSizeLimit(4096)]
        [ContentInvocation("tests.upload.request-size-overridden")]
        public static IHtmlContent Overridden(IFormFile upload)
            => Html.Span(upload.Length);
    }

    [RequestFormLimits(MultipartBodyLengthLimit = 4)]
    private static class TypeFormLimitedUploadActions
    {
        [ContentInvocation("tests.upload.type-form-limited")]
        public static IHtmlContent Limited(IFormFile upload)
            => Html.Span(upload.Length);

        [RequestFormLimits(MultipartBodyLengthLimit = 128)]
        [ContentInvocation("tests.upload.form-limit-overridden")]
        public static IHtmlContent Overridden(IFormFile upload)
            => Html.Span(upload.Length);
    }
}
