using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

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

            var htmlContent = $"""
                <tr id="{id}-row">
                  <td class="text-nowrap small text-muted">{Encoder.Encode(created)}</td>
                  <td class="fw-semibold">{title}</td>
                  <td class="text-muted">{body}</td>
                  <td class="text-end text-nowrap">
                    <button class="btn btn-sm btn-outline-primary">
                      Edit
                    </button>

                    <button class="btn btn-sm btn-outline-danger">
                      Delete
                    </button>
                  </td>
                </tr>
            """;
            return new HtmlString(htmlContent);
        }
    }
}
