using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.E2E.TestPlugin;

public static partial class TestPluginWorkbench
{
    public const string ModulePath = "/_content/Heimdall.E2E.TestPlugin/js/test-plugin.js";
    public const string StylesheetPath = "/_content/Heimdall.E2E.TestPlugin/css/test-plugin.css";

    public static class Ids
    {
        public const string Plugin = "e2e.test-plugin";
        public const string Commit = Plugin + ".commit";
        public const string Notify = Plugin + ".notify";
        public const string Document = Plugin + ".document";
        public const string SetText = Plugin + ".set-text";
        public const string Record = Plugin + ".record";
        public const string Fail = Plugin + ".fail";
        public const string Loaded = "e2e.plugin.loaded";
        public const string Save = "e2e.plugin.save";
        public const string Replace = "e2e.plugin.replace";
        public const string Append = "e2e.plugin.append";
        public const string Clear = "e2e.plugin.clear";
        public const string Mode = "e2e.plugin.mode";
        public const string Abort = "e2e.plugin.abort";
        public const string Failure = "e2e.plugin.failure";
        public const string Publish = "e2e.plugin.publish";
    }

    private static class Css
    {
        public const string Root = "plugin-workbench";
        public const string Controls = "plugin-controls";
        public const string Editor = "plugin-editor";
        public const string Status = "plugin-status";
    }

    public static IHtmlContent Render()
    {
        var topic = $"e2e-plugin-{Guid.NewGuid():N}";
        return FluentHtml.Main(root =>
        {
            root.Id("plugin-workbench").Class(Css.Root)
                .Data("plugin-topic", topic)
                .H1(h => h.Text("Frontend plugin workbench"))
                .P(p => p.Text("A separate RCL supplies the editor behavior, trigger, payload reader, response commands, server invocations, and assets. Press Ctrl+Enter in the editor to save."))
                .A(a => a.Href("/e2e").Text("Back to the core harness"))
                .H2(h => h.Text("Editor"))
                .Div(slot => slot.Id("plugin-editor-slot").Add(Editor("plugin-editor", "Initial library content", interactive: true)))
                .Div(status => status.Id("plugin-loaded").Class(Css.Status).Attr("role", "status").Text("Waiting for plugin initialization"))
                .Div(status => status.Id("plugin-save-result").Class(Css.Status).Attr("role", "status").Text("No saves yet"))
                .Div(controls =>
                {
                    controls.Class(Css.Controls)
                        .Add(Button("plugin-save", "Save with built-in click", Ids.Save, "#plugin-save-result", documentPayload: true))
                        .Button(button =>
                        {
                            button.Id("plugin-subscription").Type("button").Text("Save through a subscription");
                            button.Heimdall().OnPluginTrigger(Ids.Notify, Ids.Save)
                                .PayloadFromPlugin(Ids.Document).Target("#plugin-save-result").Disable(false);
                        })
                        .Add(Button("plugin-replace", "Replace editor", Ids.Replace, "#plugin-editor-slot"))
                        .Add(Button("plugin-mode", "Change editor options", Ids.Mode, "#plugin-editor-slot", swap: HeimdallHtml.Swap.None))
                        .Add(Button("plugin-abort", "Abort swap but run command", Ids.Abort, "#plugin-editor-slot"))
                        .Add(Button("plugin-failure", "Exercise command error isolation", Ids.Failure, "#plugin-editor-slot", swap: HeimdallHtml.Swap.None));
                })
                .H2(h => h.Text("Dynamic element lifecycle"))
                .Div(controls => controls.Class(Css.Controls)
                    .Add(Button("plugin-append", "Append two editors", Ids.Append, "#plugin-extra-slot", swap: HeimdallHtml.Swap.BeforeEnd))
                    .Add(Button("plugin-clear", "Remove appended editors", Ids.Clear, "#plugin-extra-slot")))
                .Div(slot => slot.Id("plugin-extra-slot"))
                .H2(h => h.Text("Bifrost commands"))
                .Div(host => host.Id("plugin-stream-host").Add(
                    HeimdallHtml.SseTopic(topic), HeimdallHtml.SseSwapMode(HeimdallHtml.Swap.None)))
                .Add(Button("plugin-publish", "Publish editor command", Ids.Publish, "#plugin-save-result", documentPayload: true, swap: HeimdallHtml.Swap.None))
                .H2(h => h.Text("Diagnostics"))
                .Div(d => d.Id("plugin-lifecycle").Data("mounted", "0").Data("disposed", "0").Data("updated", "0").Data("unsubscribed", "0").Text("Lifecycle events will appear here"))
                .Div(d => d.Id("plugin-errors").Data("count", "0").Attr("role", "status").Text("No plugin errors"))
                .Tag("ol", log => log.Id("plugin-command-log"));
        });
    }

    private static IHtmlContent Editor(string id, string text, bool interactive = false)
        => FluentHtml.TextArea(editor =>
        {
            editor.Id(id).Class(Css.Editor).Attr("data-e2e-plugin-editor", "true")
                .Data("plugin-mode", "default").Attr("aria-label", id == "plugin-editor" ? "Plugin editor" : "Additional plugin editor")
                .Text(text);
            if (!interactive) editor.Data("plugin-extra", "true");
            else editor.Heimdall().OnLoad(Ids.Loaded).OnPluginTrigger(Ids.Commit, Ids.Save)
                .PayloadFromPlugin(Ids.Document).Target("#plugin-save-result")
                .SyncReplace("plugin-save").Disable(false);
        });

    private static IHtmlContent Button(string id, string label, string invocation, string target,
        bool documentPayload = false, HeimdallHtml.Swap swap = HeimdallHtml.Swap.Inner)
        => FluentHtml.Button(button =>
        {
            button.Id(id).Type("button").Text(label);
            var behavior = button.Heimdall().OnClick(invocation).Target(target).Swap(swap).Disable(false);
            if (documentPayload) behavior.PayloadFromPlugin(Ids.Document);
            else behavior.PayloadEmptyObject();
        });
}
