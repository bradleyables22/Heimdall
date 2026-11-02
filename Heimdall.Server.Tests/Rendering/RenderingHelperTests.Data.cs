using Heimdall.Server.Rendering;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void FluentDataHelpers_RenderFlatAndJsonValues()
    {
        var html = Render(FluentHtml.Button(button => button
            .Data(new
            {
                Id = 42,
                Status = "active",
                Enabled = true,
                Metadata = new { Theme = "dark" },
                Omitted = (string?)null
            })
            .DataJson("config", new { Theme = "dark", Page = 2 })
            .Text("Select")));

        Assert.Contains("data-id=\"42\"", html);
        Assert.Contains("data-status=\"active\"", html);
        Assert.Contains("data-enabled=\"true\"", html);
        Assert.Contains("data-metadata=\"{&quot;Theme&quot;:&quot;dark&quot;}\"", html);
        Assert.Contains("data-config=\"{&quot;Theme&quot;:&quot;dark&quot;,&quot;Page&quot;:2}\"", html);
        Assert.DoesNotContain("data-omitted=", html);
    }

    [Fact]
    public void DataHelpers_RejectNonObjectValuesAndMissingJsonKeys()
    {
        Assert.Throws<ArgumentException>(() => Html.Data(42));
        Assert.Throws<ArgumentException>(() => Html.DataJson(" ", new { value = 1 }));
    }
}
