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

		/// <summary>Creates a directive that updates the browser URL without navigating immediately.</summary>
		public static IHtmlContent History(HistoryMode mode, string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				throw new ArgumentException("History URL is required.", nameof(url));

			var modeValue = mode switch
			{
				HistoryMode.Push => "push",
				HistoryMode.Replace => "replace",
				_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported history mode.")
			};

			return Html.Tag("history", Html.Attr("mode", modeValue), Html.Attr("url", url));
		}

		/// <summary>Adds a new browser history entry for the supplied URL.</summary>
		public static IHtmlContent HistoryPush(string url) => History(HistoryMode.Push, url);

		/// <summary>Replaces the browser's current history entry with the supplied URL.</summary>
		public static IHtmlContent HistoryReplace(string url) => History(HistoryMode.Replace, url);
	}
}
