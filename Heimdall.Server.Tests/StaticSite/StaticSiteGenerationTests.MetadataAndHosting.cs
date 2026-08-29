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
}
