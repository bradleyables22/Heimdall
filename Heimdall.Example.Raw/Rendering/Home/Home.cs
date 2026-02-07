using Heimdall.Example.Raw.Features.Notes;
using Heimdall.Example.Raw.Rendering.Shared;
using Heimdall.Example.Raw.Utilities.Models;
using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Heimdall.Example.Raw.Rendering.Home
{
    public static class Home
    {
        private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

        [ContentInvocation]
        public static IHtmlContent RenderTable(NoteService noteService)
        {
            HtmlString header = NoteHelpers.RenderHeader();

            var initialPayload = new OffsetRequest { Offset = 0, Size = 10 };

            var loader = $@"
                <tr id=""notes-loader"">
                  <td colspan=""999"" class=""text-center text-muted py-3""
                      heimdall-content-visible=""Home.RenderRowsAsync""
                      heimdall-content-target=""#notes-loader""
                      heimdall-content-swap=""outer""
                      heimdall-visible-once=""true""
                      heimdall-payload='{JsonSerializer.Serialize(initialPayload)}'>
                      Loading…
                  </td>
                </tr>";

            return new HtmlString($@"
                <table class=""table table-hover"">
                    <thead>{header}</thead>
                    <tbody id=""notes-body"">
                        {loader}
                    </tbody>
                </table>");
        }

        [ContentInvocation]
        public static async Task<IHtmlContent> RenderRowsAsync(NoteService noteService, OffsetRequest request)
        {
            var notes = await noteService.GetPageAsync(request.Offset, request.Size);

            if (notes?.Any() == false)
            {
                var statement = request.Offset == 0 ? "No data." : "No more data";
                return new HtmlString(@$"<tr><td colspan=""999"" class=""text-center text-muted"">{statement}</td></tr>");
            }

            var next = new OffsetRequest
            {
                Offset = request.Offset + request.Size,
                Size = request.Size
            };

            var b = new HtmlContentBuilder();

            foreach (var note in notes)
                b.AppendHtml(NoteHelpers.RenderRow(note));

            b.AppendHtml($@"
                <tr id=""notes-loader"">
                  <td colspan=""999"" class=""text-center text-muted py-3""
                      heimdall-content-visible=""Home.RenderRowsAsync""
                      heimdall-content-target=""#notes-loader""
                      heimdall-content-swap=""outer""
                      heimdall-visible-once=""true""
                      heimdall-payload='{JsonSerializer.Serialize(next)}'>
                      Loading…
                  </td>
                </tr>");

            return b;
        }

        // ----------------------------
        // NEW NOTE (Form Modal)
        // ----------------------------

        [ContentInvocation]
        public static IHtmlContent ShowCreateModal(IServiceProvider sp)
        {
            var model = BuildCreateNoteModal(
                sp,
                req: new CreateNoteRequest(),
                fieldErrors: null,
                topErrorHtml: null
            );

            return FormModal.Render(sp, model);
        }

        [ContentInvocation]
        public static async Task<IHtmlContent> AddAsync(IServiceProvider sp, NoteService noteService, CreateNoteRequest req)
        {
            // Validate DataAnnotations (server-side)
            var results = new List<ValidationResult>();
            var ctx = new ValidationContext(req);
            var ok = Validator.TryValidateObject(req, ctx, results, validateAllProperties: true);

            // Map errors by property
            var errorsByField = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in results)
            {
                var member = r.MemberNames?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(member) && !errorsByField.ContainsKey(member))
                    errorsByField[member] = r.ErrorMessage ?? "Invalid value.";
            }

            if (!ok)
            {
                var modal = BuildCreateNoteModal(
                    sp,
                    req,
                    errorsByField,
                    topErrorHtml: "Please fix the highlighted fields."
                );

                // Re-render modal (stays open)
                return FormModal.Render(sp, modal);
            }

            var note = await noteService.CreateAsync(req.Title, req.Content);

            var rowHtml = NoteHelpers.RenderRow(note).Value ?? "";

            return new HtmlString($$"""
                {{FormModal.Close()}}

                <invocation
                    heimdall-content-target="#notes-body"
                    heimdall-content-swap="afterbegin">
                    <template>
                    {{rowHtml}}
                    </template>
                    
                </invocation>
                """);
        }

        private static FormModalModel BuildCreateNoteModal(
            IServiceProvider sp,
            CreateNoteRequest req,
            IReadOnlyDictionary<string, string>? fieldErrors,
            string? topErrorHtml)
        {
            string FieldErr(string name)
            {
                if (fieldErrors == null) 
                    return "";
                return fieldErrors.TryGetValue(name, out var e) ? Encoder.Encode(e) : "";
            }

            var model = new FormModalModel
            {
                ModalId = "create-note-modal",
                FormId = "create-note-form",
                Title = "New Note",
                Message = "Create a new note.",
                CancelText = "Cancel",
                SubmitText = "Create",
                SubmitButtonClass = "btn-success",

                SubmitActionId = "Home.AddAsync",
                CloseActionId = "FormModal.Close",

                DisableOnSubmit = true,
                ModalSize = "modal-lg",

                ErrorHtml = topErrorHtml
            };

            model.Fields.Add(new FormFieldModel
            {
                Id = "note-title",
                Name = nameof(CreateNoteRequest.Title),
                Label = "Title",
                Type = "text",
                Value = req.Title ?? "",
                Placeholder = "A short title…",
                Required = true,
                Error = FieldErr(nameof(CreateNoteRequest.Title))
            });

            model.Fields.Add(new FormFieldModel
            {
                Id = "note-content",
                Name = nameof(CreateNoteRequest.Content),
                Label = "Content",
                Type = "textarea",
                Value = req.Content ?? "",
                Placeholder = "Write something…",
                Rows = 5,
                Required = true,
                Error = FieldErr(nameof(CreateNoteRequest.Content))
            });

            return model;
        }

        [ContentInvocation]
        public static IHtmlContent ShowRemoveModal(IServiceProvider sp, NoteIdRequest req)
        {
            var noteID = req.NoteID;

            var model = new ConfirmModalModel
            {
                ModalId = "confirm-remove-modal",
                Title = "Delete note?",
                Message = "This cannot be undone. Are you sure you want to delete this note?",

                CancelText = "Cancel",
                ConfirmText = "Delete",
                ConfirmButtonClass = "btn-danger",

                ActionId = "Home.RemoveAsync",
                Target = $"#note-row-{noteID}",
                Swap = "outer",
                PayloadJson = JsonSerializer.Serialize(new NoteIdRequest { NoteID = noteID }),

                Disable = true
            };

            return ConfirmModal.Render(sp, model);
        }

        [ContentInvocation]
        public static async Task<IHtmlContent> RemoveAsync(NoteService noteService, NoteIdRequest req)
        {
            var ok = await noteService.DeleteAsync(req.NoteID);
            var rowSelector = $"#note-row-{req.NoteID}";

            if (ok)
            {
                return new HtmlString($$"""
                    {{ConfirmModal.Close()}}

                    <invocation
                        heimdall-content-target="{{rowSelector}}"
                        heimdall-content-swap="outer"></invocation>
                    """);
            }

            return new HtmlString($$"""
                    <invocation
                        heimdall-content-target="#modal-body"
                        heimdall-content-swap="inner">
                        <p class="text-danger text-center">Failed to delete the note. Please try again later.</p>
                    </invocation>
                    """);
        }
    }
}
