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
}
