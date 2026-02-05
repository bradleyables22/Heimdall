using Heimdall.Example.Raw.Features.Notes;
using Heimdall.Example.Raw.Rendering.Layouts;
using Heimdall.Example.Raw.Rendering.Shared;
using Heimdall.Example.Raw.Utilities.Models;
using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using System.Text.Json;
namespace Heimdall.Example.Raw.Rendering.Home
{
	public static class Home
	{
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
                <div id=""modalHost""></div>

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

            if (notes?.Any()==false)
            {
                string statement = string.Empty;

                if (request.Offset == 0)
                    statement = "No data.";
                else
                    statement = "No more data";

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


        public static async Task<IHtmlContent> AddAsync(NoteService noteService, NoteIdRequest req)
        {
            var ok = await noteService.DeleteAsync(req.NoteID);
            var rowSelector = $"#note-row-{req.NoteID}";
            if (ok)
            {

                return new HtmlString($$"""
                    <template heimdall-oob="true"
                              heimdall-content-target="#modalHost"
                              heimdall-content-swap="inner"></template>

                    <template heimdall-oob="true"
                              heimdall-content-target="{{rowSelector}}"
                              heimdall-content-swap="outer"></template>
                    """);
            }

            return new HtmlString($$"""
                    <template heimdall-oob="true"
                              heimdall-content-target="#modal-body"
                              heimdall-content-swap="inner">
                        <p class="text-danger text-center">Failed to delete the note. Please try again later.</p>
                    </template>
                    """);
        }

        [ContentInvocation]
        public static async Task<IHtmlContent> RemoveAsync(NoteService noteService, NoteIdRequest req)
        {
            var ok = await noteService.DeleteAsync(req.NoteID);
            var rowSelector = $"#note-row-{req.NoteID}";
            if (ok)
            {

                return new HtmlString($$"""
                    <template heimdall-oob="true"
                              heimdall-content-target="#modalHost"
                              heimdall-content-swap="inner"></template>

                    <template heimdall-oob="true"
                              heimdall-content-target="{{rowSelector}}"
                              heimdall-content-swap="outer"></template>
                    """);
            }

            return new HtmlString($$"""
                    <template heimdall-oob="true"
                              heimdall-content-target="#modal-body"
                              heimdall-content-swap="inner">
                        <p class="text-danger text-center">Failed to delete the note. Please try again later.</p>
                    </template>
                    """);
        }

    }
}
