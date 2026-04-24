
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

		private static string SwapToString(Swap swap) => swap switch
		{
			Swap.Inner => "inner",
			Swap.Outer => "outer",
			Swap.BeforeEnd => "beforeend",
			Swap.AfterBegin => "afterbegin",
			Swap.None => "none",
			_ => "inner"
		};
		/// <summary>
		/// Defines how returned content should be applied to the DOM.
		/// </summary>
		public static Html.HtmlAttr SwapMode(Swap swap) => Html.Attr(Attrs.Swap, SwapToString(swap));
	}
}
