using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

namespace Heimdall.Server.Helpers
{
    internal static class HeimdallHelpers
    {
		internal static string RenderHtml(this IHtmlContent content, HtmlEncoder? encoder = null)
			=> Rendering.HtmlContentExtensions.ToHtmlString(content, encoder);
		internal static string NormalizeWebRootPath(string path)
        {
            var p = path.Replace('\\', '/').TrimStart('/');

            if (p.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException("Path traversal is not allowed.");

            return p;
        }
    }
}
