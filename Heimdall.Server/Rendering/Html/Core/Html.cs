
using Microsoft.AspNetCore.Html;
using System.Buffers;
using System.Collections;
using System.Text.Encodings.Web;


#pragma warning disable CS7022
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
		/// Represents the supported HTML input types.
		/// </summary>
		/// <remarks>
		/// Enum values are converted to their HTML string equivalents when rendered.
		/// Values that contain underscores are translated to hyphenated HTML attribute values.
		/// For example, <see cref="datetime_local"/> becomes <c>datetime-local</c>.
		/// </remarks>
		public enum InputType
		{
			/// <summary>
			/// Represents a standard button input type, which can be used to trigger actions or submit forms when clicked.
			/// </summary>
			button,
			/// <summary>
			///		Represents a checkbox input type, which allows users to select one or more options from a set. When rendered, it produces an HTML <c>input</c> element with the <c>type="checkbox"</c> attribute, enabling the checkbox functionality in web forms.
			/// </summary>
			checkbox,
			/// <summary>
			/// Represents a color input type, which provides a user interface for selecting a color. When rendered, it produces an HTML <c>input</c> element with the <c>type="color"</c> attribute, allowing users to choose a color from a color picker dialog in web forms.
			/// </summary>
			color,
			/// <summary>
			/// Represents a date input type, which allows users to select a date from a calendar interface. When rendered, it produces an HTML <c>input</c> element with the <c>type="date"</c> attribute, enabling date selection functionality in web forms.
			/// </summary>
			date,
			/// <summary>
			/// Represents a datetime-local input type, which allows users to select both a date and time without a time zone. When rendered, it produces an HTML <c>input</c> element with the <c>type="datetime-local"</c> attribute, providing a combined date and time picker interface in web forms.
			/// </summary>
			datetime_local,
			/// <summary>
			/// Represents an email input type, which allows users to enter and validate email addresses. When rendered, it produces an HTML <c>input</c> element with the <c>type="email"</c> attribute, enabling email address input and validation functionality in web forms.
			/// </summary>
			email,
			/// <summary>
			/// Represents a file input type, which allows users to select files from their device for upload. When rendered, it produces an HTML <c>input</c> element with the <c>type="file"</c> attribute, enabling file selection and upload functionality in web forms.
			/// </summary>
			file,
			/// <summary>
			/// Represents a hidden input type, which is used to store data that should not be visible or editable by the user. When rendered, it produces an HTML <c>input</c> element with the <c>type="hidden"</c> attribute, allowing developers to include hidden data in web forms that can be submitted to the server without being displayed in the user interface.
			/// </summary>
			hidden,
			/// <summary>
			/// Represents an image input type, which allows users to submit a form by clicking on an image. When rendered, it produces an HTML <c>input</c> element with the <c>type="image"</c> attribute, enabling the use of an image as a submit button in web forms.
			/// </summary>
			image,
			/// <summary>
			/// Represents a month input type, which allows users to select a month and year. When rendered, it produces an HTML <c>input</c> element with the <c>type="month"</c> attribute, providing a month and year picker interface in web forms.
			/// </summary>
			month,
			/// <summary>
			/// Represents a number input type, which allows users to enter numeric values. When rendered, it produces an HTML <c>input</c> element with the <c>type="number"</c> attribute, enabling numeric input and validation functionality in web forms.
			/// </summary>
			number,
			/// <summary>
			/// Represents a password input type, which allows users to enter sensitive information that should be obscured. When rendered, it produces an HTML <c>input</c> element with the <c>type="password"</c> attribute, causing the entered text to be masked (e.g., displayed as dots or asterisks) in web forms for enhanced security.
			/// </summary>
			password,
			/// <summary>
			/// Represents a radio input type, which allows users to select one option from a set of mutually exclusive options. When rendered, it produces an HTML <c>input</c> element with the <c>type="radio"</c> attribute, enabling radio button functionality in web forms where only one option can be selected at a time.
			/// </summary>
			radio,
			/// <summary>
			/// Represents a range input type, which allows users to select a value from a specified range using a slider interface. When rendered, it produces an HTML <c>input</c> element with the <c>type="range"</c> attribute, enabling the use of a slider control for selecting numeric values within defined minimum and maximum limits in web forms.
			/// </summary>
			range,
			/// <summary>
			/// Represents a reset input type, which creates a button that resets all form controls to their initial values when clicked. When rendered, it produces an HTML <c>input</c> element with the <c>type="reset"</c> attribute, providing a convenient way for users to clear or revert changes made in a form back to its original state.
			/// </summary>
			reset,
			/// <summary>
			/// Represents a search input type, which provides a user interface for entering search queries. When rendered, it produces an HTML <c>input</c> element with the <c>type="search"</c> attribute, enabling search-specific input behavior and styling in web forms.
			/// </summary>
			search,
			/// <summary>
			/// Represents a submit input type, which creates a button that submits the form data to the server when clicked. When rendered, it produces an HTML <c>input</c> element with the <c>type="submit"</c> attribute, allowing users to send form data for processing on the server side.
			/// </summary>
			submit,
			/// <summary>
			/// Represents a telephone input type, which allows users to enter and validate telephone numbers. When rendered, it produces an HTML <c>input</c> element with the <c>type="tel"</c> attribute, enabling telephone number input and validation functionality in web forms.
			/// </summary>
			tel,
			/// <summary>
			/// Represents a text input type, which allows users to enter single-line text. When rendered, it produces an HTML <c>input</c> element with the <c>type="text"</c> attribute, providing a basic text input field in web forms for general-purpose data entry.
			/// </summary>
			text,
			/// <summary>
			/// Represents a time input type, which allows users to select a time (without a date). When rendered, it produces an HTML <c>input</c> element with the <c>type="time"</c> attribute, providing a time picker interface in web forms for selecting hours and minutes.
			/// </summary>
			time,
			/// <summary>
			/// Represents a URL input type, which allows users to enter and validate URLs. When rendered, it produces an HTML <c>input</c> element with the <c>type="url"</c> attribute, enabling URL input and validation functionality in web forms.
			/// </summary>
			url,
			/// <summary>
			/// Represents a week input type, which allows users to select a specific week and year. When rendered, it produces an HTML <c>input</c> element with the <c>type="week"</c> attribute, providing a week and year picker interface in web forms for selecting a particular week of the year.
			/// </summary>
			week
		}

		/// <summary>
		/// Converts an <see cref="InputType"/> value to the corresponding HTML attribute string.
		/// </summary>
		/// <param name="type">The input type to convert.</param>
		/// <returns>The HTML-compatible string representation of the input type.</returns>
		/// <remarks>
		/// Underscores in enum names are translated to hyphens.
		/// </remarks>
		private static string ToInputTypeString(InputType type)
			=> type.ToString().Replace("_", "-");

		/// <summary>
		/// Represents a lightweight HTML attribute value used during element construction.
		/// </summary>
		/// <param name="Name">The attribute name.</param>
		/// <param name="Value">The attribute value.</param>
		/// <param name="Kind">The rendering behavior for the attribute.</param>
		public readonly record struct HtmlAttr(string Name, string Value, AttrKind Kind)
		{
			/// <summary>
			/// Represents an empty attribute value.
			/// </summary>
			public static readonly HtmlAttr Empty = new("", "", AttrKind.None);

			/// <summary>
			/// Gets a value indicating whether the attribute should be treated as empty.
			/// </summary>
			public bool IsEmpty => Kind == AttrKind.None || string.IsNullOrWhiteSpace(Name);

			/// <summary>
			/// Writes the attribute to the supplied output writer.
			/// </summary>
			/// <param name="writer">The writer that receives the rendered output.</param>
			/// <param name="encoder">The encoder used for attribute values.</param>
			/// <remarks>
			/// Boolean attributes are emitted by name only. Normal and class attributes
			/// are emitted as encoded name/value pairs.
			/// </remarks>
			internal void WriteTo(TextWriter writer, HtmlEncoder encoder)
			{
				if (IsEmpty)
					return;

				writer.Write(' ');
				writer.Write(Name);

				if (Kind == AttrKind.Boolean)
					return;

				writer.Write("=\"");
				encoder.Encode(writer, Value);
				writer.Write('"');
			}
		}

		/// <summary>
		/// Represents the rendering behavior of an HTML attribute.
		/// </summary>
		public enum AttrKind 
		{
			/// <summary>
			/// Indicates that the attribute should be ignored and not rendered. This is useful for conditionally omitting attributes based on certain conditions or for representing an empty attribute state.
			/// </summary>
			None,
			/// <summary>
			///		Indicates that the attribute should be rendered as a standard name/value pair, with the value HTML-encoded. This is the default behavior for most attributes, where the presence of the attribute and its associated value are both important for conveying information to the browser.
			/// </summary>
			Normal,
			/// <summary>
			///		Indicates that the attribute is a boolean attribute, which should be rendered by name only when its value is true. Boolean attributes are a special category of attributes in HTML where the presence of the attribute itself implies a true value, and the absence implies false. When rendering a boolean attribute, if its value is true, it is emitted as just the attribute name (e.g., <c>disabled</c>), without an explicit value. If the value is false, the attribute is not rendered at all.
			/// </summary>
			Boolean,
			/// <summary>
			/// Indicates that the attribute is a class attribute, which should be rendered by name and value, but with special handling to merge multiple class values together. When rendering a class attribute, if multiple class values are provided (e.g., from different sources), they should be combined into a single string with space-separated class names. This allows for more flexible and composable styling of elements, as different parts of the code can contribute class names without overwriting each other.
			/// </summary>
			Class
		}

		/// <summary>
		/// Creates a standard non-void HTML element.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="parts">
		/// The element parts to include. This may contain attributes, child content, strings,
		/// nested HTML content, or nested enumerables.
		/// </param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		/// <remarks>
		/// Use this method for elements that require both opening and closing tags,
		/// such as <c>div</c>, <c>span</c>, or <c>section</c>.
		/// </remarks>
		public static IHtmlContent Tag(string name, params object?[] parts)
			=> ElementNode.Create(name, isVoid: false, parts);

		/// <summary>
		/// Creates a void HTML element.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="parts">
		/// The element parts to include. This may contain attributes and other values,
		/// though child content is ignored for void tags.
		/// </param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		/// <remarks>
		/// Use this method for self-closing or void elements such as <c>input</c>, <c>img</c>, or <c>br</c>.
		/// </remarks>
		public static IHtmlContent VoidTag(string name, params object?[] parts)
			=> ElementNode.Create(name, isVoid: true, parts);

		/// <summary>
		/// Creates an HTML fragment that renders each supplied part in sequence.
		/// </summary>
		/// <param name="parts">The parts to render as a fragment.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the fragment.</returns>
		/// <remarks>
		/// This is useful when you need to return multiple sibling nodes without introducing
		/// an additional wrapper element into the output.
		/// </remarks>
		public static IHtmlContent Fragment(params object?[] parts)
			=> FragmentNode.Create(parts);

		/// <summary>
		/// Creates a text node that is HTML-encoded when rendered.
		/// </summary>
		/// <param name="text">The text content to render.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance containing encoded text output.</returns>
		/// <remarks>
		/// A <see langword="null"/> value is treated as an empty string.
		/// </remarks>
		public static IHtmlContent Text(string? text)
			=> new TextNode(text ?? string.Empty);

		/// <summary>
		/// Creates a raw HTML node that is written without HTML encoding.
		/// </summary>
		/// <param name="html">The raw HTML to emit.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance containing the unencoded HTML.</returns>
		/// <remarks>
		/// This method should only be used with trusted content.
		/// A <see langword="null"/> value is treated as an empty string.
		/// </remarks>
		public static IHtmlContent Raw(string? html)
			=> new HtmlString(html ?? string.Empty);

		/// <summary>
		/// Flattens a nested enumerable of parts into a simple sequence of renderable values.
		/// </summary>
		/// <param name="parts">The parts to flatten.</param>
		/// <returns>A flattened sequence of renderable values.</returns>
		/// <remarks>
		/// Strings and <see cref="IHtmlContent"/> values are preserved as single items.
		/// Other enumerable values are recursively expanded.
		/// </remarks>
		private static IEnumerable<object?> Flatten(IEnumerable parts)
		{
			foreach (var p in parts)
			{
				if (p is null)
					continue;

				if (p is IHtmlContent || p is string)
				{
					yield return p;
					continue;
				}

				if (p is IEnumerable seq)
				{
					foreach (var x in Flatten(seq))
						yield return x;
					continue;
				}

				yield return p;
			}
		}

		/// <summary>
		/// Provides a pooled, growable buffer for temporary value collection.
		/// </summary>
		/// <typeparam name="T">The type of item stored in the buffer.</typeparam>
		private struct PooledBuffer<T>
		{
			private T[]? _arr;
			private int _count;

			/// <summary>
			/// Gets the number of items currently stored in the buffer.
			/// </summary>
			public int Count => _count;

			/// <summary>
			/// Gets the underlying rented buffer.
			/// </summary>
			/// <exception cref="InvalidOperationException">
			/// Thrown when the buffer has not been initialized.
			/// </exception>
			internal T[] Buffer
				=> _arr ?? throw new InvalidOperationException("Buffer not initialized.");

			/// <summary>
			/// Initializes the buffer with an initial rented array.
			/// </summary>
			/// <param name="initialCapacity">The initial capacity to rent.</param>
			public void Init(int initialCapacity = 8)
			{
				_arr = ArrayPool<T>.Shared.Rent(initialCapacity);
				_count = 0;
			}

			/// <summary>
			/// Adds an item to the buffer, growing the backing storage when needed.
			/// </summary>
			/// <param name="item">The item to add.</param>
			public void Add(T item)
			{
				if (_arr is null)
					Init();

				if (_count == _arr!.Length)
				{
					var old = _arr!;
					_arr = ArrayPool<T>.Shared.Rent(old.Length * 2);
					Array.Copy(old, 0, _arr, 0, old.Length);
					ArrayPool<T>.Shared.Return(old, clearArray: true);
				}

				_arr![_count++] = item;
			}

			/// <summary>
			/// Copies the current buffer contents into a new array.
			/// </summary>
			/// <returns>An array containing the buffered items.</returns>
			public T[] ToArray()
			{
				if (_arr is null || _count == 0)
					return Array.Empty<T>();

				var result = new T[_count];
				Array.Copy(_arr, 0, result, 0, _count);
				return result;
			}

			/// <summary>
			/// Returns the rented buffer to the shared pool and resets the buffer state.
			/// </summary>
			public void Dispose()
			{
				if (_arr is not null)
				{
					ArrayPool<T>.Shared.Return(_arr, clearArray: true);
					_arr = null;
					_count = 0;
				}
			}
		}

		/// <summary>
		/// Renders a single content part to the supplied writer.
		/// </summary>
		/// <param name="writer">The writer that receives the rendered output.</param>
		/// <param name="encoder">The encoder used for string and object output.</param>
		/// <param name="part">The part to render.</param>
		/// <remarks>
		/// Attributes are ignored at this stage because they are handled during element rendering.
		/// Enumerable values are rendered recursively, and unknown objects are converted using
		/// <see cref="object.ToString"/>.
		/// </remarks>
		private static void RenderPart(TextWriter writer, HtmlEncoder encoder, object? part)
		{
			if (part is null)
				return;

			switch (part)
			{
				case HtmlAttr:
					return;

				case IHtmlContent html:
					html.WriteTo(writer, encoder);
					return;

				case string s:
					encoder.Encode(writer, s);
					return;

				case IEnumerable seq:
					foreach (var item in seq)
						RenderPart(writer, encoder, item);
					return;

				default:
					encoder.Encode(writer, part.ToString() ?? "");
					return;
			}
		}

	}
}
#pragma warning restore CS7022