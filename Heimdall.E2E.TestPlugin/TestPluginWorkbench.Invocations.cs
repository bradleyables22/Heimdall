using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Options;

namespace Heimdall.E2E.TestPlugin;

public sealed class TestPluginDocument
{
    public string Text { get; set; } = "";
    public string ClientInstance { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Label { get; set; } = "";
    public string Topic { get; set; } = "";
    public int DelayMs { get; set; }
}

public static partial class TestPluginWorkbench
{
    public sealed class Invocations(TestPluginRequestScope scope, IOptions<TestPluginOptions> options)
    {
        [ContentInvocation(Ids.Loaded)]
        public IHtmlContent Loaded(TestPluginDocument document)
            => FluentHtml.Fragment(fragment =>
            {
                fragment.Add(FluentHtml.Span(s => s.Text("Plugin ready")));
                fragment.Heimdall().Invocation("#plugin-loaded", payload: Result("plugin-loaded-content", document));
            });

        [ContentInvocation(Ids.Save)]
        public async Task<IHtmlContent> Save(TestPluginDocument document, CancellationToken cancellationToken)
        {
            await Task.Delay(Math.Clamp(document.DelayMs, 0, 500), cancellationToken);
            document.Text = document.Text.Trim();
            return FluentHtml.Fragment(fragment =>
            {
                fragment.Add(Result("plugin-saved-content", document));
                fragment.Heimdall().PluginCommand(Ids.SetText, new { document.Text }, "#plugin-editor");
            });
        }

        [ContentInvocation(Ids.Publish)]
        public async Task<IHtmlContent> Publish(TestPluginDocument document, Bifrost bifrost, CancellationToken cancellationToken)
        {
            if (!document.Topic.StartsWith("e2e-plugin-", StringComparison.Ordinal) || document.Topic.Length != 43)
                throw new ArgumentException("A workbench topic is required.");
            await bifrost.PublishAsync(document.Topic,
                HeimdallHtml.PluginCommand(Ids.SetText, new { Text = "SSE first", DelayMs = 40 }, "#plugin-editor"),
                TimeSpan.FromSeconds(10), cancellationToken);
            await bifrost.PublishAsync(document.Topic,
                HeimdallHtml.PluginCommand(Ids.SetText, new { Text = "SSE second" }, "#plugin-editor"),
                TimeSpan.FromSeconds(10), cancellationToken);
            return HtmlString.Empty;
        }

        private IHtmlContent Result(string id, TestPluginDocument document)
            => FluentHtml.Span(span => span.Id(id)
                .Data("service-label", options.Value.Label)
                .Data("request-scope", scope.Id)
                .Data("client-instance", document.ClientInstance)
                .Data("client-label", document.Label)
                .Data("mode", document.Mode)
                .Text(document.Text));
    }

    public static class StaticInvocations
    {
        [ContentInvocation(Ids.Replace)]
        public static IHtmlContent Replace()
            => FluentHtml.Fragment(fragment =>
            {
                fragment.Heimdall().PluginCommand(Ids.Record, new { Message = "before-replace" },
                    timing: HeimdallHtml.PluginCommandTiming.Before);
                fragment.Add(Editor("plugin-editor", "Replacement markup", interactive: true));
                fragment.Heimdall().PluginCommand(Ids.SetText, new { Text = "Replacement command applied" }, "#plugin-editor");
                fragment.Heimdall().PluginCommand(Ids.Record, new { Message = "after-replace" });
            });

        [ContentInvocation(Ids.Append)]
        public static IHtmlContent Append()
        {
            var id = Guid.NewGuid().ToString("N");
            return FluentHtml.Fragment(fragment => fragment
                .Add(Editor($"plugin-extra-{id}-a", "First appended editor"))
                .Add(Editor($"plugin-extra-{id}-b", "Second appended editor")));
        }

        [ContentInvocation(Ids.Clear)]
        public static IHtmlContent Clear() => HtmlString.Empty;

        [ContentInvocation(Ids.Mode)]
        public static IHtmlContent Mode()
            => FluentHtml.Fragment(fragment => fragment.Heimdall().Mutate("#plugin-editor",
                mutation => mutation.Attr("data-plugin-mode", "alternate")));

        [ContentInvocation(Ids.Abort)]
        public static IHtmlContent Abort()
            => FluentHtml.Fragment(fragment =>
            {
                fragment.Add(FluentHtml.Span(span => span.Id("plugin-should-not-render").Text("Suppressed main swap")));
                fragment.Heimdall().Abort("plugin-test-abort");
                fragment.Heimdall().PluginCommand(Ids.SetText, new { Text = "Abort command applied" }, "#plugin-editor");
            });

        [ContentInvocation(Ids.Failure)]
        public static IHtmlContent Failure()
            => FluentHtml.Fragment(fragment =>
            {
                fragment.Heimdall().PluginCommand(Ids.Fail);
                fragment.Heimdall().PluginCommand(Ids.Record, new { Message = "recovered-after-failure" });
            });
    }
}
