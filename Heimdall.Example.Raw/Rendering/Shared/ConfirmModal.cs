using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using Scriban;
using Scriban.Runtime;

namespace Heimdall.Example.Raw.Rendering.Shared
{
    public static class ConfirmModal
    {
        public static IHtmlContent Render(IServiceProvider sp, ConfirmModalModel model)
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();

            var templatePath = Path.Combine(
                env.WebRootPath,
                "components",
                "ConfirmModal.Template.html"
            );

            if (!File.Exists(templatePath))
                return new HtmlString($"<!-- ConfirmModal template missing: {templatePath} -->");

            var templateText = File.ReadAllText(templatePath);

            var template = Template.Parse(templateText);

            if (template.HasErrors)
            {
                return new HtmlString(
                    $"<!-- ConfirmModal template parse error: {string.Join(", ", template.Messages.Select(m => m.Message))} -->"
                );
            }

            var scribanModel = new ScriptObject
            {
                { "modal_id", model.ModalId },
                { "title", model.Title },
                { "message", model.Message },
                { "confirm_text", model.ConfirmText },
                { "cancel_text", model.CancelText },
                { "confirm_button_class", model.ConfirmButtonClass },
                { "action_id", model.ActionId },
                { "target", model.Target },
                { "swap", model.Swap },
                { "payload_json", model.PayloadJson },
                { "disable", model.Disable },
                { "visible_once", model.VisibleOnce },
                { "error_html", model.ErrorHtml }
            };

            var context = new TemplateContext();
            context.PushGlobal(scribanModel);

            var html = template.Render(context);

            return new HtmlString(html);
        }
        [ContentInvocation]
        public static IHtmlContent Close()
        {
            return new HtmlString(@"<invocation heimdall-content-target=""#modalHost"" heimdall-content-swap=""inner""></invocation>");
        }

    }

    public sealed class ConfirmModalModel
    {
        public string ModalId { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Message { get; set; } = default!;

        public string? ConfirmText { get; set; }
        public string? CancelText { get; set; }
        public string? ConfirmButtonClass { get; set; }

        public string ActionId { get; set; } = default!;
        public string Target { get; set; } = default!;
        public string? Swap { get; set; }
        public string? PayloadJson { get; set; }
        public bool Disable { get; set; }
        public bool VisibleOnce { get; set; }
        public string? ErrorHtml { get; set; }
    }
}
