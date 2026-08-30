
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
		/// Creates a Heimdall wrapper for an element builder.
		/// </summary>
		/// <param name="b">The element builder to wrap.</param>
		/// <returns>A fluent Heimdall builder for the provided element.</returns>
		public static HeimdallBuilder Heimdall(this FluentHtml.ElementBuilder b)
			=> new(b);

		/// <summary>
		/// Configures Heimdall behavior for an element and returns the original HTML builder.
		/// </summary>
		/// <param name="b">The element builder to configure.</param>
		/// <param name="build">The callback that applies Heimdall behavior.</param>
		/// <returns>The original element builder so HTML configuration can continue.</returns>
		public static FluentHtml.ElementBuilder Heimdall(
			this FluentHtml.ElementBuilder b,
			Action<HeimdallBuilder> build)
		{
			ArgumentNullException.ThrowIfNull(b);
			ArgumentNullException.ThrowIfNull(build);
			build(new HeimdallBuilder(b));
			return b;
		}

		/// <summary>
		/// Creates a Heimdall wrapper for a fragment builder.
		/// </summary>
		/// <param name="f">The fragment builder to wrap.</param>
		/// <returns>A fluent Heimdall fragment builder for the provided fragment.</returns>
		public static HeimdallFragmentBuilder Heimdall(this FluentHtml.FragmentBuilder f)
			=> new(f);

		/// <summary>
		/// Configures Heimdall directives for a fragment and returns the original HTML builder.
		/// </summary>
		/// <param name="f">The fragment builder to configure.</param>
		/// <param name="build">The callback that adds Heimdall directives.</param>
		/// <returns>The original fragment builder so HTML composition can continue.</returns>
		public static FluentHtml.FragmentBuilder Heimdall(
			this FluentHtml.FragmentBuilder f,
			Action<HeimdallFragmentBuilder> build)
		{
			ArgumentNullException.ThrowIfNull(f);
			ArgumentNullException.ThrowIfNull(build);
			build(new HeimdallFragmentBuilder(f));
			return f;
		}
	}
}
