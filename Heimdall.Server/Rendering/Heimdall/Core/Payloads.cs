
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
		/// Serializes and attaches a JSON payload to the element.
		/// </summary>
		public static Html.HtmlAttr Payload(object? payload, JsonSerializerOptions? options = null)
		{
			if (payload is null)
				return Html.HtmlAttr.Empty;

			var json = JsonSerializer.Serialize(payload, options);
			return Html.Attr(Attrs.Payload, json);
		}

		/// <summary>
		/// Forces an empty JSON payload.
		/// </summary>
		public static Html.HtmlAttr PayloadEmptyObject()
			=> Html.Attr(Attrs.Payload, "{}");

		/// <summary>
		/// Defines the payload source directive.
		/// </summary>
		public static Html.HtmlAttr PayloadFromDirective(string from)
			=> Html.Attr(Attrs.PayloadFrom, from);

		/// <summary>
		/// References a global payload object.
		/// </summary>
		public static Html.HtmlAttr PayloadRef(string globalPath)
			=> Html.Attr(Attrs.PayloadRef, globalPath);

		/// <summary>
		/// Uses closest-state as the payload source.
		/// </summary>
		public static Html.HtmlAttr PayloadFromClosestState(string? key = null)
			=> Html.Attr(Attrs.PayloadFrom,
				key is null ? PayloadFrom.ClosestState : PayloadFrom.ClosestStateKey(key));

		/// <summary>
		/// Provides helpers for building payload source directives.
		/// </summary>
		public static class PayloadFrom
		{
			/// <summary>
			/// Indicates that the payload should be sourced from the closest form element.
			/// </summary>
			public const string ClosestForm = "closest-form";
			/// <summary>
			/// Indicates that the payload should be sourced from the element itself, without traversing up the DOM.
			/// </summary>
			public const string Self = "self";
			/// <summary>
			/// Indicates that the payload should be sourced from the closest state in the DOM hierarchy. Optionally, a key can be provided to specify which state value to use.
			/// </summary>
			public const string ClosestState = "closest-state";
			/// <summary>
			/// Constructs a payload source directive for closest-state with an optional key. If the key is null or whitespace, it defaults to "closest-state". Otherwise, it appends the trimmed key to "closest-state" with a colon separator.
			/// </summary>
			/// <param name="key"></param>
			/// <returns></returns>
			public static string ClosestStateKey(string key)
				=> string.IsNullOrWhiteSpace(key)
					? ClosestState
					: $"{ClosestState}:{key.Trim()}";
			/// <summary>
			/// Indicates that the payload should be sourced from a global JavaScript object, specified by the globalPath parameter. The globalPath should be a dot-separated path to the desired property on the window object (e.g., "MyApp.PayloadData"). This allows for sharing payload data across different components without relying on DOM traversal.
			/// </summary>
			/// <param name="globalPath"></param>
			/// <returns></returns>
			public static string Ref(string globalPath) => $"ref:{globalPath}";
		}


	}
}
