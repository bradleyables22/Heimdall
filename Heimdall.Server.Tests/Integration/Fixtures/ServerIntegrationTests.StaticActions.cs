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
    private static class TestContentActions
    {
        [Authorize]
        [ContentInvocation("tests.auth.secure")]
        public static IHtmlContent Secure(ClaimsPrincipal user)
        {
            return Html.Span(user.Identity?.Name ?? "anonymous");
        }

        [Authorize(Policy = "tests.admin")]
        [ContentInvocation("tests.auth.admin")]
        public static IHtmlContent AdminOnly()
        {
            return Html.Span("admin");
        }

        [RequestTimeout(50)]
        [ContentInvocation("tests.timeout.slow")]
        public static async Task<IHtmlContent> Slow(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Html.Span("done");
        }

        [RequestTimeout("tests.fast-teapot")]
        [ContentInvocation("tests.timeout.named-policy")]
        public static async Task<IHtmlContent> NamedPolicySlow(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Html.Span("done");
        }

        [ContentInvocation("tests.classification.service")]
        public static IHtmlContent UsesExplicitService([FromServices] ConstructedService service)
        {
            return Html.Span(service.GetType().Name);
        }

        [ContentInvocation("tests.payload.complex")]
        public static IHtmlContent ComplexPayload(PayloadDto payload)
        {
            return Html.Span($"{payload.Name}|{payload.Count}|{payload.Enabled}|{payload.Mode}");
        }

        [ContentInvocation("tests.payload.simple")]
        public static IHtmlContent SimplePayload(string name = "fallback")
        {
            return Html.Span(name);
        }

        [ContentInvocation("tests.payload.explicit")]
        public static IHtmlContent ExplicitPayload([ContentPayload] ServiceLikePayload payload)
        {
            return Html.Span(payload.Value);
        }

        [ContentInvocation("tests.upload.from-form-aliases")]
        public static IHtmlContent FromFormAliases(
            [FromForm(Name = "model")] ServiceLikePayload payload,
            [FromForm(Name = "file")] IFormFile upload)
        {
            return Html.Span($"{payload.Value}|{upload.FileName}|{upload.Length}");
        }

        [ContentInvocation("tests.upload.single")]
        public static IHtmlContent SingleUpload(PayloadDto payload, IFormFile avatar)
        {
            return Html.Span(
                $"{payload.Name}|{payload.Count}|{payload.Enabled}|{payload.Mode}|{avatar.FileName}|{avatar.Length}");
        }

        [ContentInvocation("tests.upload.multiple")]
        public static IHtmlContent MultipleUploads(IReadOnlyList<IFormFile> attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.enumerable")]
        public static IHtmlContent EnumerableUploads(IEnumerable<IFormFile> attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.readonly-collection")]
        public static IHtmlContent ReadOnlyCollectionUploads(IReadOnlyCollection<IFormFile> attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.collection")]
        public static IHtmlContent CollectionUploads(ICollection<IFormFile> attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.list-interface")]
        public static IHtmlContent ListInterfaceUploads(IList<IFormFile> attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.list")]
        public static IHtmlContent ListUploads(List<IFormFile> attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.form-file-collection")]
        public static IHtmlContent FileCollectionUploads(IFormFileCollection attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.array")]
        public static IHtmlContent ArrayUploads(IFormFile[] attachments)
        {
            return RenderUploads(attachments);
        }

        [ContentInvocation("tests.upload.optional")]
        public static IHtmlContent OptionalUpload(IFormFile? upload = null)
        {
            return Html.Span(upload?.FileName ?? "none");
        }

        [ContentInvocation("tests.upload.required")]
        public static IHtmlContent RequiredUpload(IFormFile upload)
        {
            return Html.Span(upload.FileName);
        }

        [RequestSizeLimit(256)]
        [ContentInvocation("tests.upload.request-size-limited")]
        public static IHtmlContent RequestSizeLimitedUpload(IFormFile upload)
        {
            return Html.Span(upload.Length);
        }

        private static IHtmlContent RenderUploads(IEnumerable<IFormFile> attachments)
        {
            return Html.Span(string.Join('|', attachments.Select(file => $"{file.FileName}:{file.Length}")));
        }

        [ContentInvocation("tests.services.implicit")]
        public static IHtmlContent ImplicitService(GreetingService service)
        {
            return Html.Span(service.Message);
        }

        [ContentInvocation("tests.logging.failure")]
        public static IHtmlContent LoggedFailure()
        {
            throw new InvalidOperationException("Expected logged action failure.");
        }
    }
}
