
namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides a fluent, builder-based API for composing HTML content while preserving
	/// </summary>
	public static partial class FluentHtml
	{
		/// <summary>
		/// Creates a generic HTML attribute.
		/// </summary>
		/// <param name="name">The attribute name.</param>
		/// <param name="value">The attribute value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Attr(string name, string? value) => Html.Attr(name, value);

		/// <summary>
		/// Creates a <c>for</c> attribute.
		/// </summary>
		/// <param name="value">The referenced element identifier.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr For(string value) => Html.For(value);

		/// <summary>
		/// Creates a boolean attribute when enabled.
		/// </summary>
		/// <param name="name">The attribute name.</param>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Bool(string name, bool on) => Html.Bool(name, on);

		/// <summary>
		/// Creates a combined <c>class</c> attribute from the provided class names.
		/// </summary>
		/// <param name="classes">The class names to combine.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Class(params string?[] classes) => Html.Class(classes);

		/// <summary>
		/// Creates an <c>id</c> attribute.
		/// </summary>
		/// <param name="id">The element identifier.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Id(string id) => Html.Id(id);

		/// <summary>
		/// Creates an <c>href</c> attribute.
		/// </summary>
		/// <param name="href">The target URL.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Href(string href) => Html.Href(href);

		/// <summary>
		/// Creates a <c>src</c> attribute.
		/// </summary>
		/// <param name="src">The source URL.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Src(string src) => Html.Src(src);

		/// <summary>
		/// Creates an <c>alt</c> attribute.
		/// </summary>
		/// <param name="alt">The alternate text value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Alt(string alt) => Html.Alt(alt);

		/// <summary>
		/// Creates a <c>type</c> attribute from a raw string value.
		/// </summary>
		/// <param name="type">The input or element type value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Type(string type) => Html.Type(type);

		/// <summary>
		/// Creates a <c>type</c> attribute from a known input type.
		/// </summary>
		/// <param name="type">The input type to render.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Type(Html.InputType type) => Html.Type(type);

		/// <summary>
		/// Creates a <c>name</c> attribute.
		/// </summary>
		/// <param name="name">The form field name.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Name(string name) => Html.Name(name);

		/// <summary>
		/// Creates a <c>value</c> attribute.
		/// </summary>
		/// <param name="value">The value to assign.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Value(string value) => Html.Value(value);

		/// <summary>
		/// Creates a <c>role</c> attribute.
		/// </summary>
		/// <param name="role">The ARIA role value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Role(string role) => Html.Role(role);

		/// <summary>
		/// Creates a <c>style</c> attribute.
		/// </summary>
		/// <param name="css">The inline CSS string.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Style(string css) => Html.Style(css);

		/// <summary>
		/// Creates a <c>content</c> attribute.
		/// </summary>
		/// <param name="value">The content attribute value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr ContentAttr(string value) => Html.Content(value);

		/// <summary>
		/// Creates a <c>title</c> attribute.
		/// </summary>
		/// <param name="value">The title text.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr TitleAttr(string value) => Html.TitleAttr(value);

		/// <summary>
		/// Creates a <c>data-*</c> attribute.
		/// </summary>
		/// <param name="key">The data attribute key without the <c>data-</c> prefix.</param>
		/// <param name="value">The attribute value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Data(string key, string value) => Html.Data(key, value);

		/// <summary>
		/// Creates an <c>aria-*</c> attribute.
		/// </summary>
		/// <param name="key">The ARIA attribute key without the <c>aria-</c> prefix.</param>
		/// <param name="value">The attribute value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Aria(string key, string value) => Html.Aria(key, value);

		/// <summary>
		/// Creates a <c>placeholder</c> attribute.
		/// </summary>
		/// <param name="value">The placeholder text.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Placeholder(string value) => Html.Placeholder(value);

		/// <summary>
		/// Creates an <c>autocomplete</c> attribute.
		/// </summary>
		/// <param name="value">The autocomplete value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr AutoComplete(string value) => Html.AutoComplete(value);

		/// <summary>
		/// Creates a <c>min</c> attribute.
		/// </summary>
		/// <param name="value">The minimum value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Min(string value) => Html.Min(value);

		/// <summary>
		/// Creates a <c>max</c> attribute.
		/// </summary>
		/// <param name="value">The maximum value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Max(string value) => Html.Max(value);

		/// <summary>
		/// Creates a <c>step</c> attribute.
		/// </summary>
		/// <param name="value">The step value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Step(string value) => Html.Step(value);

		/// <summary>
		/// Creates a <c>pattern</c> attribute.
		/// </summary>
		/// <param name="value">The validation pattern.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Pattern(string value) => Html.Pattern(value);

		/// <summary>
		/// Creates a <c>maxlength</c> attribute.
		/// </summary>
		/// <param name="value">The maximum length.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr MaxLength(int value) => Html.MaxLength(value);

		/// <summary>
		/// Creates a <c>minlength</c> attribute.
		/// </summary>
		/// <param name="value">The minimum length.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr MinLength(int value) => Html.MinLength(value);

		/// <summary>
		/// Creates a <c>rows</c> attribute.
		/// </summary>
		/// <param name="value">The row count.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Rows(int value) => Html.Rows(value);

		/// <summary>
		/// Creates a <c>cols</c> attribute.
		/// </summary>
		/// <param name="value">The column count.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Cols(int value) => Html.Cols(value);

		/// <summary>
		/// Creates an <c>action</c> attribute.
		/// </summary>
		/// <param name="value">The form action URL.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Action(string value) => Html.Action(value);

		/// <summary>
		/// Creates a <c>method</c> attribute.
		/// </summary>
		/// <param name="value">The HTTP method value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Method(string value) => Html.Method(value);

		/// <summary>
		/// Creates an <c>enctype</c> attribute.
		/// </summary>
		/// <param name="value">The encoding type value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr EncType(string value) => Html.EncType(value);

		/// <summary>
		/// Creates a <c>rel</c> attribute.
		/// </summary>
		/// <param name="value">The relationship value.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Rel(string value) => Html.Rel(value);

		/// <summary>
		/// Creates a <c>target</c> attribute.
		/// </summary>
		/// <param name="value">The browsing context target.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Target(string value) => Html.Target(value);

		/// <summary>
		/// Creates a <c>disabled</c> attribute when enabled.
		/// </summary>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Disabled(bool on = true) => Html.Disabled(on);

		/// <summary>
		/// Creates a <c>checked</c> attribute when enabled.
		/// </summary>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Checked(bool on = true) => Html.Checked(on);

		/// <summary>
		/// Creates a <c>selected</c> attribute when enabled.
		/// </summary>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Selected(bool on = true) => Html.Selected(on);

		/// <summary>
		/// Creates a <c>readonly</c> attribute when enabled.
		/// </summary>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr ReadOnly(bool on = true) => Html.ReadOnly(on);

		/// <summary>
		/// Creates a <c>required</c> attribute when enabled.
		/// </summary>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Required(bool on = true) => Html.Required(on);

		/// <summary>
		/// Creates a <c>multiple</c> attribute when enabled.
		/// </summary>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr Multiple(bool on = true) => Html.Multiple(on);

		/// <summary>
		/// Creates an <c>autofocus</c> attribute when enabled.
		/// </summary>
		/// <param name="on">Determines whether the attribute should be emitted.</param>
		/// <returns>An attribute descriptor.</returns>
		public static Html.HtmlAttr AutoFocus(bool on = true) => Html.AutoFocus(on);
	}
}
