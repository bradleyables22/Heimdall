using Heimdall.Server.Rendering;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void PluginHelpers_RenderNamespacedTriggersPayloadsAndEncodedCommands()
    {
        var html = Render(FluentHtml.Fragment(fragment =>
        {
            fragment.Add(FluentHtml.Button(button => button.Heimdall()
                .OnPluginTrigger("company.editor.commit", "documents.save")
                .PayloadFromPlugin("company.editor.document")));
            fragment.Heimdall().PluginCommand("company.editor.set-text", new { Text = "<unsafe>&\"" }, "#editor");
        }));

        Assert.Contains("heimdall-content-company-editor-commit=\"documents.save\"", html);
        Assert.Contains("heimdall-payload-from=\"plugin:company.editor.document\"", html);
        Assert.Contains("<heimdall-command", html);
        Assert.Contains("name=\"company.editor.set-text\"", html);
        Assert.Contains("&quot;text&quot;", html);
        Assert.Contains("target=\"#editor\"", html);
        Assert.Contains("timing=\"after\"", html);
        Assert.DoesNotContain("<unsafe>", html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unqualified")]
    [InlineData("Company.invalid")]
    [InlineData("company.invalid name")]
    public void PluginHelpers_RejectInvalidNames(string name)
        => Assert.Throws<ArgumentException>(() => HeimdallHtml.PluginCommand(name));

    [Fact]
    public void PluginHelpers_RequireATargetForMultipleTargetCommands()
        => Assert.Throws<ArgumentException>(() => HeimdallHtml.PluginCommand("company.editor.clear", allTargets: true));
}
