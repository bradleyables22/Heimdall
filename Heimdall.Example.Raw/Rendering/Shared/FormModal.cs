using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using Scriban;
using Scriban.Runtime;

namespace Heimdall.Example.Raw.Rendering.Shared
{
    public static class FormModal
    {
        public static IHtmlContent Render(IServiceProvider sp, FormModalModel model)
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();

            var templatePath = Path.Combine(
                env.WebRootPath,
                "components",
                "FormModal.Template.html"
            );

            if (!File.Exists(templatePath))
                return new HtmlString($"<!-- FormModal template missing: {templatePath} -->");

            var templateText = File.ReadAllText(templatePath);
            var template = Template.Parse(templateText);

            if (template.HasErrors)
            {
                return new HtmlString(
                    $"<!-- FormModal template parse error: {string.Join(", ", template.Messages.Select(m => m.Message))} -->"
                );
            }

            var fields = new List<ScriptObject>();
            foreach (var f in model.Fields)
            {
                var so = new ScriptObject
                {
                    { "id", f.Id },
                    { "name", f.Name },
                    { "label", f.Label },
                    { "type", f.Type ?? "text" },
                    { "value", f.Value ?? "" },
                    { "placeholder", f.Placeholder ?? "" },
                    { "hint", f.Hint ?? "" },
                    { "error", f.Error ?? "" },
                    { "required", f.Required },
                    { "rows", f.Rows }
                };
                fields.Add(so);
            }

            var scribanModel = new ScriptObject
            {
                { "modal_id", model.ModalId },
                { "form_id", model.FormId },
                { "title", model.Title },
                { "message", model.Message ?? "" },

                { "cancel_text", model.CancelText ?? "" },
                { "submit_text", model.SubmitText ?? "" },
                { "submit_button_class", model.SubmitButtonClass ?? "" },

                { "submit_action_id", model.SubmitActionId },
                { "close_action_id", model.CloseActionId ?? "FormModal.Close" },

                { "payload_json", model.PayloadJson ?? "" },
                { "disable_on_submit", model.DisableOnSubmit },

                { "modal_size", model.ModalSize ?? "" },

                { "error_html", model.ErrorHtml ?? "" },
                { "body_html", model.BodyHtml ?? "" },

                { "fields", fields }
            };

            var ctx = new TemplateContext();
            ctx.PushGlobal(scribanModel);

            var html = template.Render(ctx);
            return new HtmlString(html);
        }

        [ContentInvocation]
        public static IHtmlContent Close()
        {
            // Clears the modal outlet
            return new HtmlString(
                @"<invocation heimdall-content-target=""#modalHost"" heimdall-content-swap=""inner""></invocation>"
            );
        }
    }

    public sealed class FormModalModel
    {
        public string ModalId { get; set; } = "form-modal";
        public string FormId { get; set; } = "form-modal-form";

        public string Title { get; set; } = "Form";
        public string? Message { get; set; }

        public string SubmitActionId { get; set; } = default!;
        public string? CloseActionId { get; set; } = "FormModal.Close";

        public string? CancelText { get; set; }
        public string? SubmitText { get; set; }
        public string? SubmitButtonClass { get; set; }

        public string? PayloadJson { get; set; }
        public bool DisableOnSubmit { get; set; } = true;

        // Optional: "modal-lg", "modal-sm", "modal-xl"
        public string? ModalSize { get; set; }

        // Rendered above fields (danger alert)
        public string? ErrorHtml { get; set; }

        // Optional raw HTML region after fields
        public string? BodyHtml { get; set; }

        public List<FormFieldModel> Fields { get; set; } = new();
    }

    public sealed class FormFieldModel
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Label { get; set; } = default!;

        // "text", "textarea", "email", etc.
        public string? Type { get; set; } = "text";

        public string? Value { get; set; }
        public string? Placeholder { get; set; }
        public string? Hint { get; set; }

        public bool Required { get; set; }
        public int Rows { get; set; } = 4;

        // Field-level error
        public string? Error { get; set; }
    }
}
