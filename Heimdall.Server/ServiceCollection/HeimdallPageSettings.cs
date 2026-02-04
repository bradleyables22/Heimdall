
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
	public sealed class HeimdallPageSettings
	{
		public string Pattern { get; set; } = string.Empty;
		public string PagePath { get; set; } = string.Empty;
		public string LayoutPath { get; set; } = string.Empty;
        public string LayoutPlaceholder { get; set; } = string.Empty;
        public Dictionary<string, Func<IServiceProvider, HttpContext, IHtmlContent>> LayoutComponents { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
