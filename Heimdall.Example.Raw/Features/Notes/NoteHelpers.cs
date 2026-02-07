using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Heimdall.Example.Raw.Features.Notes
{
    public static class NoteHelpers
    {
        private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

        public static HtmlString RenderHeader()
        {
            var htmlContent = """
                <tr>
                  <th class="text-nowrap small text-muted">Created</th>
                  <th>Title</th>
                  <th>Body</th>
                  <th class="text-end text-nowrap">Actions</th>
                </tr>
            """;
            return new HtmlString(htmlContent);
        }

        public static HtmlString RenderRow(this Note note)
        {
            var id = Encoder.Encode(note.Id);
            var title = Encoder.Encode(note.Title ?? "");
            var body = Encoder.Encode(note.Body ?? "");
             
            var created = note.CreatedAt.ToLocalTime().ToString("g");
            var updated = note.UpdatedAt?.ToLocalTime().ToString("g");

            var meta = updated is null ? $"Created {Encoder.Encode(created)}" : $"Updated {Encoder.Encode(updated)}";
            var rowId = $"note-row-{id}";

            // Build JSON payload for ShowRemoveModal (expects noteID)
            // Use JsonSerializer so it's real JSON.
            var payloadObj = new { noteID = id };
            var payloadJson = JsonSerializer.Serialize(payloadObj);

            var htmlContent = $$$"""
                <tr id="{{{rowId}}}">
                  <td class="text-nowrap small text-muted">{{{created}}}</td>
                  <td class="fw-semibold">{{{title}}}</td>
                  <td class="text-muted">{{{body}}}</td>
                  <td class="text-end text-nowrap">
                    <button type="button" class="btn btn-sm btn-outline-danger"
                            heimdall-content-click="Home.ShowRemoveModal"
                            heimdall-content-target="#modalHost"
                            heimdall-content-swap="inner"
                            heimdall-payload='{{{payloadJson}}}'>
                      Delete
                    </button>
                  </td>
                </tr>
                """;
            return new HtmlString(htmlContent);
        }
    }
}
