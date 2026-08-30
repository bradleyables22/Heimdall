using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void HtmlHelpers_RenderNativeLanguageAttributeAcrossStaticAndFluentApis()
    {
        var coreHtml = Render(Html.Div(Html.Lang("fr-FR"), "Bonjour"));
        var fluentAttributeHtml = Render(Html.Div(FluentHtml.Lang("en-US"), "Hello"));
        var elementBuilderHtml = Render(FluentHtml.Div(div => div
            .Lang("de-DE")
            .Text("Hallo")));
        var encodedHtml = Render(Html.Div(Html.Lang("en-US\" data-bad=\"true"), "Safe"));

        Assert.Equal("<div lang=\"fr-FR\">Bonjour</div>", coreHtml);
        Assert.Equal("<div lang=\"en-US\">Hello</div>", fluentAttributeHtml);
        Assert.Equal("<div lang=\"de-DE\">Hallo</div>", elementBuilderHtml);
        Assert.Equal("<div lang=\"en-US&quot; data-bad=&quot;true\">Safe</div>", encodedHtml);
    }

    [Fact]
    public void StaticHelpers_RenderIgnoreAndScopeAttributes()
    {
        var html = Render(Html.Div(
            HeimdallHtml.Ignore(HeimdallHtml.Trigger.Click, HeimdallHtml.Trigger.Submit),
            HeimdallHtml.Scope(HeimdallHtml.EventScope.Self),
            HeimdallHtml.Sync(HeimdallHtml.RequestSync.QueueLatest),
            HeimdallHtml.SyncGroup("checkout"),
            "Save"));

        Assert.Contains("heimdall-ignore=\"click submit\"", html);
        Assert.Contains("heimdall-scope=\"self\"", html);
        Assert.Contains("heimdall-sync=\"queue-latest\"", html);
        Assert.Contains("heimdall-sync-group=\"checkout\"", html);
    }

    [Theory]
    [InlineData(HeimdallHtml.RequestSync.Parallel, "parallel")]
    [InlineData(HeimdallHtml.RequestSync.Replace, "replace")]
    [InlineData(HeimdallHtml.RequestSync.Drop, "drop")]
    [InlineData(HeimdallHtml.RequestSync.QueueLatest, "queue-latest")]
    public void StaticHelpers_RenderRequestSynchronizationStrategies(
        HeimdallHtml.RequestSync strategy,
        string expected)
    {
        var html = Render(Html.Button(HeimdallHtml.Sync(strategy), "Run"));

        Assert.Contains($"heimdall-sync=\"{expected}\"", html);
    }

    [Theory]
    [InlineData(Html.CommandType.toggle_popover, "toggle-popover")]
    [InlineData(Html.CommandType.show_popover, "show-popover")]
    [InlineData(Html.CommandType.hide_popover, "hide-popover")]
    [InlineData(Html.CommandType.close, "close")]
    [InlineData(Html.CommandType.request_close, "request-close")]
    [InlineData(Html.CommandType.show_modal, "show-modal")]
    public void HtmlHelpers_RenderNativeCommandAttributes(
        Html.CommandType command,
        string expected)
    {
        var html = Render(Html.Button(
            Html.CommandFor("confirmation-dialog"),
            Html.Command(command),
            "Run"));

        Assert.Contains("commandfor=\"confirmation-dialog\"", html);
        Assert.Contains($"command=\"{expected}\"", html);
    }

    [Fact]
    public void FluentHelpers_RenderCompleteFileUploadForm()
    {
        var html = Render(FluentHtml.Form(form =>
        {
            form.MultipartFormData();
            form.Heimdall()
                .OnSubmit("profile.save")
                .Target("#profile-result");

            form.Input(Html.InputType.file, input => input
                .Name("avatar")
                .Accept(".png", "image/jpeg")
                .Capture(Html.CaptureMode.environment)
                .Required()
                .Multiple());

            form.Button(button => button
                .Type("submit")
                .Text("Upload"));
        }));

        Assert.Equal(
            "<form enctype=\"multipart/form-data\" " +
            "heimdall-content-submit=\"profile.save\" " +
            "heimdall-content-target=\"#profile-result\">" +
            "<input type=\"file\" name=\"avatar\" accept=\".png,image/jpeg\" " +
            "capture=\"environment\" required multiple />" +
            "<button type=\"submit\">Upload</button>" +
            "</form>",
            html);
    }

    [Fact]
    public void StaticHelpers_RenderRawFileInputHints()
    {
        var html = Render(Html.Input(
            Html.InputType.file,
            Html.Accept(" image/png ", "", "image/jpeg"),
            Html.Capture("user")));

        Assert.Equal(
            "<input type=\"file\" accept=\"image/png,image/jpeg\" capture=\"user\" />",
            html);
    }

    [Fact]
    public void HtmlHelpers_RenderRawCustomCommandsAcrossStaticAndFluentApis()
    {
        var staticHtml = Render(Html.Button(
            FluentHtml.CommandFor("record-preview"),
            FluentHtml.Command("--archive-record"),
            "Archive"));
        var fluentHtml = Render(FluentHtml.Button(button =>
        {
            button.CommandFor("confirmation-dialog")
                .Command(Html.CommandType.request_close)
                .Text("Cancel");
        }));

        Assert.Contains("commandfor=\"record-preview\"", staticHtml);
        Assert.Contains("command=\"--archive-record\"", staticHtml);
        Assert.Contains("commandfor=\"confirmation-dialog\"", fluentHtml);
        Assert.Contains("command=\"request-close\"", fluentHtml);
    }
}
