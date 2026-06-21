using System.Text.Json;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Heimdall.Server.Tests;

public sealed class StaticSiteGenerationTests
{
    [Fact]
    public async Task GenerateAsync_RendersStaticPagesWithScopedServices()
    {
        var outputPath = CreateTempDirectory();
        ScopedRenderProbe.Reset();

        try
        {
            var services = new ServiceCollection();
            services.AddScoped<ScopedRenderProbe>();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                })
                .WithStaticPage("/", ctx =>
                {
                    var probe = ctx.GetRequiredService<ScopedRenderProbe>();
                    return Html.Div($"home:{probe.InstanceId}:{ctx.Route}");
                })
                .WithStaticPage("/about", async ctx =>
                {
                    await Task.Yield();
                    var probe = ctx.GetRequiredService<ScopedRenderProbe>();
                    return Html.Div($"about:{probe.InstanceId}:{Path.GetFileName(ctx.OutputFilePath)}");
                })
                .WithStaticPage("/feed.xml", () => Html.Raw("<rss></rss>"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            Assert.Equal(Path.GetFullPath(outputPath), result.OutputRootPath);
            Assert.Equal(3, result.Pages.Count);
            Assert.Empty(result.Assets);
            Assert.Equal("<div>home:1:/</div>", await File.ReadAllTextAsync(Path.Combine(outputPath, "index.html")));
            Assert.Equal("<div>about:2:index.html</div>", await File.ReadAllTextAsync(Path.Combine(outputPath, "about", "index.html")));
            Assert.Equal("<rss></rss>", await File.ReadAllTextAsync(Path.Combine(outputPath, "feed.xml")));
            Assert.Equal(2, ScopedRenderProbe.DisposedCount);
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_AllowsEmptyPageRegistry()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var services = new ServiceCollection();
            services.AddHeimdallStaticSiteGeneration(options =>
            {
                options.OutputPath = outputPath;
            });

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            Assert.Equal(Path.GetFullPath(outputPath), result.OutputRootPath);
            Assert.Empty(result.Pages);
            Assert.Empty(result.Assets);
            Assert.True(Directory.Exists(outputPath));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_WritesManifestByDefault()
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
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            Assert.NotNull(result.ManifestFilePath);
            Assert.True(File.Exists(result.ManifestFilePath));

            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(result.ManifestFilePath));
            Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("heimdall.static.manifest.json", manifest.RootElement.GetProperty("manifestRelativePath").GetString());

            var files = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
            var page = Assert.Single(files);
            Assert.Equal("page", page.GetProperty("kind").GetString());
            Assert.Equal("/", page.GetProperty("route").GetString());
            Assert.Equal("index.html", page.GetProperty("relativePath").GetString());
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_CleanOutputPathFullyCleansArtifactDirectory()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(outputPath, "stale"));
            await File.WriteAllTextAsync(Path.Combine(outputPath, "stale", "old.txt"), "old");
            await File.WriteAllTextAsync(Path.Combine(outputPath, "loose.txt"), "loose");

            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.CleanOutputPath = true;
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            await provider.GenerateHeimdallStaticSiteAsync();

            Assert.False(File.Exists(Path.Combine(outputPath, "stale", "old.txt")));
            Assert.False(File.Exists(Path.Combine(outputPath, "loose.txt")));
            Assert.Equal("home", await File.ReadAllTextAsync(Path.Combine(outputPath, "index.html")));
            Assert.True(File.Exists(Path.Combine(outputPath, "heimdall.static.manifest.json")));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_CleanOutputPathUsesManifestWhenOutputRootIsWebRoot()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            await File.WriteAllTextAsync(Path.Combine(webRoot, "css", "site.css"), "body{}");
            var environment = new TestWebHostEnvironment(contentRoot, webRoot);

