using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides strongly-typed helpers for emitting Heimdall response directive elements.
	/// </summary>
	public static partial class HeimdallHtml
	{
		/// <summary>
		/// Creates an abort directive that suppresses the main target swap.
		/// </summary>
		public static IHtmlContent Abort(string? reason = null)
			=> string.IsNullOrWhiteSpace(reason)
				? Html.Tag("abort")
				: Html.Tag("abort", Html.Attr("reason", reason));

		/// <summary>
		/// Creates a redirect directive that navigates the browser to the supplied URL.
		/// </summary>
		public static IHtmlContent Redirect(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				throw new ArgumentException("Redirect URL is required.", nameof(url));

			return Html.Tag("redirect", Html.Attr("url", url));
		}
	}
}
