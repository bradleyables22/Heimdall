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
}
