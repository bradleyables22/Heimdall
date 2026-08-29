using System.Text.Json;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Heimdall.Server.Tests;

public sealed partial class StaticSiteGenerationTests
{
    [Fact]
    public async Task GenerateAsync_ResolvesRelativeOutputPathFromContentRoot()
    {
        var contentRoot = CreateTempDirectory();

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(contentRoot));
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "site";
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var expectedOutputRoot = Path.Combine(contentRoot, "site");
            Assert.Equal(Path.GetFullPath(expectedOutputRoot), result.OutputRootPath);
            Assert.Equal("home", await File.ReadAllTextAsync(Path.Combine(expectedOutputRoot, "index.html")));
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_UseWebRootPathResolvesOutputRootFromWebRoot()
    {
        var contentRoot = CreateTempDirectory();

        try
        {
            var webRoot = Path.Combine(contentRoot, "public");
            Directory.CreateDirectory(webRoot);
            var environment = new TestWebHostEnvironment(contentRoot, webRoot);

            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.UseWebRootPath();
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            Assert.Equal(Path.GetFullPath(webRoot), result.OutputRootPath);
            Assert.Equal("home", await File.ReadAllTextAsync(Path.Combine(webRoot, "index.html")));
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_UseWebRootPathCanResolveSubdirectoryUnderWebRoot()
    {
        var contentRoot = CreateTempDirectory();

        try
        {
            var webRoot = Path.Combine(contentRoot, "wwwroot");
            Directory.CreateDirectory(webRoot);
            var environment = new TestWebHostEnvironment(contentRoot, webRoot);

            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.UseWebRootPath("static-site");
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var expectedOutputRoot = Path.Combine(webRoot, "static-site");
            Assert.Equal(Path.GetFullPath(expectedOutputRoot), result.OutputRootPath);
            Assert.Equal("home", await File.ReadAllTextAsync(Path.Combine(expectedOutputRoot, "index.html")));
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_UseWebRootPathRequiresWebHostEnvironment()
    {
        var services = new ServiceCollection();
        services
            .AddHeimdallStaticSiteGeneration(options =>
            {
                options.UseWebRootPath();
            })
            .WithStaticPage("/", () => Html.Text("home"));

        await using var provider = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GenerateHeimdallStaticSiteAsync());

        Assert.Contains("web root", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IWebHostEnvironment.WebRootPath", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_StopsWhenCancelledAndDoesNotRenderLaterPages()
    {
        var outputPath = CreateTempDirectory();
        using var cts = new CancellationTokenSource();
        var renderedRoutes = new List<string>();

        try
        {
            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                })
                .WithStaticPage("/one", () =>
                {
                    renderedRoutes.Add("/one");
                    return Html.Text("one");
                })
                .WithStaticPage("/two", ctx =>
                {
                    renderedRoutes.Add("/two");
                    cts.Cancel();
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    return Html.Text("two");
                })
                .WithStaticPage("/three", () =>
                {
                    renderedRoutes.Add("/three");
                    return Html.Text("three");
                });

            await using var provider = services.BuildServiceProvider();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => provider.GenerateHeimdallStaticSiteAsync(cts.Token));

            Assert.Equal(["/one", "/two"], renderedRoutes);
            Assert.True(File.Exists(Path.Combine(outputPath, "one", "index.html")));
            Assert.False(File.Exists(Path.Combine(outputPath, "two", "index.html")));
            Assert.False(File.Exists(Path.Combine(outputPath, "three", "index.html")));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_AddsRouteContextToRenderFailures()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                })
                .WithStaticPage("/broken", () => throw new InvalidOperationException("render exploded"));

            await using var provider = services.BuildServiceProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GenerateHeimdallStaticSiteAsync());

            Assert.Contains("/broken", ex.Message);
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Equal("render exploded", ex.InnerException.Message);
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_AddsRouteContextToWriteFailures()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var existingFile = Path.Combine(outputPath, "index.html");
            await File.WriteAllTextAsync(existingFile, "existing");

            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.OverwriteExistingFiles = false;
                })
                .WithStaticPage("/", () => Html.Text("replacement"));

            await using var provider = services.BuildServiceProvider();

            var ex = await Assert.ThrowsAsync<IOException>(
                () => provider.GenerateHeimdallStaticSiteAsync());

            Assert.Contains("static page '/'", ex.Message);
            Assert.Contains("index.html", ex.Message);
            Assert.IsType<IOException>(ex.InnerException);
            Assert.Equal("existing", await File.ReadAllTextAsync(existingFile));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }
}
