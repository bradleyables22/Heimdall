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
		/// Represents an encoded text node.
		/// </summary>
		private sealed class TextNode : IHtmlContent
		{
			private readonly string _text;
			public TextNode(string text) => _text = text;

			/// <summary>
			/// Writes the encoded text content to the supplied writer.
			/// </summary>
			/// <param name="writer">The writer that receives the rendered output.</param>
			/// <param name="encoder">The encoder used to safely encode the text.</param>
			public void WriteTo(TextWriter writer, HtmlEncoder encoder)
				=> encoder.Encode(writer, _text);
		}
	}
}
