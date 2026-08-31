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
}
