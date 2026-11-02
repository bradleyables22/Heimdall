
using System.Text.Json;

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
		/// Writes a JSON state blob to the element.
		/// </summary>
		public static Html.HtmlAttr State(object? state, JsonSerializerOptions? options = null)
		{
			if (state is null)
				return Html.HtmlAttr.Empty;

			var json = JsonSerializer.Serialize(state, options);
			return Html.Attr(Attrs.DataState, json);
		}

		/// <summary>
		/// Writes a keyed JSON state blob to the element.
		/// </summary>
		public static Html.HtmlAttr State(string key, object? state, JsonSerializerOptions? options = null)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("State key is required.", nameof(key));

			if (state is null)
				return Html.HtmlAttr.Empty;

			var json = JsonSerializer.Serialize(state, options);
			return Html.Attr($"{Attrs.DataStatePrefix}{key.Trim()}", json);
		}

		/// <summary>
		/// Writes raw JSON state without serialization.
		/// </summary>
		public static Html.HtmlAttr StateJson(string json)
			=> Html.Attr(Attrs.DataState, json ?? "null");

		/// <summary>
		/// Writes raw keyed JSON state without serialization.
		/// </summary>
		public static Html.HtmlAttr StateJson(string key, string json)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("State key is required.", nameof(key));

			return Html.Attr($"{Attrs.DataStatePrefix}{key.Trim()}", json ?? "null");
		}
	}
}
