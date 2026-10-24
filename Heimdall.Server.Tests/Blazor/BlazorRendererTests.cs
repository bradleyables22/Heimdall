using System.Collections;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server.Tests;

public sealed class BlazorRendererTests
{
    [Fact]
    public async Task AddHeimdallBlazor_RegistersRenderer()
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();

        var first = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();
        var second = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        Assert.Same(first, second);

        await using var secondScope = app.Services.CreateAsyncScope();
        Assert.NotSame(first, secondScope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>());
    }

    [Fact]
    public async Task AddHeimdallBlazor_PreservesCustomRendererRegistration()
    {
        await using var app = CreateApp(services =>
        {
            services.AddSingleton<IHeimdallBlazorRenderer, CustomBlazorRenderer>();
        });
        await using var scope = app.Services.CreateAsyncScope();

        Assert.IsType<CustomBlazorRenderer>(
            scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>());
    }

    [Fact]
    public async Task Renderer_RendersParametersDependencyInjectionAndAsyncInitialization()
    {
        await using var app = CreateApp(services =>
        {
            services.AddSingleton(new BlazorGreeting("hello"));
        });
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var content = await renderer.RenderAsync<AsyncGreetingComponent>(new
        {
            Name = "Ada <Lovelace>"
        });
        var html = content.ToHtmlString();

        Assert.Contains("data-name=\"Ada &lt;Lovelace&gt;\"", html);
        Assert.Contains("hello: Ada &lt;Lovelace&gt;", html);
        Assert.Contains("data-initialized", html);
    }

    [Fact]
    public async Task Renderer_AcceptsNativeParameterView()
    {
        await using var app = CreateApp(services =>
        {
            services.AddSingleton(new BlazorGreeting("hello"));
        });
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(AsyncGreetingComponent.Name)] = "Grace"
        });

        var content = await renderer.RenderAsync<AsyncGreetingComponent>(parameters);

        Assert.Contains("data-name=\"Grace\"", content.ToHtmlString());
    }

    [Fact]
    public async Task Renderer_RendersCompiledRazorComponentAndNestedChildLifecycle()
    {
        await using var app = CreateApp(services =>
        {
            services.AddSingleton(new BlazorGreeting("hello"));
        });
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var content = await renderer.RenderAsync<TestRazorComponent>(new
        {
            Name = "Ada <Lovelace>"
        });
        var html = content.ToHtmlString();

        Assert.Contains("<section data-name=\"Ada &lt;Lovelace&gt;\">", html);
        Assert.Contains("<span data-child=\"Ada &lt;Lovelace&gt;\">hello: Ada &lt;Lovelace&gt;</span>", html);
        Assert.Contains("<strong>initialized</strong>", html);
    }

    [Fact]
    public async Task Renderer_AcceptsNonGenericDictionaryParameters()
    {
        await using var app = CreateApp(services =>
        {
            services.AddSingleton(new BlazorGreeting("hello"));
        });
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var content = await renderer.RenderAsync<AsyncGreetingComponent>(
            new Hashtable
            {
                [nameof(AsyncGreetingComponent.Name)] = "Grace"
            });

        Assert.Contains("data-name=\"Grace\"", content.ToHtmlString());
    }

    [Fact]
    public async Task Renderer_RejectsNonStringDictionaryKeys()
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => renderer.RenderAsync<AsyncGreetingComponent>(new Hashtable { [42] = "invalid" }));

        Assert.Contains("keys must be strings", exception.Message);
    }

    [Fact]
    public async Task Renderer_RejectsUnknownComponentParameters()
    {
        await using var app = CreateApp(services =>
        {
            services.AddSingleton(new BlazorGreeting("hello"));
        });
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => renderer.RenderAsync<AsyncGreetingComponent>(new { Unknown = "value" }));

        Assert.Contains("Unknown", exception.Message);
    }

    [Fact]
    public async Task Renderer_RendersTypeOverload()
    {
        await using var app = CreateApp(services =>
        {
            services.AddSingleton(new BlazorGreeting("hello"));
        });
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var content = await renderer.RenderAsync(
            typeof(AsyncGreetingComponent),
            new Dictionary<string, object?>
            {
                [nameof(AsyncGreetingComponent.Name)] = "Lin"
            });

        Assert.Contains("data-name=\"Lin\"", content.ToHtmlString());
    }

    [Fact]
    public async Task Renderer_RejectsNonComponentType()
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => renderer.RenderAsync(typeof(string)));

        Assert.Contains("must implement", exception.Message);
    }

    [Fact]
    public async Task Renderer_HonorsCancellationBeforeRendering()
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => renderer.RenderAsync<AsyncGreetingComponent>(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Renderer_HonorsCancellationAfterComponentRenderingCompletes()
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();
        var gate = new RenderGate();
        using var cancellation = new CancellationTokenSource();

        var renderTask = renderer.RenderAsync<BlockedComponent>(
            new { Gate = gate },
            cancellation.Token);

        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        gate.Release.TrySetResult(null);

        await Assert.ThrowsAsync<OperationCanceledException>(() => renderTask);
    }

    [Fact]
    public async Task Renderer_DisposesComponentAfterMaterializingHtml()
    {
        var probe = new RenderProbe();
        await using var app = CreateApp(services =>
        {
            services.AddSingleton(probe);
        });
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var content = await renderer.RenderAsync<DisposableComponent>();

        Assert.True(probe.Disposed);
        Assert.Equal("<p>disposable</p>", content.ToHtmlString());
    }

    [Fact]
    public async Task Renderer_PropagatesComponentInitializationFailures()
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => renderer.RenderAsync<FailingComponent>());

        Assert.Contains("component initialization failed", exception.Message);
    }

    [Fact]
    public async Task Renderer_DoesNotProduceInteractiveEventAttributes()
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IHeimdallBlazorRenderer>();

        var content = await renderer.RenderAsync<InteractiveMarkupComponent>();
        var html = content.ToHtmlString();

        Assert.Equal("<button>Static button</button>", html);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplication CreateApp(Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        configureServices?.Invoke(builder.Services);
        builder.Services.AddHeimdallBlazor();

        return builder.Build();
    }

    private sealed class AsyncGreetingComponent : ComponentBase
    {
        [Inject]
        private BlazorGreeting Greeting { get; set; } = null!;

        [Parameter]
        public string? Name { get; set; }

        private bool _initialized;

        protected override async Task OnInitializedAsync()
        {
            await Task.Yield();
            _initialized = true;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "article");
            builder.AddAttribute(1, "data-name", Name);
            builder.AddAttribute(2, "data-initialized", _initialized);
            builder.AddContent(3, $"{Greeting.Prefix}: {Name}");
            builder.CloseElement();
        }
    }

    private sealed class BlockedComponent : ComponentBase
    {
        [Parameter]
        public RenderGate Gate { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            Gate.Started.TrySetResult(null);
            await Gate.Release.Task;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, "released");
        }
    }

    private sealed class DisposableComponent : ComponentBase, IDisposable
    {
        [Inject]
        private RenderProbe Probe { get; set; } = null!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, "disposable");
            builder.CloseElement();
        }

        public void Dispose() => Probe.Disposed = true;
    }

    private sealed class FailingComponent : ComponentBase
    {
        protected override Task OnInitializedAsync()
            => Task.FromException(new InvalidOperationException("component initialization failed"));

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, "never rendered");
        }
    }

    private sealed class InteractiveMarkupComponent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, OnClick));
            builder.AddContent(2, "Static button");
            builder.CloseElement();
        }

        private void OnClick()
        {
        }
    }

    private sealed class CustomBlazorRenderer : IHeimdallBlazorRenderer
    {
        public Task<IHtmlContent> RenderAsync<TComponent>(CancellationToken cancellationToken = default)
            where TComponent : IComponent
            => Task.FromResult<IHtmlContent>(new HtmlString(string.Empty));

        public Task<IHtmlContent> RenderAsync<TComponent>(
            object? parameters,
            CancellationToken cancellationToken = default)
            where TComponent : IComponent
            => Task.FromResult<IHtmlContent>(new HtmlString(string.Empty));

        public Task<IHtmlContent> RenderAsync<TComponent>(
            ParameterView parameters,
            CancellationToken cancellationToken = default)
            where TComponent : IComponent
            => Task.FromResult<IHtmlContent>(new HtmlString(string.Empty));

        public Task<IHtmlContent> RenderAsync(
            Type componentType,
            object? parameters = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IHtmlContent>(new HtmlString(string.Empty));
    }

    private sealed class RenderProbe
    {
        public bool Disposed { get; set; }
    }

    private sealed class RenderGate
    {
        public TaskCompletionSource<object?> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<object?> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal sealed class BlazorGreeting(string prefix)
{
    public string Prefix { get; } = prefix;
}
