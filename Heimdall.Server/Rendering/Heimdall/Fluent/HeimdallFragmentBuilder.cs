
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
