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
    private sealed class CustomMvcRenderer : IHeimdallMvcRenderer
    {
        public Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IHtmlContent>(new HtmlString("custom renderer"));

        public Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model,
            Action<ViewDataDictionary> configureViewData,
            CancellationToken cancellationToken = default)
        {
            var viewData = new ViewDataDictionary(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = model
            };
            configureViewData(viewData);
            return Task.FromResult<IHtmlContent>(new HtmlString("custom renderer"));
        }
    }

    private sealed class FakeCompositeViewEngine : ICompositeViewEngine
    {
        private static readonly IView FakeView = new FakeMvcPartialView();

        public IReadOnlyList<IViewEngine> ViewEngines { get; } = Array.Empty<IViewEngine>();

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            if (string.Equals(viewName, "missing", StringComparison.Ordinal))
            {
                return ViewEngineResult.NotFound(
                    viewName,
                    [$"/Views/{viewName}.cshtml", $"/Views/Shared/{viewName}.cshtml"]);
            }

            return ViewEngineResult.Found(viewName, FakeView);
        }

        public ViewEngineResult GetView(string? executingFilePath, string viewPath, bool isMainPage)
        {
            if (string.Equals(viewPath, "missing", StringComparison.Ordinal))
            {
                return ViewEngineResult.NotFound(
                    viewPath,
                    [$"/Views/{viewPath}.cshtml", $"/Views/Shared/{viewPath}.cshtml"]);
            }

            if (viewPath.StartsWith("~/", StringComparison.Ordinal) ||
                viewPath.StartsWith("/", StringComparison.Ordinal) ||
                viewPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            {
                return ViewEngineResult.Found(viewPath, FakeView);
            }

            return ViewEngineResult.NotFound(
                viewPath,
                [$"/Views/{viewPath}.cshtml", $"/Views/Shared/{viewPath}.cshtml"]);
        }
    }

    private sealed class FakeMvcPartialView : IView
    {
        public string Path => "/Views/Shared/_Greeting.cshtml";

        public Task RenderAsync(ViewContext context)
        {
            var payload = Assert.IsType<MvcPartialPayload>(context.ViewData.Model);
            var source = Assert.IsType<string>(context.ViewData["source"]);

            context.Writer.Write(
                $"<div id=\"mvc-partial\" data-source=\"{source}\">Hello {payload.Name}</div>");

            return Task.CompletedTask;
        }
    }
}
