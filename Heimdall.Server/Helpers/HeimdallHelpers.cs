using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Helpers
{
    internal static class HeimdallHelpers
    {
        internal static string RenderHtml(this IHtmlContent content)
        {
            using var sw = new StringWriter();
            content.WriteTo(sw, System.Text.Encodings.Web.HtmlEncoder.Default);
            return sw.ToString();
        }
        internal static string NormalizeWebRootPath(string path)
        {
            var p = path.Replace('\\', '/').TrimStart('/');

            if (p.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException("Path traversal is not allowed.");

            return p;
        }
    }
}
