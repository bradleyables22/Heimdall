
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
	}
}
