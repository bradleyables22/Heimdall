using Microsoft.AspNetCore.Html;
using System.Buffers;
using System.Collections;
using System.Text;
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
	public static class Html
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
			button,
			checkbox,
			color,
			date,
			datetime_local,
			email,
			file,
			hidden,
			image,
			month,
			number,
			password,
			radio,
			range,
			reset,
			search,
			submit,
			tel,
			text,
			time,
			url,
			week
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
		/// Creates a standard HTML attribute using the format <c>name="value"</c>.
		/// </summary>
		/// <param name="name">The attribute name.</param>
		/// <param name="value">The attribute value.</param>
		/// <returns>
		/// A populated <see cref="HtmlAttr"/> when the name is valid; otherwise <see cref="HtmlAttr.Empty"/>.
		/// </returns>
		/// <remarks>
		/// Attribute values are HTML-encoded when written to the response.
		/// </remarks>
		public static HtmlAttr Attr(string name, string? value)
			=> string.IsNullOrWhiteSpace(name)
				? HtmlAttr.Empty
				: new HtmlAttr(name, value ?? string.Empty, AttrKind.Normal);

		/// <summary>
		/// Creates a boolean HTML attribute that is rendered by presence alone.
		/// </summary>
		/// <param name="name">The attribute name.</param>
		/// <param name="on">
		/// <see langword="true"/> to include the attribute; otherwise, the attribute is omitted.
		/// </param>
		/// <returns>
		/// A populated <see cref="HtmlAttr"/> when enabled and valid; otherwise <see cref="HtmlAttr.Empty"/>.
		/// </returns>
		/// <remarks>
		/// Boolean attributes are emitted as a name only, without an explicit value.
		/// </remarks>
		public static HtmlAttr Bool(string name, bool on)
			=> on && !string.IsNullOrWhiteSpace(name)
				? new HtmlAttr(name, name, AttrKind.Boolean)
				: HtmlAttr.Empty;

		/// <summary>
		/// Creates a <c>class</c> attribute from one or more CSS class names.
		/// </summary>
		/// <param name="classes">The class values to combine.</param>
		/// <returns>A <see cref="HtmlAttr"/> representing the merged class attribute.</returns>
		/// <remarks>
		/// Empty and whitespace-only values are ignored. Remaining values are trimmed and joined
		/// using a single space.
		/// </remarks>
		public static HtmlAttr Class(params string?[] classes)
			=> new HtmlAttr("class", CssJoin(classes), AttrKind.Class);

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
		/// Creates an <c>id</c> attribute.
		/// </summary>
		public static HtmlAttr Id(string id) => Attr("id", id);

		/// <summary>
		/// Creates an <c>href</c> attribute.
		/// </summary>
		public static HtmlAttr Href(string href) => Attr("href", href);

		/// <summary>
		/// Creates a <c>src</c> attribute.
		/// </summary>
		public static HtmlAttr Src(string src) => Attr("src", src);

		/// <summary>
		/// Creates an <c>alt</c> attribute.
		/// </summary>
		public static HtmlAttr Alt(string alt) => Attr("alt", alt);

		/// <summary>
		/// Creates a <c>type</c> attribute from a raw string value.
		/// </summary>
		public static HtmlAttr Type(string type) => Attr("type", type);

		/// <summary>
		/// Creates a <c>type</c> attribute from a strongly typed <see cref="InputType"/> value.
		/// </summary>
		public static HtmlAttr Type(InputType type) => Attr("type", ToInputTypeString(type));

		/// <summary>
		/// Creates a <c>name</c> attribute.
		/// </summary>
		public static HtmlAttr Name(string name) => Attr("name", name);

		/// <summary>
		/// Creates a <c>value</c> attribute.
		/// </summary>
		public static HtmlAttr Value(string value) => Attr("value", value);

		/// <summary>
		/// Creates a <c>role</c> attribute.
		/// </summary>
		public static HtmlAttr Role(string role) => Attr("role", role);

		/// <summary>
		/// Creates a <c>style</c> attribute.
		/// </summary>
		public static HtmlAttr Style(string css) => Attr("style", css);

		/// <summary>
		/// Creates a <c>content</c> attribute.
		/// </summary>
		public static HtmlAttr Content(string value) => Attr("content", value);

		/// <summary>
		/// Creates a <c>for</c> attribute.
		/// </summary>
		public static HtmlAttr For(string value) => Attr("for", value);

		/// <summary>
		/// Creates a <c>title</c> attribute.
		/// </summary>
		/// <param name="value">The title value to assign.</param>
		/// <returns>A <see cref="HtmlAttr"/> representing the title attribute.</returns>
		/// <remarks>
		/// This is commonly used for browser tooltips and accessibility-related labeling.
		/// </remarks>
		public static HtmlAttr TitleAttr(string value) => Attr("title", value);

		/// <summary>
		/// Creates a <c>data-*</c> attribute.
		/// </summary>
		/// <param name="key">The custom data key.</param>
		/// <param name="value">The value to assign.</param>
		/// <returns>A <see cref="HtmlAttr"/> representing the generated data attribute.</returns>
		public static HtmlAttr Data(string key, string value) => Attr($"data-{key}", value);

		/// <summary>
		/// Creates an <c>aria-*</c> attribute.
		/// </summary>
		/// <param name="key">The ARIA key.</param>
		/// <param name="value">The value to assign.</param>
		/// <returns>A <see cref="HtmlAttr"/> representing the generated ARIA attribute.</returns>
		public static HtmlAttr Aria(string key, string value) => Attr($"aria-{key}", value);

		/// <summary>
		/// Creates a <c>placeholder</c> attribute.
		/// </summary>
		public static HtmlAttr Placeholder(string value) => Attr("placeholder", value);

		/// <summary>
		/// Creates an <c>autocomplete</c> attribute.
		/// </summary>
		public static HtmlAttr AutoComplete(string value) => Attr("autocomplete", value);

		/// <summary>
		/// Creates a <c>min</c> attribute.
		/// </summary>
		public static HtmlAttr Min(string value) => Attr("min", value);

		/// <summary>
		/// Creates a <c>max</c> attribute.
		/// </summary>
		public static HtmlAttr Max(string value) => Attr("max", value);

		/// <summary>
		/// Creates a <c>step</c> attribute.
		/// </summary>
		public static HtmlAttr Step(string value) => Attr("step", value);

		/// <summary>
		/// Creates a <c>pattern</c> attribute.
		/// </summary>
		public static HtmlAttr Pattern(string value) => Attr("pattern", value);

		/// <summary>
		/// Creates a <c>maxlength</c> attribute.
		/// </summary>
		public static HtmlAttr MaxLength(int value) => Attr("maxlength", value.ToString());

		/// <summary>
		/// Creates a <c>minlength</c> attribute.
		/// </summary>
		public static HtmlAttr MinLength(int value) => Attr("minlength", value.ToString());

		/// <summary>
		/// Creates a <c>rows</c> attribute.
		/// </summary>
		public static HtmlAttr Rows(int value) => Attr("rows", value.ToString());

		/// <summary>
		/// Creates a <c>cols</c> attribute.
		/// </summary>
		public static HtmlAttr Cols(int value) => Attr("cols", value.ToString());

		/// <summary>
		/// Creates an <c>action</c> attribute.
		/// </summary>
		public static HtmlAttr Action(string value) => Attr("action", value);

		/// <summary>
		/// Creates a <c>method</c> attribute.
		/// </summary>
		public static HtmlAttr Method(string value) => Attr("method", value);

		/// <summary>
		/// Creates an <c>enctype</c> attribute.
		/// </summary>
		public static HtmlAttr EncType(string value) => Attr("enctype", value);

		/// <summary>
		/// Creates a <c>rel</c> attribute.
		/// </summary>
		public static HtmlAttr Rel(string value) => Attr("rel", value);

		/// <summary>
		/// Creates a <c>target</c> attribute.
		/// </summary>
		public static HtmlAttr Target(string value) => Attr("target", value);

		/// <summary>
		/// Creates a <c>disabled</c> boolean attribute.
		/// </summary>
		public static HtmlAttr Disabled(bool on = true) => Bool("disabled", on);

		/// <summary>
		/// Creates a <c>checked</c> boolean attribute.
		/// </summary>
		public static HtmlAttr Checked(bool on = true) => Bool("checked", on);

		/// <summary>
		/// Creates a <c>selected</c> boolean attribute.
		/// </summary>
		public static HtmlAttr Selected(bool on = true) => Bool("selected", on);

		/// <summary>
		/// Creates a <c>readonly</c> boolean attribute.
		/// </summary>
		public static HtmlAttr ReadOnly(bool on = true) => Bool("readonly", on);

		/// <summary>
		/// Creates a <c>required</c> boolean attribute.
		/// </summary>
		public static HtmlAttr Required(bool on = true) => Bool("required", on);

		/// <summary>
		/// Creates a <c>multiple</c> boolean attribute.
		/// </summary>
		public static HtmlAttr Multiple(bool on = true) => Bool("multiple", on);

		/// <summary>
		/// Creates an <c>autofocus</c> boolean attribute.
		/// </summary>
		public static HtmlAttr AutoFocus(bool on = true) => Bool("autofocus", on);

		/// <summary>
		/// Creates an <c>html</c> element.
		/// </summary>
		public static IHtmlContent HtmlTag(params object?[] c) => Tag("html", c);

		/// <summary>
		/// Creates a <c>head</c> element.
		/// </summary>
		public static IHtmlContent Head(params object?[] c) => Tag("head", c);

		/// <summary>
		/// Creates a <c>body</c> element.
		/// </summary>
		public static IHtmlContent Body(params object?[] c) => Tag("body", c);

		/// <summary>
		/// Creates a <c>header</c> element.
		/// </summary>
		public static IHtmlContent Header(params object?[] c) => Tag("header", c);

		/// <summary>
		/// Creates a <c>main</c> element.
		/// </summary>
		public static IHtmlContent Main(params object?[] c) => Tag("main", c);

		/// <summary>
		/// Creates a <c>section</c> element.
		/// </summary>
		public static IHtmlContent Section(params object?[] c) => Tag("section", c);

		/// <summary>
		/// Creates an <c>article</c> element.
		/// </summary>
		public static IHtmlContent Article(params object?[] c) => Tag("article", c);

		/// <summary>
		/// Creates an <c>aside</c> element.
		/// </summary>
		public static IHtmlContent Aside(params object?[] c) => Tag("aside", c);

		/// <summary>
		/// Creates a <c>footer</c> element.
		/// </summary>
		public static IHtmlContent Footer(params object?[] c) => Tag("footer", c);

		/// <summary>
		/// Creates a <c>nav</c> element.
		/// </summary>
		public static IHtmlContent Nav(params object?[] c) => Tag("nav", c);

		/// <summary>
		/// Creates a <c>title</c> element.
		/// </summary>
		public static IHtmlContent Title(params object?[] c) => Tag("title", c);

		/// <summary>
		/// Creates a <c>meta</c> element.
		/// </summary>
		public static IHtmlContent Meta(params object?[] c) => VoidTag("meta", c);

		/// <summary>
		/// Creates a <c>link</c> element.
		/// </summary>
		public static IHtmlContent Link(params object?[] c) => VoidTag("link", c);

		/// <summary>
		/// Creates a <c>script</c> element.
		/// </summary>
		public static IHtmlContent Script(params object?[] c) => Tag("script", c);

		/// <summary>
		/// Creates a <c>noscript</c> element.
		/// </summary>
		public static IHtmlContent NoScript(params object?[] c) => Tag("noscript", c);

		/// <summary>
		/// Creates a <c>div</c> element.
		/// </summary>
		public static IHtmlContent Div(params object?[] c) => Tag("div", c);

		/// <summary>
		/// Creates a <c>span</c> element.
		/// </summary>
		public static IHtmlContent Span(params object?[] c) => Tag("span", c);

		/// <summary>
		/// Creates a <c>strong</c> element.
		/// </summary>
		public static IHtmlContent Strong(params object?[] c) => Tag("strong", c);

		/// <summary>
		/// Creates a <c>p</c> element.
		/// </summary>
		public static IHtmlContent P(params object?[] c) => Tag("p", c);

		/// <summary>
		/// Creates an <c>h1</c> element.
		/// </summary>
		public static IHtmlContent H1(params object?[] c) => Tag("h1", c);

		/// <summary>
		/// Creates an <c>h2</c> element.
		/// </summary>
		public static IHtmlContent H2(params object?[] c) => Tag("h2", c);

		/// <summary>
		/// Creates an <c>h3</c> element.
		/// </summary>
		public static IHtmlContent H3(params object?[] c) => Tag("h3", c);

		/// <summary>
		/// Creates an <c>h4</c> element.
		/// </summary>
		public static IHtmlContent H4(params object?[] c) => Tag("h4", c);

		/// <summary>
		/// Creates an <c>h5</c> element.
		/// </summary>
		public static IHtmlContent H5(params object?[] c) => Tag("h5", c);

		/// <summary>
		/// Creates an <c>h6</c> element.
		/// </summary>
		public static IHtmlContent H6(params object?[] c) => Tag("h6", c);

		/// <summary>
		/// Creates an <c>a</c> element.
		/// </summary>
		public static IHtmlContent A(params object?[] c) => Tag("a", c);

		/// <summary>
		/// Creates a <c>button</c> element.
		/// </summary>
		public static IHtmlContent Button(params object?[] c) => Tag("button", c);

		/// <summary>
		/// Creates a <c>code</c> element.
		/// </summary>
		public static IHtmlContent Code(params object?[] c) => Tag("code", c);

		/// <summary>
		/// Creates a <c>pre</c> element.
		/// </summary>
		public static IHtmlContent Pre(params object?[] c) => Tag("pre", c);

		/// <summary>
		/// Creates a <c>template</c> element.
		/// </summary>
		public static IHtmlContent Template(params object?[] c) => Tag("template", c);

		/// <summary>
		/// Creates a <c>ul</c> element.
		/// </summary>
		public static IHtmlContent Ul(params object?[] c) => Tag("ul", c);

		/// <summary>
		/// Creates an <c>li</c> element.
		/// </summary>
		public static IHtmlContent Li(params object?[] c) => Tag("li", c);

		/// <summary>
		/// Creates a <c>figure</c> element.
		/// </summary>
		public static IHtmlContent Figure(params object?[] c) => Tag("figure", c);

		/// <summary>
		/// Creates a <c>figcaption</c> element.
		/// </summary>
		public static IHtmlContent FigCaption(params object?[] c) => Tag("figcaption", c);

		/// <summary>
		/// Creates a <c>form</c> element.
		/// </summary>
		public static IHtmlContent Form(params object?[] c) => Tag("form", c);

		/// <summary>
		/// Creates a <c>label</c> element.
		/// </summary>
		public static IHtmlContent Label(params object?[] c) => Tag("label", c);

		/// <summary>
		/// Creates a <c>textarea</c> element.
		/// </summary>
		public static IHtmlContent TextArea(params object?[] c) => Tag("textarea", c);

		/// <summary>
		/// Creates a <c>fieldset</c> element.
		/// </summary>
		public static IHtmlContent Fieldset(params object?[] c) => Tag("fieldset", c);

		/// <summary>
		/// Creates a <c>legend</c> element.
		/// </summary>
		public static IHtmlContent Legend(params object?[] c) => Tag("legend", c);

		/// <summary>
		/// Creates a <c>select</c> element.
		/// </summary>
		public static IHtmlContent Select(params object?[] c) => Tag("select", c);

		/// <summary>
		/// Creates an <c>option</c> element.
		/// </summary>
		public static IHtmlContent Option(params object?[] c) => Tag("option", c);

		/// <summary>
		/// Creates an <c>optgroup</c> element.
		/// </summary>
		public static IHtmlContent OptGroup(params object?[] c) => Tag("optgroup", c);

		/// <summary>
		/// Creates a <c>datalist</c> element.
		/// </summary>
		public static IHtmlContent DataList(params object?[] c) => Tag("datalist", c);

		/// <summary>
		/// Creates a <c>details</c> element.
		/// </summary>
		public static IHtmlContent Details(params object?[] c) => Tag("details", c);

		/// <summary>
		/// Creates a <c>summary</c> element.
		/// </summary>
		public static IHtmlContent Summary(params object?[] c) => Tag("summary", c);

		/// <summary>
		/// Creates a <c>dialog</c> element.
		/// </summary>
		public static IHtmlContent Dialog(params object?[] c) => Tag("dialog", c);

		/// <summary>
		/// Creates a <c>thead</c> element.
		/// </summary>
		public static IHtmlContent TableHead(params object?[] c) => Tag("thead", c);

		/// <summary>
		/// Creates a <c>tbody</c> element.
		/// </summary>
		public static IHtmlContent TableBody(params object?[] c) => Tag("tbody", c);

		/// <summary>
		/// Creates a <c>tfoot</c> element.
		/// </summary>
		public static IHtmlContent TableFoot(params object?[] c) => Tag("tfoot", c);

		/// <summary>
		/// Creates a <c>tr</c> element.
		/// </summary>
		public static IHtmlContent TableRow(params object?[] c) => Tag("tr", c);

		/// <summary>
		/// Creates a <c>th</c> element.
		/// </summary>
		public static IHtmlContent TableHeaderCell(params object?[] c) => Tag("th", c);

		/// <summary>
		/// Creates a <c>td</c> element.
		/// </summary>
		public static IHtmlContent TableDataCell(params object?[] c) => Tag("td", c);

		/// <summary>
		/// Creates a <c>caption</c> element.
		/// </summary>
		public static IHtmlContent Caption(params object?[] c) => Tag("caption", c);

		/// <summary>
		/// Creates a <c>br</c> element.
		/// </summary>
		public static IHtmlContent Br(params object?[] c) => VoidTag("br", c);

		/// <summary>
		/// Creates an <c>hr</c> element.
		/// </summary>
		public static IHtmlContent Hr(params object?[] c) => VoidTag("hr", c);

		/// <summary>
		/// Creates an <c>img</c> element.
		/// </summary>
		public static IHtmlContent Img(params object?[] c) => VoidTag("img", c);

		/// <summary>
		/// Creates an <c>input</c> element.
		/// </summary>
		public static IHtmlContent Input(params object?[] c) => VoidTag("input", c);

		/// <summary>
		/// Creates an <c>input</c> element with a strongly typed input type.
		/// </summary>
		/// <param name="type">The input type to apply.</param>
		/// <param name="c">Additional attributes or parts to include.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the input element.</returns>
		public static IHtmlContent Input(InputType type, params object?[] c)
			=> VoidTag("input", Type(type), c);

		/// <summary>
		/// Creates a syntax-highlight-ready code block.
		/// </summary>
		/// <param name="language">The language identifier to place on the nested code element.</param>
		/// <param name="c">The code content or additional parts to include.</param>
		/// <returns>
		/// An <see cref="IHtmlContent"/> representing a <c>pre</c> element containing
		/// a nested <c>code</c> element.
		/// </returns>
		/// <remarks>
		/// The nested <c>code</c> element is assigned a CSS class in the format
		/// <c>language-{language}</c>.
		/// </remarks>
		public static IHtmlContent CodeBlock(string language, params object?[] c)
			=> Tag("pre",
				Tag("code",
					Class($"language-{language}"),
					c
				)
			);

		/// <summary>
		/// Joins CSS class values into a single normalized string.
		/// </summary>
		/// <param name="classes">The class values to combine.</param>
		/// <returns>A space-delimited string containing the non-empty class values.</returns>
		/// <remarks>
		/// Empty and whitespace-only entries are ignored, and remaining values are trimmed.
		/// </remarks>
		private static string CssJoin(IEnumerable<string?> classes)
		{
			var sb = new StringBuilder();
			foreach (var c in classes)
			{
				if (string.IsNullOrWhiteSpace(c))
					continue;

				if (sb.Length > 0)
					sb.Append(' ');

				sb.Append(c.Trim());
			}
			return sb.ToString();
		}

		/// <summary>
		/// Represents the rendering behavior of an HTML attribute.
		/// </summary>
		public enum AttrKind { None, Normal, Boolean, Class }

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