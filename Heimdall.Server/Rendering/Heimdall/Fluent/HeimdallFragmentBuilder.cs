
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides fluent extension helpers for applying Heimdall attributes to FluentHtml builders.
	/// </summary>
	/// <remarks>
	/// This class offers an ergonomic wrapper over <see cref="HeimdallHtml"/> so Heimdall behaviors
	/// can be attached to elements and fragments using a fluent, strongly-typed API.
	/// </remarks>
	public static partial class FluentHeimdall
	{
		/// <summary>
		/// Represents a fluent wrapper that applies Heimdall content to a fragment builder.
		/// </summary>
		public readonly struct HeimdallFragmentBuilder
		{
			private readonly FluentHtml.FragmentBuilder _f;

			/// <summary>
			/// Initializes a new instance of the <see cref="HeimdallFragmentBuilder"/> struct.
			/// </summary>
			/// <param name="fragment">The fragment builder to wrap.</param>
			/// <exception cref="ArgumentNullException">Thrown when <paramref name="fragment"/> is <see langword="null"/>.</exception>
			public HeimdallFragmentBuilder(FluentHtml.FragmentBuilder fragment)
				=> _f = fragment ?? throw new ArgumentNullException(nameof(fragment));

			/// <summary>
			/// Adds an out-of-band invocation element to the fragment.
			/// </summary>
			/// <param name="targetSelector">The selector that should receive the invocation result.</param>
			/// <param name="swap">The swap mode used when applying the invocation payload.</param>
			/// <param name="payload">Optional content to include in the invocation.</param>
			/// <param name="wrapInTemplate">Determines whether the payload should be wrapped in a template element.</param>
			/// <returns>The current builder instance.</returns>
			public HeimdallFragmentBuilder Invocation(
				string targetSelector,
				HeimdallHtml.Swap swap = HeimdallHtml.Swap.Inner,
				IHtmlContent? payload = null,
				bool wrapInTemplate = false)
			{
				_f.Add(HeimdallHtml.Invocation(targetSelector, swap, payload, wrapInTemplate));
				return this;
			}

			/// <summary>Adds a single-root in-place mutation directive to the response fragment.</summary>
			public HeimdallFragmentBuilder Mutate(
				string targetSelector,
				Action<MutationBuilder> build,
				HeimdallHtml.MutationScope scope = default)
			{
				ArgumentNullException.ThrowIfNull(build);
				var mutation = new MutationBuilder();
				build(mutation);
				_f.Add(HeimdallHtml.Mutate(targetSelector, scope, allTargets: false, mutation.ToArray()));
				return this;
			}

			/// <summary>Adds an in-place mutation directive for every root matching the target selector.</summary>
			public HeimdallFragmentBuilder MutateAll(
				string targetSelector,
				Action<MutationBuilder> build,
				HeimdallHtml.MutationScope scope = default)
			{
				ArgumentNullException.ThrowIfNull(build);
				var mutation = new MutationBuilder();
				build(mutation);
				_f.Add(HeimdallHtml.Mutate(targetSelector, scope, allTargets: true, mutation.ToArray()));
				return this;
			}

			/// <summary>
			/// Adds an abort directive that suppresses the main target swap.
			/// </summary>
			/// <param name="reason">An optional reason surfaced in Heimdall abort events.</param>
			/// <returns>The current builder instance.</returns>
			public HeimdallFragmentBuilder Abort(string? reason = null)
			{
				_f.Add(HeimdallHtml.Abort(reason));
				return this;
			}

			/// <summary>
			/// Adds a redirect directive that navigates the browser to the supplied URL.
			/// </summary>
			/// <param name="url">The URL to navigate to.</param>
			/// <returns>The current builder instance.</returns>
			public HeimdallFragmentBuilder Redirect(string url)
			{
				_f.Add(HeimdallHtml.Redirect(url));
				return this;
			}

			/// <summary>Adds a browser history directive to the response fragment.</summary>
			public HeimdallFragmentBuilder History(HeimdallHtml.HistoryMode mode, string url)
			{
				_f.Add(HeimdallHtml.History(mode, url));
				return this;
			}

			/// <summary>Adds a new browser history entry for the supplied URL.</summary>
			public HeimdallFragmentBuilder HistoryPush(string url)
			{
				_f.Add(HeimdallHtml.HistoryPush(url));
				return this;
			}

			/// <summary>Replaces the browser's current history entry with the supplied URL.</summary>
			public HeimdallFragmentBuilder HistoryReplace(string url)
			{
				_f.Add(HeimdallHtml.HistoryReplace(url));
				return this;
			}

			/// <summary>
			/// Adds a JavaScript void invocation directive that runs after response swaps.
			/// </summary>
			/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.saved</c>.</param>
			/// <param name="args">Arguments passed to the JavaScript function.</param>
			/// <returns>The current builder instance.</returns>
			public HeimdallFragmentBuilder JsInvokeVoid(string functionPath, params object?[] args)
			{
				_f.Add(HeimdallHtml.JsInvokeVoid(functionPath, args));
				return this;
			}

			/// <summary>
			/// Adds a JavaScript void invocation directive that runs before response swaps.
			/// </summary>
			/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.prepare</c>.</param>
			/// <param name="args">Arguments passed to the JavaScript function.</param>
			/// <returns>The current builder instance.</returns>
			public HeimdallFragmentBuilder JsInvokeVoidBefore(string functionPath, params object?[] args)
			{
				_f.Add(HeimdallHtml.JsInvokeVoidBefore(functionPath, args));
				return this;
			}

			/// <summary>
			/// Adds a JavaScript void invocation directive that runs after response swaps.
			/// </summary>
			/// <param name="functionPath">An explicitly rooted function path such as <c>window.App.saved</c>.</param>
			/// <param name="args">Arguments passed to the JavaScript function.</param>
			/// <returns>The current builder instance.</returns>
			public HeimdallFragmentBuilder JsInvokeVoidAfter(string functionPath, params object?[] args)
			{
				_f.Add(HeimdallHtml.JsInvokeVoidAfter(functionPath, args));
				return this;
			}

			/// <summary>
			/// Adds HTML content directly to the fragment.
			/// </summary>
			/// <param name="content">The content to append.</param>
			/// <returns>The current builder instance.</returns>
			public HeimdallFragmentBuilder Add(IHtmlContent content)
			{
				_f.Add(content);
				return this;
			}
		}
	}
}
