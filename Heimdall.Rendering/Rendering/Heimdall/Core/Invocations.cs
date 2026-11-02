using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides strongly-typed helpers for emitting Heimdall-compatible HTML attributes.
	/// </summary>
	/// <remarks>
	/// This class centralizes all Heimdall attribute names and provides safe, composable helpers
	/// for triggers, payload handling, swap behavior, and server-sent event configuration.
	/// </remarks>
	public static partial class HeimdallHtml
	{
		/// <summary>
		/// Creates an out-of-band invocation element.
		/// </summary>
		public static IHtmlContent Invocation(
			string targetSelector,
			Swap swap = Swap.Inner,
			IHtmlContent? payload = null,
			bool wrapInTemplate = false)
		{
			if (string.IsNullOrWhiteSpace(targetSelector))
				throw new ArgumentException("Invocation target selector is required.", nameof(targetSelector));

			var swapStr = SwapToString(swap);

			if (payload is null)
			{
				return Html.Tag("invocation",
					Html.Attr(Attrs.Target, targetSelector),
					Html.Attr(Attrs.Swap, swapStr));
			}

			if (!wrapInTemplate)
			{
				return Html.Tag("invocation",
					Html.Attr(Attrs.Target, targetSelector),
					Html.Attr(Attrs.Swap, swapStr),
					payload);
			}

			return Html.Tag("invocation",
				Html.Attr(Attrs.Target, targetSelector),
				Html.Attr(Attrs.Swap, swapStr),
				Html.Tag("template", payload));
		}
	}
}
