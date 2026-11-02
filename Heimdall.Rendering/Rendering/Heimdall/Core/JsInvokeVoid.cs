using Microsoft.AspNetCore.Html;
using System.Text.Json;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides strongly-typed helpers for emitting JavaScript void invocation directives.
	/// </summary>
	public static partial class HeimdallHtml
	{
		/// <summary>
		/// Creates a JavaScript void invocation directive that runs after response swaps.
		/// </summary>
		/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.saved</c>.</param>
		/// <param name="args">Arguments passed to the JavaScript function.</param>
		public static IHtmlContent JsInvokeVoid(string functionPath, params object?[] args)
			=> JsInvokeVoid(functionPath, JsInvokeTiming.After, args);

		/// <summary>
		/// Creates a JavaScript void invocation directive.
		/// </summary>
		/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.saved</c>.</param>
		/// <param name="timing">When the invocation should run relative to response swaps.</param>
		/// <param name="args">Arguments passed to the JavaScript function.</param>
		public static IHtmlContent JsInvokeVoid(
			string functionPath,
			JsInvokeTiming timing,
			params object?[] args)
			=> JsInvokeVoid(functionPath, timing, options: null, args: args);

		/// <summary>
		/// Creates a JavaScript void invocation directive that runs before response swaps.
		/// </summary>
		/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.prepare</c>.</param>
		/// <param name="args">Arguments passed to the JavaScript function.</param>
		public static IHtmlContent JsInvokeVoidBefore(string functionPath, params object?[] args)
			=> JsInvokeVoid(functionPath, JsInvokeTiming.Before, args);

		/// <summary>
		/// Creates a JavaScript void invocation directive that runs after response swaps.
		/// </summary>
		/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.saved</c>.</param>
		/// <param name="args">Arguments passed to the JavaScript function.</param>
		public static IHtmlContent JsInvokeVoidAfter(string functionPath, params object?[] args)
			=> JsInvokeVoid(functionPath, JsInvokeTiming.After, args);

		/// <summary>
		/// Creates a JavaScript void invocation directive with explicit JSON serialization options.
		/// </summary>
		/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.saved</c>.</param>
		/// <param name="timing">When the invocation should run relative to response swaps.</param>
		/// <param name="options">JSON serialization options used for the argument array.</param>
		/// <param name="args">Arguments passed to the JavaScript function.</param>
		public static IHtmlContent JsInvokeVoid(
			string functionPath,
			JsInvokeTiming timing,
			JsonSerializerOptions? options,
			params object?[] args)
		{
			var normalizedFunctionPath = NormalizeJsInvokeVoidFunctionPath(functionPath);
			var jsonArgs = JsonSerializer.Serialize(args ?? Array.Empty<object?>(), options);

			return Html.Tag("javascript",
				Html.Attr("function", normalizedFunctionPath),
				Html.Attr("args", jsonArgs),
				Html.Attr("timing", JsInvokeTimingToString(timing)));
		}

		private static string NormalizeJsInvokeVoidFunctionPath(string functionPath)
		{
			if (string.IsNullOrWhiteSpace(functionPath))
				throw new ArgumentException("JavaScript function path is required.", nameof(functionPath));

			var trimmed = functionPath.Trim();
			if (!HasAllowedJsInvokeVoidRoot(trimmed))
			{
				throw new ArgumentException(
					"JavaScript function path must start with 'window.', 'globalThis.', or 'document.'.",
					nameof(functionPath));
			}

			var segments = trimmed.Split('.');
			if (segments.Length < 2)
				throw new ArgumentException("JavaScript function path must include a function name.", nameof(functionPath));

			foreach (var segment in segments)
			{
				if (!IsValidJsPathSegment(segment))
				{
					throw new ArgumentException(
						"JavaScript function path must use dotted property access only.",
						nameof(functionPath));
				}
			}

			return trimmed;
		}

		private static bool HasAllowedJsInvokeVoidRoot(string functionPath)
			=> functionPath.StartsWith("window.", StringComparison.Ordinal) ||
				functionPath.StartsWith("globalThis.", StringComparison.Ordinal) ||
				functionPath.StartsWith("document.", StringComparison.Ordinal);

		private static bool IsValidJsPathSegment(string segment)
		{
			if (string.IsNullOrWhiteSpace(segment))
				return false;

			if (!IsJsIdentifierStart(segment[0]))
				return false;

			for (int i = 1; i < segment.Length; i++)
			{
				if (!IsJsIdentifierPart(segment[i]))
					return false;
			}

			return true;
		}

		private static bool IsJsIdentifierStart(char value)
			=> value is '_' or '$' ||
				(value >= 'A' && value <= 'Z') ||
				(value >= 'a' && value <= 'z');

		private static bool IsJsIdentifierPart(char value)
			=> IsJsIdentifierStart(value) ||
				(value >= '0' && value <= '9');

		private static string JsInvokeTimingToString(JsInvokeTiming timing)
			=> timing switch
			{
				JsInvokeTiming.Before => "before",
				JsInvokeTiming.After => "after",
				_ => throw new ArgumentOutOfRangeException(nameof(timing), timing, "Unsupported JavaScript invocation timing.")
			};
	}
}
