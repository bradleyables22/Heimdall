
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
		/// Creates a Heimdall wrapper for a fragment builder.
		/// </summary>
		/// <param name="f">The fragment builder to wrap.</param>
		/// <returns>A fluent Heimdall fragment builder for the provided fragment.</returns>
		public static HeimdallFragmentBuilder Heimdall(this FluentHtml.FragmentBuilder f)
			=> new(f);
	}
}
