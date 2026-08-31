using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void StaticHelpers_RenderOrderedMutationOperationsAndScopes()
    {
        var html = Render(Html.Fragment(
            HeimdallHtml.Mutate(
                "#order-panel",
                HeimdallHtml.MutateAttribute("aria-busy", "false"),
                HeimdallHtml.MutateAttribute("data-note", ""),
                HeimdallHtml.RemoveMutatedAttribute("hidden"),
                HeimdallHtml.RemoveMutatedClass("loading", "dimmed"),
                HeimdallHtml.AddMutatedClass("ready", "selected")),
            HeimdallHtml.Mutate(
                ".order",
                HeimdallHtml.MutationScope.Subtree,
                allTargets: true,
                HeimdallHtml.MutateAttribute("data-status", "complete")),
            HeimdallHtml.Mutate(
                "#list",
                HeimdallHtml.MutationScope.Matching(":scope > .row[data-live]"),
                allTargets: false,
                HeimdallHtml.AddMutatedClass("fresh"))));

        Assert.Equal(
            "<mutation heimdall-content-target=\"#order-panel\" scope=\"self\">" +
            "<mutation-attr name=\"aria-busy\" value=\"false\"></mutation-attr>" +
            "<mutation-attr name=\"data-note\" value=\"\"></mutation-attr>" +
            "<mutation-attr name=\"hidden\"></mutation-attr>" +
            "<mutation-class remove=\"loading dimmed\"></mutation-class>" +
            "<mutation-class add=\"ready selected\"></mutation-class>" +
            "</mutation>" +
            "<mutation heimdall-content-target=\".order\" scope=\"subtree\" all>" +
            "<mutation-attr name=\"data-status\" value=\"complete\"></mutation-attr>" +
            "</mutation>" +
            "<mutation heimdall-content-target=\"#list\" scope=\"select\" selector=\":scope &gt; .row[data-live]\">" +
            "<mutation-class add=\"fresh\"></mutation-class>" +
            "</mutation>",
            html);
    }

    [Fact]
    public void FluentHelpers_RenderMutationStateAndTypedAttributes()
    {
        var html = Render(FluentHtml.Fragment(fragment =>
        {
            fragment.Heimdall()
                .Mutate("#profile", mutation => mutation
                    .Set(Html.Disabled())
                    .State(new { Page = 2 })
                    .State("filter", new { Query = "<active>" })
                    .RemoveState("stale")
                    .AddClass("active", null, "", "current")
                    .RemoveClass("pending"))
                .MutateAll(".row", mutation => mutation.StateJson("selection", "{\"ids\":[1,2]}"),
                    HeimdallHtml.MutationScope.Matching(".selectable"));
        }));

        Assert.Contains("<mutation heimdall-content-target=\"#profile\" scope=\"self\">", html);
        Assert.Contains("<mutation-attr name=\"disabled\" value=\"\"></mutation-attr>", html);
        Assert.Contains("name=\"data-heimdall-state\" value=\"{&quot;Page&quot;:2}\"", html);
        Assert.Contains(
            "name=\"data-heimdall-state-filter\" value=\"{&quot;Query&quot;:&quot;\\u003Cactive\\u003E&quot;}\"",
            html);
        Assert.Contains("<mutation-attr name=\"data-heimdall-state-stale\"></mutation-attr>", html);
        Assert.Contains("<mutation-class add=\"active current\"></mutation-class>", html);
        Assert.Contains("<mutation-class remove=\"pending\"></mutation-class>", html);
        Assert.Contains(
            "<mutation heimdall-content-target=\".row\" scope=\"select\" selector=\".selectable\" all>",
            html);
        Assert.Contains(
            "name=\"data-heimdall-state-selection\" value=\"{&quot;ids&quot;:[1,2]}\"",
            html);
    }

    [Fact]
    public void MutationHelpers_ValidateRequiredArguments()
    {
        Assert.Throws<ArgumentException>(() => HeimdallHtml.Mutate(" "));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.MutateAttribute(" ", "value"));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.MutateAttribute("bad name", "value"));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.RemoveMutatedAttribute("bad=name"));
        Assert.Throws<ArgumentNullException>(() => HeimdallHtml.MutateAttribute("title", null!));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.RemoveMutatedAttribute(" "));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.MutationScope.Matching(" "));
        Assert.Throws<ArgumentNullException>(() => Render(FluentHtml.Fragment(fragment =>
            fragment.Heimdall().Mutate("#target", null!))));

        var emptyClasses = Render(Html.Fragment(
            HeimdallHtml.AddMutatedClass(null, " "),
            HeimdallHtml.RemoveMutatedClass()));
        Assert.Equal(string.Empty, emptyClasses);
    }
}
