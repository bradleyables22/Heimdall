using System.Diagnostics;
using System.Xml.Linq;

namespace Heimdall.Server.Tests;

public sealed class StaticSiteGenerationBuildTargetsTests
{
    [Fact]
    public void PackageIncludesStaticSiteGenerationBuildTargets()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "Heimdall.Server", "Heimdall.Server.csproj"));
        var include = project
            .Descendants("None")
            .SingleOrDefault(element =>
                string.Equals(
                    (string?)element.Attribute("Include"),
                    @"buildTransitive\HeimdallFramework.Server.targets",
                    StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(include);
        Assert.Equal("true", (string?)include.Attribute("Pack"));
        Assert.Equal(
            @"buildTransitive\HeimdallFramework.Server.targets",
            (string?)include.Attribute("PackagePath"));
    }

    [Fact]
    public void BuildTargetsAreOptInAndRunTheStaticGenerationCommand()
    {
        var root = FindRepositoryRoot();
        var targets = XDocument.Load(Path.Combine(
            root,
            "Heimdall.Server",
            "buildTransitive",
            "HeimdallFramework.Server.targets"));

        Assert.Contains(
            targets.Descendants("GenerateHeimdallStaticSiteOnBuild"),
            property => string.Equals(property.Value, "false", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            targets.Descendants("GenerateHeimdallStaticSiteOnPublish"),
            property => string.Equals(property.Value, "false", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            targets.Descendants("HeimdallStaticSiteGenerationDotNetCommand"),
            property => string.Equals(property.Value, "dotnet", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            targets.Descendants("HeimdallStaticSiteGenerationCommand"),
            property => string.Equals(property.Value, "--heimdall-generate-static", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            targets.Descendants("HeimdallStaticSiteGenerationNoBuildArgument"),
            property => string.Equals(property.Value, "--no-build", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            targets.Descendants("HeimdallStaticSiteGenerationNoLaunchProfileArgument"),
            property => string.Equals(property.Value, "--no-launch-profile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            targets.Descendants("HeimdallStaticSiteExcludeGeneratedStaticWebAssets"),
            property => string.Equals(property.Value, "true", StringComparison.OrdinalIgnoreCase));

        var runCommand = targets
            .Descendants("HeimdallStaticSiteGenerationRunCommand")
            .Single()
            .Value;

        Assert.Contains("$(HeimdallStaticSiteGenerationDotNetCommand)", runCommand);
        Assert.Contains("run", runCommand);
        Assert.Contains("$(HeimdallStaticSiteGenerationNoBuildArgument)", runCommand);
        Assert.Contains("$(HeimdallStaticSiteGenerationNoLaunchProfileArgument)", runCommand);
        Assert.Contains("$(HeimdallStaticSiteGenerationCommand)", runCommand);

        Assert.NotNull(targets
            .Descendants("Target")
            .SingleOrDefault(target =>
                string.Equals((string?)target.Attribute("Name"), "GenerateHeimdallStaticSiteOnBuild", StringComparison.Ordinal)));
        Assert.NotNull(targets
            .Descendants("Target")
            .SingleOrDefault(target =>
                string.Equals((string?)target.Attribute("Name"), "GenerateHeimdallStaticSiteOnPublish", StringComparison.Ordinal)));

        Assert.Contains(
            targets.Descendants("Content"),
            item => string.Equals(
                (string?)item.Attribute("Remove"),
                @"wwwroot\_content\**",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            targets.Descendants("None"),
            item => string.Equals(
                (string?)item.Attribute("Remove"),
                @"wwwroot\_content\**",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildTargetsAllowRepeatBuildsWhenStaticWebAssetsAreCopiedToWebRoot()
    {
        var root = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"heimdall-ssg-targets-{Guid.NewGuid():N}");
        var appRoot = Path.Combine(tempRoot, "RepeatBuildApp");

        try
        {
            Directory.CreateDirectory(appRoot);
            WriteRepeatBuildApp(root, appRoot);

            var projectPath = Path.Combine(appRoot, "RepeatBuildApp.csproj");
            RunDotNetBuild(projectPath, appRoot);
            RunDotNetBuild(projectPath, appRoot);

            Assert.True(File.Exists(Path.Combine(
                appRoot,
                "wwwroot",
                "_content",
                "HeimdallFramework.Web",
                "heimdall-bundle.min.js")));
            Assert.True(File.Exists(Path.Combine(appRoot, "wwwroot", "index.html")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Heimdall.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find Heimdall repository root.");
    }

    private static void WriteRepeatBuildApp(string root, string appRoot)
    {
        Directory.CreateDirectory(Path.Combine(appRoot, "wwwroot"));

        var serverProject = Path.Combine(root, "Heimdall.Server", "Heimdall.Server.csproj");
        var webProject = Path.Combine(root, "Heimdall.Web", "Heimdall.Web.csproj");
        var targetsProject = Path.Combine(
            root,
            "Heimdall.Server",
            "buildTransitive",
            "HeimdallFramework.Server.targets");

        File.WriteAllText(
            Path.Combine(appRoot, "RepeatBuildApp.csproj"),
            $$"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <GenerateHeimdallStaticSiteOnBuild>true</GenerateHeimdallStaticSiteOnBuild>
              </PropertyGroup>

              <ItemGroup>
                <ProjectReference Include="{{EscapeXmlAttribute(serverProject)}}" />
                <ProjectReference Include="{{EscapeXmlAttribute(webProject)}}" />
              </ItemGroup>

              <Import Project="{{EscapeXmlAttribute(targetsProject)}}" />
            </Project>
            """);

        File.WriteAllText(
            Path.Combine(appRoot, "Program.cs"),
            """
            using Heimdall.Server;
            using Heimdall.Server.Rendering;

            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.UseStaticWebAssets();

            builder.Services
                .AddHeimdallStaticSiteGeneration(options =>
                {
                    options.UseWebRootPath();
                    options.CleanOutputPath = true;
                    options.CopyStaticWebAssets = true;
                })
                .WithStaticPage("/", () => Html.Raw(
                    "<!doctype html><script src=\"/_content/HeimdallFramework.Web/heimdall-bundle.min.js\"></script>"));

            var app = builder.Build();

            app.MapStaticAssets();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            await app.RunWithHeimdallStaticSiteGenerationAsync(args);
            """);
    }

    private static void RunDotNetBuild(string projectPath, string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo.ArgumentList.Add("build");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet build timed out for '{projectPath}'.");
        }

        var output = stdout.GetAwaiter().GetResult();
        var errors = stderr.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet build failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                output +
                Environment.NewLine +
                errors);
        }
    }

    private static string EscapeXmlAttribute(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