            var firstServices = new ServiceCollection();
            firstServices.AddSingleton<IHostEnvironment>(environment);
            firstServices.AddSingleton<IWebHostEnvironment>(environment);
            firstServices
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.UseWebRootPath();
                    options.CopyWebRootAssets = false;
                    options.CopyStaticWebAssets = false;
                })
                .WithStaticPage("/keep", () => Html.Text("first"))
                .WithStaticPage("/stale", () => Html.Text("stale"));

            await using (var firstProvider = firstServices.BuildServiceProvider())
            {
                await firstProvider.GenerateHeimdallStaticSiteAsync();
            }

            var secondServices = new ServiceCollection();
            secondServices.AddSingleton<IHostEnvironment>(environment);
            secondServices.AddSingleton<IWebHostEnvironment>(environment);
            secondServices
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.UseWebRootPath();
                    options.CleanOutputPath = true;
                    options.CopyWebRootAssets = false;
                    options.CopyStaticWebAssets = false;
                })
                .WithStaticPage("/keep", () => Html.Text("second"));

            await using (var secondProvider = secondServices.BuildServiceProvider())
            {
                await secondProvider.GenerateHeimdallStaticSiteAsync();
            }

            Assert.Equal("body{}", await File.ReadAllTextAsync(Path.Combine(webRoot, "css", "site.css")));
            Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(webRoot, "keep", "index.html")));
            Assert.False(File.Exists(Path.Combine(webRoot, "stale", "index.html")));
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_GeneratesSitemapAndRobotsTxt()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.UseSitemap("https://example.com/docs");
                    options.UseRobotsTxt();
                })
                .WithStaticPage("/", () => Html.Text("home"))
                .WithStaticPage("/about", () => Html.Text("about"))
                .WithStaticPage("/feed.xml", () => Html.Text("feed"))
                .WithNotFoundPage(() => Html.Text("not found"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            Assert.Equal(2, result.SupplementalFiles.Count);
            Assert.Contains(result.SupplementalFiles, file => file.Kind == "sitemap");
            Assert.Contains(result.SupplementalFiles, file => file.Kind == "robots");

            var sitemap = await File.ReadAllTextAsync(Path.Combine(outputPath, "sitemap.xml"));
            Assert.Contains("<loc>https://example.com/docs/</loc>", sitemap);
            Assert.Contains("<loc>https://example.com/docs/about/</loc>", sitemap);
            Assert.DoesNotContain("feed.xml", sitemap);
            Assert.DoesNotContain("404.html", sitemap);

            var robots = await File.ReadAllTextAsync(Path.Combine(outputPath, "robots.txt"));
            Assert.Contains("User-agent: *", robots);
            Assert.Contains("Sitemap: https://example.com/docs/sitemap.xml", robots);
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_AppliesPathBaseToContextSitemapAndRobotsTxt()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.UsePathBase("/portal");
                    options.UseSitemap("https://example.com");
                    options.UseRobotsTxt();
                })
                .WithStaticPage("/", ctx => Html.Text(string.Join(
                    "|",
                    ctx.PathBase,
                    ctx.ToSitePath("/css/site.css"),
                    ctx.ToSitePath("docs/"),
                    ctx.ToSitePath("https://cdn.example.com/app.css"))))
                .WithStaticPage("/docs", () => Html.Text("docs"));

            await using var provider = services.BuildServiceProvider();

            await provider.GenerateHeimdallStaticSiteAsync();

            Assert.Equal(
                "/portal|/portal/css/site.css|/portal/docs/|https://cdn.example.com/app.css",
                await File.ReadAllTextAsync(Path.Combine(outputPath, "index.html")));

            var sitemap = await File.ReadAllTextAsync(Path.Combine(outputPath, "sitemap.xml"));
            Assert.Contains("<loc>https://example.com/portal/</loc>", sitemap);
            Assert.Contains("<loc>https://example.com/portal/docs/</loc>", sitemap);

            var robots = await File.ReadAllTextAsync(Path.Combine(outputPath, "robots.txt"));
            Assert.Contains("Allow: /portal/", robots);
            Assert.Contains("Sitemap: https://example.com/portal/sitemap.xml", robots);
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Theory]
    [InlineData("https://example.com/app")]
    [InlineData("/../app")]
    [InlineData("/app?x=1")]
    public async Task GenerateAsync_RejectsInvalidPathBase(string pathBase)
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.PathBase = pathBase;
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            await Assert.ThrowsAsync<ArgumentException>(
                () => provider.GenerateHeimdallStaticSiteAsync());
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_RejectsSupplementalFileCollisions()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var services = new ServiceCollection();
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.UseSitemap("https://example.com");
                })
                .WithStaticPage("/sitemap.xml", () => Html.Text("page"));

            await using var provider = services.BuildServiceProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GenerateHeimdallStaticSiteAsync());

            Assert.Contains("Static output collision", ex.Message);
            Assert.Contains("sitemap", ex.Message);
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task WithNotFoundPage_Generates404Html()
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
                .WithNotFoundPage(() => Html.Text("not found"));

            await using var provider = services.BuildServiceProvider();

            await provider.GenerateHeimdallStaticSiteAsync();

            Assert.Equal("not found", await File.ReadAllTextAsync(Path.Combine(outputPath, "404.html")));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Theory]
    [InlineData("--heimdall-generate-static")]
    [InlineData("--generate-static")]
    [InlineData("generate-static")]
    public async Task UseHeimdallStaticSiteGenerationAsync_GeneratesWhenRequested(string argument)
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.Services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.CopyWebRootAssets = false;
                    options.CopyStaticWebAssets = false;
                })
                .WithStaticPage("/", () => Html.Text(argument));

            await using var app = builder.Build();

            var handled = await app.UseHeimdallStaticSiteGenerationAsync([argument]);

            Assert.True(handled);
            Assert.Equal(argument, await File.ReadAllTextAsync(Path.Combine(outputPath, "index.html")));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task UseHeimdallStaticSiteGenerationAsync_ReturnsFalseWhenGenerationWasNotRequested()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.Services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.CopyWebRootAssets = false;
                    options.CopyStaticWebAssets = false;
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var app = builder.Build();

            var handled = await app.UseHeimdallStaticSiteGenerationAsync(["--not-static-generation"]);

            Assert.False(handled);
            Assert.False(File.Exists(Path.Combine(outputPath, "index.html")));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task UseHeimdallStaticSiteGenerationAsync_RequiresStaticGenerationServicesWhenRequested()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        await using var app = builder.Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.UseHeimdallStaticSiteGenerationAsync(["--heimdall-generate-static"]));

        Assert.Contains("AddHeimdallStaticSiteGeneration", ex.Message);
    }

    [Fact]
    public async Task RunWithHeimdallStaticSiteGenerationAsync_GeneratesAndReturnsWhenRequested()
    {
        var outputPath = CreateTempDirectory();

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.Services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = outputPath;
                    options.CopyWebRootAssets = false;
                    options.CopyStaticWebAssets = false;
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var app = builder.Build();

            await app.RunWithHeimdallStaticSiteGenerationAsync(["--heimdall-generate-static"]);

            Assert.Equal("home", await File.ReadAllTextAsync(Path.Combine(outputPath, "index.html")));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

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

    [Fact]
    public async Task GenerateAsync_CopiesPhysicalWebRootAssetsByDefault()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            Directory.CreateDirectory(Path.Combine(webRoot, "images"));
            await File.WriteAllTextAsync(Path.Combine(webRoot, "css", "app.css"), "body{}");
            await File.WriteAllTextAsync(Path.Combine(webRoot, "images", "logo.txt"), "logo");

            var environment = new TestWebHostEnvironment(contentRoot, webRoot);
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "dist";
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var outputRoot = Path.Combine(contentRoot, "dist");
            Assert.Equal("body{}", await File.ReadAllTextAsync(Path.Combine(outputRoot, "css", "app.css")));
            Assert.Equal("logo", await File.ReadAllTextAsync(Path.Combine(outputRoot, "images", "logo.txt")));
            Assert.Equal(2, result.Assets.Count);
            Assert.All(result.Assets, asset => Assert.StartsWith(outputRoot, asset.FilePath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_CanDisablePhysicalWebRootAssetCopying()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            await File.WriteAllTextAsync(Path.Combine(webRoot, "css", "app.css"), "body{}");

            var environment = new TestWebHostEnvironment(contentRoot, webRoot);
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "dist";
                    options.CopyWebRootAssets = false;
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var outputRoot = Path.Combine(contentRoot, "dist");
            Assert.False(File.Exists(Path.Combine(outputRoot, "css", "app.css")));
            Assert.Empty(result.Assets);
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_SkipsExistingOutputWhenOutputRootIsInsideWebRoot()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            Directory.CreateDirectory(Path.Combine(webRoot, "dist", "old"));
            await File.WriteAllTextAsync(Path.Combine(webRoot, "css", "app.css"), "body{}");
            await File.WriteAllTextAsync(Path.Combine(webRoot, "dist", "old", "stale.txt"), "stale");

            var environment = new TestWebHostEnvironment(contentRoot, webRoot);
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "wwwroot/dist";
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var outputRoot = Path.Combine(webRoot, "dist");
            Assert.Equal("body{}", await File.ReadAllTextAsync(Path.Combine(outputRoot, "css", "app.css")));
            Assert.True(File.Exists(Path.Combine(outputRoot, "old", "stale.txt")));
            Assert.False(File.Exists(Path.Combine(outputRoot, "dist", "old", "stale.txt")));
            Assert.DoesNotContain(result.Assets, asset => asset.FilePath.Contains($"{Path.DirectorySeparatorChar}old{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_CopiesStaticWebAssetsFromWebRootFileProvider()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var staticAssetRoot = Path.Combine(contentRoot, "static-assets");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            Directory.CreateDirectory(Path.Combine(staticAssetRoot, "_content", "HeimdallFramework.Web"));
            await File.WriteAllTextAsync(Path.Combine(webRoot, "css", "app.css"), "body{}");
            await File.WriteAllTextAsync(
                Path.Combine(staticAssetRoot, "_content", "HeimdallFramework.Web", "heimdall-bundle.min.js"),
                "window.Heimdall={};");

            using var staticWebAssetProvider = new PhysicalFileProvider(staticAssetRoot);
            var environment = new TestWebHostEnvironment(contentRoot, webRoot)
            {
                WebRootFileProvider = staticWebAssetProvider
            };
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "dist";
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var outputRoot = Path.Combine(contentRoot, "dist");
            Assert.Equal("body{}", await File.ReadAllTextAsync(Path.Combine(outputRoot, "css", "app.css")));
            Assert.Equal(
                "window.Heimdall={};",
                await File.ReadAllTextAsync(Path.Combine(outputRoot, "_content", "HeimdallFramework.Web", "heimdall-bundle.min.js")));
            Assert.Equal(2, result.Assets.Count);
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_CanDisableStaticWebAssetCopying()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var staticAssetRoot = Path.Combine(contentRoot, "static-assets");

        try
        {
            Directory.CreateDirectory(webRoot);
            Directory.CreateDirectory(Path.Combine(staticAssetRoot, "_content", "HeimdallFramework.Web"));
            await File.WriteAllTextAsync(
                Path.Combine(staticAssetRoot, "_content", "HeimdallFramework.Web", "heimdall-bundle.min.js"),
                "window.Heimdall={};");

            using var staticWebAssetProvider = new PhysicalFileProvider(staticAssetRoot);
            var environment = new TestWebHostEnvironment(contentRoot, webRoot)
            {
                WebRootFileProvider = staticWebAssetProvider
            };
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "dist";
                    options.CopyStaticWebAssets = false;
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var outputRoot = Path.Combine(contentRoot, "dist");
            Assert.False(File.Exists(Path.Combine(outputRoot, "_content", "HeimdallFramework.Web", "heimdall-bundle.min.js")));
            Assert.Empty(result.Assets);
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_HonorsOverwritePolicyForAssets()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            Directory.CreateDirectory(Path.Combine(contentRoot, "dist", "css"));
            await File.WriteAllTextAsync(Path.Combine(webRoot, "css", "app.css"), "body{}");
            await File.WriteAllTextAsync(Path.Combine(contentRoot, "dist", "css", "app.css"), "existing");

            var environment = new TestWebHostEnvironment(contentRoot, webRoot);
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "dist";
                    options.OverwriteExistingFiles = false;
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            await Assert.ThrowsAsync<IOException>(
                () => provider.GenerateHeimdallStaticSiteAsync());

            Assert.Equal("existing", await File.ReadAllTextAsync(Path.Combine(contentRoot, "dist", "css", "app.css")));
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_ReturnsAssetResultMetadata()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            var sourcePath = Path.Combine(webRoot, "css", "app.css");
            await File.WriteAllTextAsync(sourcePath, "body{}");

            var environment = new TestWebHostEnvironment(contentRoot, webRoot);
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "dist";
                })
                .WithStaticPage("/", () => Html.Text("home"));

            await using var provider = services.BuildServiceProvider();

            var result = await provider.GenerateHeimdallStaticSiteAsync();

            var asset = Assert.Single(result.Assets);
            Assert.Equal(Path.GetFullPath(sourcePath), asset.SourcePath);
            Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "dist", "css", "app.css")), asset.FilePath);
            Assert.Equal(new FileInfo(sourcePath).Length, asset.BytesWritten);
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_RejectsPageAndAssetOutputCollisions()
    {
        var contentRoot = CreateTempDirectory();
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(Path.Combine(webRoot, "css"));
            await File.WriteAllTextAsync(Path.Combine(webRoot, "css", "app.css"), "body{}");

            var environment = new TestWebHostEnvironment(contentRoot, webRoot);
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<IWebHostEnvironment>(environment);
            services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.OutputPath = "dist";
                })
                .WithStaticPage("/css/app.css", () => Html.Text("page"));

            await using var provider = services.BuildServiceProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GenerateHeimdallStaticSiteAsync());

            Assert.Contains("both generate", ex.Message);
            Assert.Contains("css", ex.Message);
        }
        finally
        {
            DeleteTempDirectory(contentRoot);
        }
    }

    [Fact]
    public void WithStaticPage_RejectsUnsafeRoutes()
    {
        var services = new ServiceCollection();
        var builder = services.AddHeimdallStaticSiteGeneration();

        Assert.Throws<ArgumentException>(() => builder.WithStaticPage("../escape", () => Html.Text("bad")));
        Assert.Throws<ArgumentException>(() => builder.WithStaticPage("/docs?x=1", () => Html.Text("bad")));
        Assert.Throws<ArgumentException>(() => builder.WithStaticPage("/docs#intro", () => Html.Text("bad")));
    }

    [Fact]
    public async Task GenerateAsync_RejectsDuplicateOutputFiles()
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
                .WithStaticPage("/about", () => Html.Text("first"))
                .WithStaticPage("/about/index.html", () => Html.Text("second"));

            await using var provider = services.BuildServiceProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GenerateHeimdallStaticSiteAsync());

            Assert.Contains("both generate", ex.Message);
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_HonorsOverwritePolicy()
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

            await Assert.ThrowsAsync<IOException>(
                () => provider.GenerateHeimdallStaticSiteAsync());

            Assert.Equal("existing", await File.ReadAllTextAsync(existingFile));
        }
        finally
        {
            DeleteTempDirectory(outputPath);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"heimdall-ssg-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private sealed class ScopedRenderProbe : IDisposable
    {
        private static int nextInstanceId;
        private static int disposedCount;

        public ScopedRenderProbe()
        {
            InstanceId = Interlocked.Increment(ref nextInstanceId);
        }

        public int InstanceId { get; }

        public static int DisposedCount => Volatile.Read(ref disposedCount);

        public static void Reset()
        {
            Interlocked.Exchange(ref nextInstanceId, 0);
            Interlocked.Exchange(ref disposedCount, 0);
        }

        public void Dispose()
        {
            Interlocked.Increment(ref disposedCount);
        }
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = Path.GetFullPath(contentRootPath);
        }

        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Heimdall.Server.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestWebHostEnvironment : TestHostEnvironment, IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath, string webRootPath)
            : base(contentRootPath)
        {
            WebRootPath = Path.GetFullPath(webRootPath);
        }

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
