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
		/// Represents a rendered HTML element with attributes and child content.
		/// </summary>
		private sealed class ElementNode : IHtmlContent
		{
			private readonly string _name;
			private readonly bool _isVoid;
			private readonly HtmlAttr[] _attrs;
			private readonly object?[] _children;

			private ElementNode(string name, bool isVoid, HtmlAttr[] attrs, object?[] children)
			{
				_name = name;
				_isVoid = isVoid;
				_attrs = attrs;
				_children = children;
			}

			/// <summary>
			/// Creates an element node by separating attributes from child content.
			/// </summary>
			/// <param name="name">The element name to render.</param>
			/// <param name="isVoid">Indicates whether the element is a void element.</param>
			/// <param name="parts">The raw parts supplied for element construction.</param>
			/// <returns>An <see cref="IHtmlContent"/> representing the constructed element.</returns>
			/// <remarks>
			/// Class attributes are merged, duplicate attributes are overwritten by name,
			/// and nested enumerables are flattened before processing.
			/// </remarks>
			public static IHtmlContent Create(string name, bool isVoid, object?[] parts)
			{
				var attrs = new PooledBuffer<HtmlAttr>();
				var children = new PooledBuffer<object?>();

				attrs.Init(initialCapacity: 8);
				children.Init(initialCapacity: 8);

				int classIndex = -1;

				try
				{
					foreach (var part in Flatten(parts ?? Array.Empty<object?>()))
					{
						if (part is HtmlAttr attr)
						{
							if (attr.IsEmpty)
								continue;

							if (attr.Kind == AttrKind.Class)
							{
								if (string.IsNullOrWhiteSpace(attr.Value))
									continue;

								if (classIndex < 0)
								{
									classIndex = attrs.Count;
									attrs.Add(attr);
								}
								else
								{
									var existing = attrs.Buffer[classIndex];
									attrs.Buffer[classIndex] = new HtmlAttr(
										"class",
										string.IsNullOrWhiteSpace(existing.Value)
											? attr.Value
											: $"{existing.Value} {attr.Value}",
										AttrKind.Class);
								}

								continue;
							}

							var replaced = false;
							for (int i = 0; i < attrs.Count; i++)
							{
								var existing = attrs.Buffer[i];
								if (existing.IsEmpty)
									continue;

								if (string.Equals(existing.Name, attr.Name, StringComparison.OrdinalIgnoreCase))
								{
									attrs.Buffer[i] = attr;
									replaced = true;
									break;
								}
							}

							if (!replaced)
								attrs.Add(attr);
						}
						else
						{
							children.Add(part);
						}
					}

					return new ElementNode(
						name,
						isVoid,
						attrs.ToArray(),
						children.ToArray());
				}
				finally
				{
					attrs.Dispose();
					children.Dispose();
				}
			}

			/// <summary>
			/// Writes the complete element, including attributes and children, to the supplied writer.
			/// </summary>
			/// <param name="writer">The writer that receives the rendered HTML.</param>
			/// <param name="encoder">The encoder used for strings and attribute values.</param>
			public void WriteTo(TextWriter writer, HtmlEncoder encoder)
			{
				writer.Write('<');
				writer.Write(_name);

				foreach (var attr in _attrs)
					attr.WriteTo(writer, encoder);

				if (_isVoid)
				{
					writer.Write(" />");
					return;
				}

				writer.Write('>');

				foreach (var child in _children)
					RenderPart(writer, encoder, child);

				writer.Write("</");
				writer.Write(_name);
				writer.Write('>');
			}
		}
	}
}
