using System.Net;
using System.Text.RegularExpressions;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Server.Tests.Authoring;

using Heimdall = global::Heimdall.Server.Rendering.Heimdall;

public sealed class HeimdallAuthoringIntegrationTests
{
    [Fact]
    public async Task CompiledBlazorComponentSplatsCanonicalAttributesAndEncodesContent()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<NavigationManager>(new TestNavigationManager());
        await using var app = builder.Build();
        await using var renderer = new HtmlRenderer(app.Services, app.Services.GetRequiredService<ILoggerFactory>());
        const string name = "<img src=x> & Ada";

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<FluentAttributes>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(FluentAttributes.Name)] = name }));
            return root.ToHtmlString();
        });

        Assert.Contains("heimdall-content-submit=\"orders.save\"", html);
        Assert.Contains("heimdall-payload-from=\"closest-form\"", html);
        Assert.Contains("heimdall-debounce=\"300\"", html);
        Assert.Contains("heimdall-sync=\"replace\"", html);
        Assert.Contains("heimdall-content-target=\"#explicit\"", Button(html));
        Assert.DoesNotContain("#default", Button(html));
        Assert.Contains("id=\"forwarded\"", html);
        Assert.Contains("heimdall-content-disable=\"false\"", html);
        Assert.Contains("heimdall-time-format=\"yyyy-MM-dd\"", html);
        Assert.Contains(">2026-08-26</span>", html);
        Assert.Contains("&lt;img src=x&gt; &amp; Ada", html);
        Assert.DoesNotContain("&amp;lt;img", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("hd-", html);
    }

    [Fact]
    public async Task BlazorRerenderRemovesStaleAttributesAndUpdatesFalseValues()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<NavigationManager>(new TestNavigationManager());
        await using var app = builder.Build();
        await using var renderer = new HtmlRenderer(app.Services, app.Services.GetRequiredService<ILoggerFactory>());

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            UpdatingHost? host = null;
            var root = await renderer.RenderComponentAsync<UpdatingHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(UpdatingHost.Capture)] = (Action<UpdatingHost>)(value => host = value)
            }));
            var first = Button(root.ToHtmlString());
            Assert.Contains("heimdall-content-click=\"orders.first\"", first);
            Assert.Contains("heimdall-payload=", first);
            Assert.Contains("heimdall-content-disable", first);

            Assert.NotNull(host);
            host.Update();
            var second = Button(root.ToHtmlString());
            Assert.DoesNotContain("heimdall-content-click", second);
            Assert.DoesNotContain("heimdall-payload", second);
            Assert.Contains("heimdall-content-input=\"orders.second\"", second);
            Assert.Contains("heimdall-content-disable=\"false\"", second);
            Assert.Contains("heimdall-content-target=\"#explicit\"", second);
        });
    }

    [Fact]
    public async Task CompiledRazorViewComposesWithMvcFormAndInputHelpers()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HeimdallAuthoringIntegrationTests).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddHeimdallMvc();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(AuthoringFixtureController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        app.MapGet("/authoring", async (IHeimdallMvcRenderer views) => Results.Content(
            (await views.PartialAsync("/Authoring/Views/FluentAttributes.cshtml", new AuthoringModel { Name = "<img src=x> & Ada" })).ToHtmlString(),
            "text/html"));
        await app.StartAsync();
        using var client = app.GetTestClient();
        var html = await client.GetStringAsync("/authoring");

        Assert.Contains("heimdall-content-submit=\"orders.save\"", html);
        Assert.Contains("heimdall-payload-from=\"closest-form\"", html);
        Assert.Contains("heimdall-content-target=\"#explicit\"", RazorButton(html));
        Assert.DoesNotContain("#default", RazorButton(html));
        Assert.Contains("heimdall-content-disable=\"false\"", RazorButton(html));
        Assert.Contains("action=\"/authoring/save\"", html);
        Assert.Contains("name=\"Name\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.DoesNotContain("heimdall=", html);
        Assert.Equal("<button id=\"razor-conditional\">", Regex.Match(html, "<button id=\"razor-conditional\"[^>]*>").Value);
        Assert.DoesNotContain("asp-", html);
        Assert.Contains("&lt;img src=x&gt; &amp; Ada", html);
        Assert.DoesNotContain("&amp;lt;img", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains(">2026-08-26</span>", html);
        Assert.Contains(">Explicit fallback</span>", html);
        Assert.Contains("<span>&lt;img src=x&gt; &amp; Ada</span>", html);
        Assert.Single(Regex.Matches(RazorButton(html), "heimdall-content-target="));
        Assert.Contains("2026-08-26T18:30:05.000Z", WebUtility.HtmlDecode(html));
    }

    private static string Button(string html) => Regex.Match(html, "<button id=\"fluent-button\"[^>]*>").Value;
    private static string RazorButton(string html) => Regex.Match(html, "<button id=\"razor-button\"[^>]*>").Value;

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/authoring");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    public sealed class UpdatingHost : ComponentBase
    {
        [Parameter] public Action<UpdatingHost>? Capture { get; set; }
        private FluentHeimdall.HeimdallBuilder _button = Heimdall.OnClick("orders.first").Payload(new { Id = 1 }).Disable();
        protected override void OnInitialized() => Capture?.Invoke(this);
        public void Update()
        {
            _button = Heimdall.OnInput("orders.second").Disable(false);
            StateHasChanged();
        }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<FluentAttributes>(0);
            builder.AddAttribute(1, nameof(FluentAttributes.Button), _button);
            builder.CloseComponent();
        }
    }
}
