using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides a lightweight HTML rendering core for building HTML content in code.
	/// </summary>
	/// <remarks>
	/// This type is designed to offer a small, predictable surface for constructing HTML nodes,
	/// composing fragments, generating attributes, and rendering encoded output by default.
	/// Raw HTML can still be emitted intentionally when needed.
	/// </remarks>
	public static partial class Html
	{
		/// <summary>
		/// Represents an HTML fragment that renders its parts in sequence.
		/// </summary>
		private sealed class FragmentNode : IHtmlContent
		{
			private readonly object?[] _parts;
			private FragmentNode(object?[] parts) => _parts = parts;

			/// <summary>
			/// Creates a new fragment node from the supplied parts.
			/// </summary>
			/// <param name="parts">The parts to include in the fragment.</param>
			/// <returns>An <see cref="IHtmlContent"/> representing the fragment.</returns>
			public static IHtmlContent Create(object?[] parts)
				=> new FragmentNode(parts ?? Array.Empty<object?>());

			/// <summary>
			/// Writes the fragment contents to the supplied writer.
			/// </summary>
			/// <param name="writer">The writer that receives the rendered HTML.</param>
			/// <param name="encoder">The encoder used for string output.</param>
			public void WriteTo(TextWriter writer, HtmlEncoder encoder)
			{
				foreach (var p in _parts)
					RenderPart(writer, encoder, p);
			}
		}

	}
}
