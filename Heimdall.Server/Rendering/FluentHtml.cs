using Microsoft.AspNetCore.Html;
using System.Buffers;
#pragma warning disable CS7022
namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides a fluent, builder-based API for composing HTML content while preserving
	/// normal C# control flow and delegating final rendering to <see cref="Html"/>.
	/// </summary>
	public static class FluentHtml
	{
		/// <summary>
		/// Creates a standard HTML element using a fluent builder callback.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="build">The builder callback used to populate attributes and children.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		public static IHtmlContent Tag(string name, Action<ElementBuilder> build)
			=> BuildTag(name, isVoid: false, build);

		/// <summary>
		/// Creates a void HTML element using a fluent builder callback.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="build">The builder callback used to populate element attributes.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered void element.</returns>
		public static IHtmlContent VoidTag(string name, Action<ElementBuilder> build)
			=> BuildTag(name, isVoid: true, build);

		/// <summary>
		/// Creates a standard HTML element from the provided parts.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="parts">The attributes and child content to merge into the element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		public static IHtmlContent Tag(string name, params object?[] parts)
			=> Html.Tag(name, parts);

		/// <summary>
		/// Creates a void HTML element from the provided parts.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="parts">The attributes to merge into the element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered void element.</returns>
		public static IHtmlContent VoidTag(string name, params object?[] parts)
			=> Html.VoidTag(name, parts);

		/// <summary>
		/// Creates an HTML fragment using a fluent builder callback.
		/// </summary>
		/// <param name="build">The builder callback used to add fragment parts.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered fragment.</returns>
		public static IHtmlContent Fragment(Action<FragmentBuilder> build)
		{
			using var fb = new FragmentBuilder(initialCapacity: 8);
			build(fb);
			return Html.Fragment(fb.ToArray());
		}

		/// <summary>
		/// Creates an HTML fragment from the provided parts.
		/// </summary>
		/// <param name="parts">The content parts to include in the fragment.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered fragment.</returns>
		public static IHtmlContent Fragment(params object?[] parts)
			=> Html.Fragment(parts);

		/// <summary>
		/// Encodes plain text as HTML content.
		/// </summary>
		/// <param name="text">The text content to encode.</param>
		/// <returns>An encoded text node.</returns>
		public static IHtmlContent Text(string? text) => Html.Text(text);

		/// <summary>
		/// Wraps raw HTML content without additional encoding.
		/// </summary>
		/// <param name="html">The raw HTML content to render.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance that writes the provided markup as-is.</returns>
		public static IHtmlContent Raw(string? html) => Html.Raw(html);

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

		/// <summary>
		/// Creates an <c>html</c> element.
		/// </summary>
		public static IHtmlContent HtmlTag(Action<ElementBuilder> b) => Tag("html", b);

		/// <summary>
		/// Creates a <c>head</c> element.
		/// </summary>
		public static IHtmlContent Head(Action<ElementBuilder> b) => Tag("head", b);

		/// <summary>
		/// Creates a <c>body</c> element.
		/// </summary>
		public static IHtmlContent Body(Action<ElementBuilder> b) => Tag("body", b);

		/// <summary>
		/// Creates a <c>header</c> element.
		/// </summary>
		public static IHtmlContent Header(Action<ElementBuilder> b) => Tag("header", b);

		/// <summary>
		/// Creates a <c>main</c> element.
		/// </summary>
		public static IHtmlContent Main(Action<ElementBuilder> b) => Tag("main", b);

		/// <summary>
		/// Creates a <c>section</c> element.
		/// </summary>
		public static IHtmlContent Section(Action<ElementBuilder> b) => Tag("section", b);

		/// <summary>
		/// Creates an <c>article</c> element.
		/// </summary>
		public static IHtmlContent Article(Action<ElementBuilder> b) => Tag("article", b);

		/// <summary>
		/// Creates an <c>aside</c> element.
		/// </summary>
		public static IHtmlContent Aside(Action<ElementBuilder> b) => Tag("aside", b);

		/// <summary>
		/// Creates a <c>footer</c> element.
		/// </summary>
		public static IHtmlContent Footer(Action<ElementBuilder> b) => Tag("footer", b);

		/// <summary>
		/// Creates a <c>nav</c> element.
		/// </summary>
		public static IHtmlContent Nav(Action<ElementBuilder> b) => Tag("nav", b);

		/// <summary>
		/// Creates a <c>title</c> element.
		/// </summary>
		public static IHtmlContent Title(Action<ElementBuilder> b) => Tag("title", b);

		/// <summary>
		/// Creates a <c>script</c> element.
		/// </summary>
		public static IHtmlContent Script(Action<ElementBuilder> b) => Tag("script", b);

		/// <summary>
		/// Creates a <c>noscript</c> element.
		/// </summary>
		public static IHtmlContent NoScript(Action<ElementBuilder> b) => Tag("noscript", b);

		/// <summary>
		/// Creates a <c>meta</c> element.
		/// </summary>
		public static IHtmlContent Meta(Action<ElementBuilder> b) => VoidTag("meta", b);

		/// <summary>
		/// Creates a <c>link</c> element.
		/// </summary>
		public static IHtmlContent Link(Action<ElementBuilder> b) => VoidTag("link", b);

		/// <summary>
		/// Creates a <c>div</c> element.
		/// </summary>
		public static IHtmlContent Div(Action<ElementBuilder> b) => Tag("div", b);

		/// <summary>
		/// Creates a <c>span</c> element.
		/// </summary>
		public static IHtmlContent Span(Action<ElementBuilder> b) => Tag("span", b);

		/// <summary>
		/// Creates a <c>strong</c> element.
		/// </summary>
		public static IHtmlContent Strong(Action<ElementBuilder> b) => Tag("strong", b);

		/// <summary>
		/// Creates a <c>p</c> element.
		/// </summary>
		public static IHtmlContent P(Action<ElementBuilder> b) => Tag("p", b);

		/// <summary>
		/// Creates an <c>h1</c> element.
		/// </summary>
		public static IHtmlContent H1(Action<ElementBuilder> b) => Tag("h1", b);

		/// <summary>
		/// Creates an <c>h2</c> element.
		/// </summary>
		public static IHtmlContent H2(Action<ElementBuilder> b) => Tag("h2", b);

		/// <summary>
		/// Creates an <c>h3</c> element.
		/// </summary>
		public static IHtmlContent H3(Action<ElementBuilder> b) => Tag("h3", b);

		/// <summary>
		/// Creates an <c>h4</c> element.
		/// </summary>
		public static IHtmlContent H4(Action<ElementBuilder> b) => Tag("h4", b);

		/// <summary>
		/// Creates an <c>h5</c> element.
		/// </summary>
		public static IHtmlContent H5(Action<ElementBuilder> b) => Tag("h5", b);

		/// <summary>
		/// Creates an <c>h6</c> element.
		/// </summary>
		public static IHtmlContent H6(Action<ElementBuilder> b) => Tag("h6", b);

		/// <summary>
		/// Creates a <c>ul</c> element.
		/// </summary>
		public static IHtmlContent Ul(Action<ElementBuilder> b) => Tag("ul", b);

		/// <summary>
		/// Creates an <c>li</c> element.
		/// </summary>
		public static IHtmlContent Li(Action<ElementBuilder> b) => Tag("li", b);

		/// <summary>
		/// Creates an <c>a</c> element.
		/// </summary>
		public static IHtmlContent A(Action<ElementBuilder> b) => Tag("a", b);

		/// <summary>
		/// Creates a <c>button</c> element.
		/// </summary>
		public static IHtmlContent Button(Action<ElementBuilder> b) => Tag("button", b);

		/// <summary>
		/// Creates an <c>i</c> element.
		/// </summary>
		public static IHtmlContent I(Action<ElementBuilder> b) => Tag("i", b);

		/// <summary>
		/// Creates a <c>code</c> element.
		/// </summary>
		public static IHtmlContent Code(Action<ElementBuilder> b) => Tag("code", b);

		/// <summary>
		/// Creates a <c>pre</c> element.
		/// </summary>
		public static IHtmlContent Pre(Action<ElementBuilder> b) => Tag("pre", b);

		/// <summary>
		/// Creates a <c>template</c> element.
		/// </summary>
		public static IHtmlContent Template(Action<ElementBuilder> b) => Tag("template", b);

		/// <summary>
		/// Creates a <c>figure</c> element.
		/// </summary>
		public static IHtmlContent Figure(Action<ElementBuilder> b) => Tag("figure", b);

		/// <summary>
		/// Creates a <c>figcaption</c> element.
		/// </summary>
		public static IHtmlContent FigCaption(Action<ElementBuilder> b) => Tag("figcaption", b);

		/// <summary>
		/// Creates a <c>form</c> element.
		/// </summary>
		public static IHtmlContent Form(Action<ElementBuilder> b) => Tag("form", b);

		/// <summary>
		/// Creates a <c>label</c> element.
		/// </summary>
		public static IHtmlContent Label(Action<ElementBuilder> b) => Tag("label", b);

		/// <summary>
		/// Creates a <c>textarea</c> element.
		/// </summary>
		public static IHtmlContent TextArea(Action<ElementBuilder> b) => Tag("textarea", b);

		/// <summary>
		/// Creates a <c>textarea</c> element.
		/// </summary>
		public static IHtmlContent Textarea(Action<ElementBuilder> b) => Tag("textarea", b);

		/// <summary>
		/// Creates a <c>fieldset</c> element.
		/// </summary>
		public static IHtmlContent Fieldset(Action<ElementBuilder> b) => Tag("fieldset", b);

		/// <summary>
		/// Creates a <c>legend</c> element.
		/// </summary>
		public static IHtmlContent Legend(Action<ElementBuilder> b) => Tag("legend", b);

		/// <summary>
		/// Creates a <c>select</c> element.
		/// </summary>
		public static IHtmlContent Select(Action<ElementBuilder> b) => Tag("select", b);

		/// <summary>
		/// Creates an <c>option</c> element.
		/// </summary>
		public static IHtmlContent Option(Action<ElementBuilder> b) => Tag("option", b);

		/// <summary>
		/// Creates an <c>optgroup</c> element.
		/// </summary>
		public static IHtmlContent OptGroup(Action<ElementBuilder> b) => Tag("optgroup", b);

		/// <summary>
		/// Creates a <c>datalist</c> element.
		/// </summary>
		public static IHtmlContent DataList(Action<ElementBuilder> b) => Tag("datalist", b);

		/// <summary>
		/// Creates a <c>details</c> element.
		/// </summary>
		public static IHtmlContent Details(Action<ElementBuilder> b) => Tag("details", b);

		/// <summary>
		/// Creates a <c>summary</c> element.
		/// </summary>
		public static IHtmlContent Summary(Action<ElementBuilder> b) => Tag("summary", b);

		/// <summary>
		/// Creates a <c>dialog</c> element.
		/// </summary>
		public static IHtmlContent Dialog(Action<ElementBuilder> b) => Tag("dialog", b);

        /// <summary>
        /// Creates a <c>table</c> element.
        /// </summary>
        public static IHtmlContent Table(Action<ElementBuilder> b) => Tag("table", b);

        /// <summary>
        /// Creates a <c>thead</c> element.
        /// </summary>
        public static IHtmlContent TableHead(Action<ElementBuilder> b) => Tag("thead", b);

		/// <summary>
		/// Creates a <c>tbody</c> element.
		/// </summary>
		public static IHtmlContent TableBody(Action<ElementBuilder> b) => Tag("tbody", b);

		/// <summary>
		/// Creates a <c>tfoot</c> element.
		/// </summary>
		public static IHtmlContent TableFoot(Action<ElementBuilder> b) => Tag("tfoot", b);

		/// <summary>
		/// Creates a <c>tr</c> element.
		/// </summary>
		public static IHtmlContent TableRow(Action<ElementBuilder> b) => Tag("tr", b);

		/// <summary>
		/// Creates a <c>th</c> element.
		/// </summary>
		public static IHtmlContent TableHeaderCell(Action<ElementBuilder> b) => Tag("th", b);

		/// <summary>
		/// Creates a <c>td</c> element.
		/// </summary>
		public static IHtmlContent TableDataCell(Action<ElementBuilder> b) => Tag("td", b);

		/// <summary>
		/// Creates a <c>caption</c> element.
		/// </summary>
		public static IHtmlContent Caption(Action<ElementBuilder> b) => Tag("caption", b);

		/// <summary>
		/// Creates a preformatted code block with a nested <c>code</c> element
		/// and a language-specific CSS class.
		/// </summary>
		/// <param name="language">The language identifier appended to the <c>language-*</c> CSS class.</param>
		/// <param name="build">The builder callback used to populate the nested <c>code</c> element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered code block.</returns>
		public static IHtmlContent CodeBlock(string language, Action<ElementBuilder> build)
			=> Tag("pre", p =>
			{
				p.Tag("code", c =>
				{
					c.Class($"language-{language}");
					build(c);
				});
			});

		/// <summary>
		/// Creates a <c>br</c> element.
		/// </summary>
		public static IHtmlContent Br(Action<ElementBuilder> b) => VoidTag("br", b);

		/// <summary>
		/// Creates an <c>hr</c> element.
		/// </summary>
		public static IHtmlContent Hr(Action<ElementBuilder> b) => VoidTag("hr", b);

		/// <summary>
		/// Creates an <c>img</c> element.
		/// </summary>
		public static IHtmlContent Img(Action<ElementBuilder> b) => VoidTag("img", b);

		/// <summary>
		/// Creates an <c>input</c> element.
		/// </summary>
		public static IHtmlContent Input(Action<ElementBuilder> b) => VoidTag("input", b);

		/// <summary>
		/// Creates an <c>input</c> element with the specified input type.
		/// </summary>
		/// <param name="type">The input type to assign.</param>
		/// <param name="build">The builder callback used to populate remaining attributes.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered input element.</returns>
		public static IHtmlContent Input(Html.InputType type, Action<ElementBuilder> build)
			=> VoidTag("input", b =>
			{
				b.Type(type);
				build(b);
			});

		/// <summary>
		/// Creates an <c>input</c> element from the provided parts.
		/// </summary>
		/// <param name="type">The input type to assign.</param>
		/// <param name="parts">The attributes to merge into the element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered input element.</returns>
		public static IHtmlContent Input(Html.InputType type, params object?[] parts)
			=> Html.Input(type, parts);

		/// <summary>
		/// Provides a pooled fluent builder for constructing a single HTML element.
		/// </summary>
		public sealed class ElementBuilder : IDisposable
		{
			private PooledBuffer<object?> _parts;

			/// <summary>
			/// Initializes a new <see cref="ElementBuilder"/> with the specified starting capacity.
			/// </summary>
			/// <param name="initialCapacity">The initial pooled buffer capacity.</param>
			internal ElementBuilder(int initialCapacity)
			{
				_parts.Init(initialCapacity);
			}

			/// <summary>
			/// Materializes the buffered parts into a compact array.
			/// </summary>
			/// <returns>An array containing the buffered parts.</returns>
			internal object?[] ToArray() => _parts.ToArray();

			/// <summary>
			/// Returns pooled resources used by this builder.
			/// </summary>
			public void Dispose() => _parts.Dispose();

			/// <summary>
			/// Adds a single part to the builder.
			/// </summary>
			/// <param name="part">The part to add.</param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder Add(object? part)
			{
				_parts.Add(part);
				return this;
			}

			/// <summary>
			/// Adds multiple parts to the builder.
			/// </summary>
			/// <param name="parts">The parts to add.</param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder Add(params object?[] parts)
			{
				if (parts is null || parts.Length == 0) return this;
				for (int i = 0; i < parts.Length; i++)
					_parts.Add(parts[i]);
				return this;
			}

			/// <summary>
			/// Adds prebuilt HTML content as a child node.
			/// </summary>
			/// <param name="content">The content to add.</param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder Content(IHtmlContent content)
			{
				_parts.Add(content);
				return this;
			}

			/// <summary>
			/// Adds encoded text content as a child node.
			/// </summary>
			/// <param name="text">The text value to encode and add.</param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder Text(string? text)
			{
				_parts.Add(Html.Text(text));
				return this;
			}

			/// <summary>
			/// Adds raw HTML content as a child node.
			/// </summary>
			/// <param name="html">The raw HTML content to add.</param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder Raw(string? html)
			{
				_parts.Add(Html.Raw(html));
				return this;
			}

			/// <summary>
			/// Adds a generic HTML attribute.
			/// </summary>
			public ElementBuilder Attr(string name, string? value) { _parts.Add(Html.Attr(name, value)); return this; }

			/// <summary>
			/// Adds a <c>for</c> attribute.
			/// </summary>
			public ElementBuilder For(string value) { _parts.Add(Html.For(value)); return this; }

			/// <summary>
			/// Adds a boolean attribute when enabled.
			/// </summary>
			public ElementBuilder Bool(string name, bool on) { _parts.Add(Html.Bool(name, on)); return this; }

			/// <summary>
			/// Adds a combined <c>class</c> attribute.
			/// </summary>
			public ElementBuilder Class(params string?[] classes) { _parts.Add(Html.Class(classes)); return this; }

			/// <summary>
			/// Adds an <c>id</c> attribute.
			/// </summary>
			public ElementBuilder Id(string id) { _parts.Add(Html.Id(id)); return this; }

			/// <summary>
			/// Adds an <c>href</c> attribute.
			/// </summary>
			public ElementBuilder Href(string href) { _parts.Add(Html.Href(href)); return this; }

			/// <summary>
			/// Adds a <c>src</c> attribute.
			/// </summary>
			public ElementBuilder Src(string src) { _parts.Add(Html.Src(src)); return this; }

			/// <summary>
			/// Adds an <c>alt</c> attribute.
			/// </summary>
			public ElementBuilder Alt(string alt) { _parts.Add(Html.Alt(alt)); return this; }

			/// <summary>
			/// Adds a <c>type</c> attribute from a raw string value.
			/// </summary>
			public ElementBuilder Type(string type) { _parts.Add(Html.Type(type)); return this; }

			/// <summary>
			/// Adds a <c>type</c> attribute from a known input type.
			/// </summary>
			public ElementBuilder Type(Html.InputType type) { _parts.Add(Html.Type(type)); return this; }

			/// <summary>
			/// Adds a <c>name</c> attribute.
			/// </summary>
			public ElementBuilder Name(string name) { _parts.Add(Html.Name(name)); return this; }

			/// <summary>
			/// Adds a <c>value</c> attribute.
			/// </summary>
			public ElementBuilder Value(string value) { _parts.Add(Html.Value(value)); return this; }

			/// <summary>
			/// Adds a <c>role</c> attribute.
			/// </summary>
			public ElementBuilder Role(string role) { _parts.Add(Html.Role(role)); return this; }

			/// <summary>
			/// Adds a <c>style</c> attribute.
			/// </summary>
			public ElementBuilder Style(string css) { _parts.Add(Html.Style(css)); return this; }

			/// <summary>
			/// Adds a <c>content</c> attribute.
			/// </summary>
			public ElementBuilder ContentAttr(string value) { _parts.Add(Html.Content(value)); return this; }

			/// <summary>
			/// Adds a <c>title</c> attribute.
			/// </summary>
			public ElementBuilder TitleAttr(string value) { _parts.Add(Html.TitleAttr(value)); return this; }

			/// <summary>
			/// Adds a <c>data-*</c> attribute.
			/// </summary>
			public ElementBuilder Data(string key, string value) { _parts.Add(Html.Data(key, value)); return this; }

			/// <summary>
			/// Adds an <c>aria-*</c> attribute.
			/// </summary>
			public ElementBuilder Aria(string key, string value) { _parts.Add(Html.Aria(key, value)); return this; }

			/// <summary>
			/// Adds a <c>placeholder</c> attribute.
			/// </summary>
			public ElementBuilder Placeholder(string value) { _parts.Add(Html.Placeholder(value)); return this; }

			/// <summary>
			/// Adds an <c>autocomplete</c> attribute.
			/// </summary>
			public ElementBuilder AutoComplete(string value) { _parts.Add(Html.AutoComplete(value)); return this; }

			/// <summary>
			/// Adds a <c>min</c> attribute.
			/// </summary>
			public ElementBuilder Min(string value) { _parts.Add(Html.Min(value)); return this; }

			/// <summary>
			/// Adds a <c>max</c> attribute.
			/// </summary>
			public ElementBuilder Max(string value) { _parts.Add(Html.Max(value)); return this; }

			/// <summary>
			/// Adds a <c>step</c> attribute.
			/// </summary>
			public ElementBuilder Step(string value) { _parts.Add(Html.Step(value)); return this; }

			/// <summary>
			/// Adds a <c>pattern</c> attribute.
			/// </summary>
			public ElementBuilder Pattern(string value) { _parts.Add(Html.Pattern(value)); return this; }

			/// <summary>
			/// Adds a <c>maxlength</c> attribute.
			/// </summary>
			public ElementBuilder MaxLength(int value) { _parts.Add(Html.MaxLength(value)); return this; }

			/// <summary>
			/// Adds a <c>minlength</c> attribute.
			/// </summary>
			public ElementBuilder MinLength(int value) { _parts.Add(Html.MinLength(value)); return this; }

			/// <summary>
			/// Adds a <c>rows</c> attribute.
			/// </summary>
			public ElementBuilder Rows(int value) { _parts.Add(Html.Rows(value)); return this; }

			/// <summary>
			/// Adds a <c>cols</c> attribute.
			/// </summary>
			public ElementBuilder Cols(int value) { _parts.Add(Html.Cols(value)); return this; }

			/// <summary>
			/// Adds an <c>action</c> attribute.
			/// </summary>
			public ElementBuilder Action(string value) { _parts.Add(Html.Action(value)); return this; }

			/// <summary>
			/// Adds a <c>method</c> attribute.
			/// </summary>
			public ElementBuilder Method(string value) { _parts.Add(Html.Method(value)); return this; }

			/// <summary>
			/// Adds an <c>enctype</c> attribute.
			/// </summary>
			public ElementBuilder EncType(string value) { _parts.Add(Html.EncType(value)); return this; }

			/// <summary>
			/// Adds a <c>rel</c> attribute.
			/// </summary>
			public ElementBuilder Rel(string value) { _parts.Add(Html.Rel(value)); return this; }

			/// <summary>
			/// Adds a <c>target</c> attribute.
			/// </summary>
			public ElementBuilder Target(string value) { _parts.Add(Html.Target(value)); return this; }

			/// <summary>
			/// Adds a <c>disabled</c> attribute when enabled.
			/// </summary>
			public ElementBuilder Disabled(bool on = true) { _parts.Add(Html.Disabled(on)); return this; }

			/// <summary>
			/// Adds a <c>checked</c> attribute when enabled.
			/// </summary>
			public ElementBuilder Checked(bool on = true) { _parts.Add(Html.Checked(on)); return this; }

			/// <summary>
			/// Adds a <c>selected</c> attribute when enabled.
			/// </summary>
			public ElementBuilder Selected(bool on = true) { _parts.Add(Html.Selected(on)); return this; }

			/// <summary>
			/// Adds a <c>readonly</c> attribute when enabled.
			/// </summary>
			public ElementBuilder ReadOnly(bool on = true) { _parts.Add(Html.ReadOnly(on)); return this; }

			/// <summary>
			/// Adds a <c>required</c> attribute when enabled.
			/// </summary>
			public ElementBuilder Required(bool on = true) { _parts.Add(Html.Required(on)); return this; }

			/// <summary>
			/// Adds a <c>multiple</c> attribute when enabled.
			/// </summary>
			public ElementBuilder Multiple(bool on = true) { _parts.Add(Html.Multiple(on)); return this; }

			/// <summary>
			/// Adds an <c>autofocus</c> attribute when enabled.
			/// </summary>
			public ElementBuilder AutoFocus(bool on = true) { _parts.Add(Html.AutoFocus(on)); return this; }

			/// <summary>
			/// Adds a nested standard HTML element.
			/// </summary>
			public ElementBuilder Tag(string name, Action<ElementBuilder> build) { _parts.Add(FluentHtml.Tag(name, build)); return this; }

			/// <summary>
			/// Adds a nested void HTML element.
			/// </summary>
			public ElementBuilder VoidTag(string name, Action<ElementBuilder> build) { _parts.Add(FluentHtml.VoidTag(name, build)); return this; }

			/// <summary>
			/// Adds a nested <c>html</c> element.
			/// </summary>
			public ElementBuilder HtmlTag(Action<ElementBuilder> b) => Tag("html", b);

			/// <summary>
			/// Adds a nested <c>head</c> element.
			/// </summary>
			public ElementBuilder Head(Action<ElementBuilder> b) => Tag("head", b);

			/// <summary>
			/// Adds a nested <c>body</c> element.
			/// </summary>
			public ElementBuilder Body(Action<ElementBuilder> b) => Tag("body", b);

			/// <summary>
			/// Adds a nested <c>header</c> element.
			/// </summary>
			public ElementBuilder Header(Action<ElementBuilder> b) => Tag("header", b);

			/// <summary>
			/// Adds a nested <c>main</c> element.
			/// </summary>
			public ElementBuilder Main(Action<ElementBuilder> b) => Tag("main", b);

			/// <summary>
			/// Adds a nested <c>section</c> element.
			/// </summary>
			public ElementBuilder Section(Action<ElementBuilder> b) => Tag("section", b);

			/// <summary>
			/// Adds a nested <c>article</c> element.
			/// </summary>
			public ElementBuilder Article(Action<ElementBuilder> b) => Tag("article", b);

			/// <summary>
			/// Adds a nested <c>aside</c> element.
			/// </summary>
			public ElementBuilder Aside(Action<ElementBuilder> b) => Tag("aside", b);

			/// <summary>
			/// Adds a nested <c>footer</c> element.
			/// </summary>
			public ElementBuilder Footer(Action<ElementBuilder> b) => Tag("footer", b);

			/// <summary>
			/// Adds a nested <c>nav</c> element.
			/// </summary>
			public ElementBuilder Nav(Action<ElementBuilder> b) => Tag("nav", b);

			/// <summary>
			/// Adds a nested <c>title</c> element.
			/// </summary>
			public ElementBuilder Title(Action<ElementBuilder> b) => Tag("title", b);

			/// <summary>
			/// Adds a nested <c>script</c> element.
			/// </summary>
			public ElementBuilder Script(Action<ElementBuilder> b) => Tag("script", b);

			/// <summary>
			/// Adds a nested <c>noscript</c> element.
			/// </summary>
			public ElementBuilder NoScript(Action<ElementBuilder> b) => Tag("noscript", b);

			/// <summary>
			/// Adds a nested <c>meta</c> element.
			/// </summary>
			public ElementBuilder Meta(Action<ElementBuilder> b) => VoidTag("meta", b);

			/// <summary>
			/// Adds a nested <c>link</c> element.
			/// </summary>
			public ElementBuilder Link(Action<ElementBuilder> b) => VoidTag("link", b);

			/// <summary>
			/// Adds a nested <c>div</c> element.
			/// </summary>
			public ElementBuilder Div(Action<ElementBuilder> b) => Tag("div", b);

			/// <summary>
			/// Adds a nested <c>span</c> element.
			/// </summary>
			public ElementBuilder Span(Action<ElementBuilder> b) => Tag("span", b);

			/// <summary>
			/// Adds a nested <c>strong</c> element.
			/// </summary>
			public ElementBuilder Strong(Action<ElementBuilder> b) => Tag("strong", b);

			/// <summary>
			/// Adds a nested <c>p</c> element.
			/// </summary>
			public ElementBuilder P(Action<ElementBuilder> b) => Tag("p", b);

			/// <summary>
			/// Adds a nested <c>h1</c> element.
			/// </summary>
			public ElementBuilder H1(Action<ElementBuilder> b) => Tag("h1", b);

			/// <summary>
			/// Adds a nested <c>h2</c> element.
			/// </summary>
			public ElementBuilder H2(Action<ElementBuilder> b) => Tag("h2", b);

			/// <summary>
			/// Adds a nested <c>h3</c> element.
			/// </summary>
			public ElementBuilder H3(Action<ElementBuilder> b) => Tag("h3", b);

			/// <summary>
			/// Adds a nested <c>h4</c> element.
			/// </summary>
			public ElementBuilder H4(Action<ElementBuilder> b) => Tag("h4", b);

			/// <summary>
			/// Adds a nested <c>h5</c> element.
			/// </summary>
			public ElementBuilder H5(Action<ElementBuilder> b) => Tag("h5", b);

			/// <summary>
			/// Adds a nested <c>h6</c> element.
			/// </summary>
			public ElementBuilder H6(Action<ElementBuilder> b) => Tag("h6", b);

			/// <summary>
			/// Adds a nested <c>ul</c> element.
			/// </summary>
			public ElementBuilder Ul(Action<ElementBuilder> b) => Tag("ul", b);

			/// <summary>
			/// Adds a nested <c>li</c> element.
			/// </summary>
			public ElementBuilder Li(Action<ElementBuilder> b) => Tag("li", b);

			/// <summary>
			/// Adds a nested <c>a</c> element.
			/// </summary>
			public ElementBuilder A(Action<ElementBuilder> b) => Tag("a", b);

			/// <summary>
			/// Adds a nested <c>button</c> element.
			/// </summary>
			public ElementBuilder Button(Action<ElementBuilder> b) => Tag("button", b);

			/// <summary>
			/// Adds a nested <c>i</c> element.
			/// </summary>
			public ElementBuilder I(Action<ElementBuilder> b) => Tag("i", b);

			/// <summary>
			/// Adds a nested <c>code</c> element.
			/// </summary>
			public ElementBuilder Code(Action<ElementBuilder> b) => Tag("code", b);

			/// <summary>
			/// Adds a nested <c>pre</c> element.
			/// </summary>
			public ElementBuilder Pre(Action<ElementBuilder> b) => Tag("pre", b);

			/// <summary>
			/// Adds a nested <c>template</c> element.
			/// </summary>
			public ElementBuilder Template(Action<ElementBuilder> b) => Tag("template", b);

			/// <summary>
			/// Adds a nested <c>figure</c> element.
			/// </summary>
			public ElementBuilder Figure(Action<ElementBuilder> b) => Tag("figure", b);

			/// <summary>
			/// Adds a nested <c>figcaption</c> element.
			/// </summary>
			public ElementBuilder FigCaption(Action<ElementBuilder> b) => Tag("figcaption", b);

			/// <summary>
			/// Adds a nested <c>form</c> element.
			/// </summary>
			public ElementBuilder Form(Action<ElementBuilder> b) => Tag("form", b);

			/// <summary>
			/// Adds a nested <c>label</c> element.
			/// </summary>
			public ElementBuilder Label(Action<ElementBuilder> b) => Tag("label", b);

			/// <summary>
			/// Adds a nested <c>textarea</c> element.
			/// </summary>
			public ElementBuilder TextArea(Action<ElementBuilder> b) => Tag("textarea", b);

			/// <summary>
			/// Adds a nested <c>textarea</c> element.
			/// </summary>
			public ElementBuilder Textarea(Action<ElementBuilder> b) => Tag("textarea", b);

			/// <summary>
			/// Adds a nested <c>fieldset</c> element.
			/// </summary>
			public ElementBuilder Fieldset(Action<ElementBuilder> b) => Tag("fieldset", b);

			/// <summary>
			/// Adds a nested <c>legend</c> element.
			/// </summary>
			public ElementBuilder Legend(Action<ElementBuilder> b) => Tag("legend", b);

			/// <summary>
			/// Adds a nested <c>select</c> element.
			/// </summary>
			public ElementBuilder Select(Action<ElementBuilder> b) => Tag("select", b);

			/// <summary>
			/// Adds a nested <c>option</c> element.
			/// </summary>
			public ElementBuilder Option(Action<ElementBuilder> b) => Tag("option", b);

			/// <summary>
			/// Adds a nested <c>optgroup</c> element.
			/// </summary>
			public ElementBuilder OptGroup(Action<ElementBuilder> b) => Tag("optgroup", b);

			/// <summary>
			/// Adds a nested <c>datalist</c> element.
			/// </summary>
			public ElementBuilder DataList(Action<ElementBuilder> b) => Tag("datalist", b);

			/// <summary>
			/// Adds a nested <c>details</c> element.
			/// </summary>
			public ElementBuilder Details(Action<ElementBuilder> b) => Tag("details", b);

			/// <summary>
			/// Adds a nested <c>summary</c> element.
			/// </summary>
			public ElementBuilder Summary(Action<ElementBuilder> b) => Tag("summary", b);

			/// <summary>
			/// Adds a nested <c>dialog</c> element.
			/// </summary>
			public ElementBuilder Dialog(Action<ElementBuilder> b) => Tag("dialog", b);

			/// <summary>
			/// Adds a nested <c>thead</c> element.
			/// </summary>
			public ElementBuilder TableHead(Action<ElementBuilder> b) => Tag("thead", b);

			/// <summary>
			/// Adds a nested <c>tbody</c> element.
			/// </summary>
			public ElementBuilder TableBody(Action<ElementBuilder> b) => Tag("tbody", b);

			/// <summary>
			/// Adds a nested <c>tfoot</c> element.
			/// </summary>
			public ElementBuilder TableFoot(Action<ElementBuilder> b) => Tag("tfoot", b);

			/// <summary>
			/// Adds a nested <c>tr</c> element.
			/// </summary>
			public ElementBuilder TableRow(Action<ElementBuilder> b) => Tag("tr", b);

			/// <summary>
			/// Adds a nested <c>th</c> element.
			/// </summary>
			public ElementBuilder TableHeaderCell(Action<ElementBuilder> b) => Tag("th", b);

			/// <summary>
			/// Adds a nested <c>td</c> element.
			/// </summary>
			public ElementBuilder TableDataCell(Action<ElementBuilder> b) => Tag("td", b);

			/// <summary>
			/// Adds a nested <c>caption</c> element.
			/// </summary>
			public ElementBuilder Caption(Action<ElementBuilder> b) => Tag("caption", b);

			/// <summary>
			/// Adds a nested code block.
			/// </summary>
			/// <param name="language">The language identifier appended to the <c>language-*</c> CSS class.</param>
			/// <param name="build">The builder callback used to populate the nested <c>code</c> element.</param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder CodeBlock(string language, Action<ElementBuilder> build)
			{
				_parts.Add(FluentHtml.CodeBlock(language, build));
				return this;
			}

			/// <summary>
			/// Adds a nested <c>br</c> element.
			/// </summary>
			public ElementBuilder Br(Action<ElementBuilder> b) => VoidTag("br", b);

			/// <summary>
			/// Adds a nested <c>hr</c> element.
			/// </summary>
			public ElementBuilder Hr(Action<ElementBuilder> b) => VoidTag("hr", b);

			/// <summary>
			/// Adds a nested <c>img</c> element.
			/// </summary>
			public ElementBuilder Img(Action<ElementBuilder> b) => VoidTag("img", b);

			/// <summary>
			/// Adds a nested <c>input</c> element.
			/// </summary>
			public ElementBuilder Input(Action<ElementBuilder> b) => VoidTag("input", b);

			/// <summary>
			/// Adds a nested <c>input</c> element with the specified type.
			/// </summary>
			/// <param name="type">The input type to assign.</param>
			/// <param name="build">The builder callback used to populate remaining attributes.</param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder Input(Html.InputType type, Action<ElementBuilder> build)
			{
				_parts.Add(FluentHtml.Input(type, build));
				return this;
			}
		}

		/// <summary>
		/// Provides a pooled fluent builder for constructing an HTML fragment.
		/// </summary>
		public sealed class FragmentBuilder : IDisposable
		{
			private PooledBuffer<object?> _parts;

			/// <summary>
			/// Initializes a new <see cref="FragmentBuilder"/> with the specified starting capacity.
			/// </summary>
			/// <param name="initialCapacity">The initial pooled buffer capacity.</param>
			internal FragmentBuilder(int initialCapacity)
			{
				_parts.Init(initialCapacity);
			}

			/// <summary>
			/// Materializes the buffered parts into a compact array.
			/// </summary>
			/// <returns>An array containing the buffered parts.</returns>
			internal object?[] ToArray() => _parts.ToArray();

			/// <summary>
			/// Returns pooled resources used by this builder.
			/// </summary>
			public void Dispose() => _parts.Dispose();

			/// <summary>
			/// Adds a single part to the fragment.
			/// </summary>
			public FragmentBuilder Add(object? part) { _parts.Add(part); return this; }

			/// <summary>
			/// Adds multiple parts to the fragment.
			/// </summary>
			public FragmentBuilder Add(params object?[] parts)
			{
				if (parts is null || parts.Length == 0) return this;
				for (int i = 0; i < parts.Length; i++)
					_parts.Add(parts[i]);
				return this;
			}

			/// <summary>
			/// Adds prebuilt HTML content to the fragment.
			/// </summary>
			public FragmentBuilder Content(IHtmlContent content) { _parts.Add(content); return this; }

			/// <summary>
			/// Adds encoded text content to the fragment.
			/// </summary>
			public FragmentBuilder Text(string? text) { _parts.Add(Html.Text(text)); return this; }

			/// <summary>
			/// Adds raw HTML content to the fragment.
			/// </summary>
			public FragmentBuilder Raw(string? html) { _parts.Add(Html.Raw(html)); return this; }

			/// <summary>
			/// Adds a <c>for</c> attribute.
			/// </summary>
			public FragmentBuilder For(string value) { _parts.Add(Html.For(value)); return this; }

			/// <summary>
			/// Adds a <c>type</c> attribute from a known input type.
			/// </summary>
			public FragmentBuilder Type(Html.InputType type) { _parts.Add(Html.Type(type)); return this; }

			/// <summary>
			/// Adds a <c>type</c> attribute from a raw string value.
			/// </summary>
			public FragmentBuilder Type(string type) { _parts.Add(Html.Type(type)); return this; }

			/// <summary>
			/// Adds a combined <c>class</c> attribute.
			/// </summary>
			public FragmentBuilder Class(params string?[] classes) { _parts.Add(Html.Class(classes)); return this; }

			/// <summary>
			/// Adds an <c>id</c> attribute.
			/// </summary>
			public FragmentBuilder Id(string id) { _parts.Add(Html.Id(id)); return this; }

			/// <summary>
			/// Adds a <c>name</c> attribute.
			/// </summary>
			public FragmentBuilder Name(string name) { _parts.Add(Html.Name(name)); return this; }

			/// <summary>
			/// Adds a <c>value</c> attribute.
			/// </summary>
			public FragmentBuilder Value(string value) { _parts.Add(Html.Value(value)); return this; }

			/// <summary>
			/// Adds a boolean attribute when enabled.
			/// </summary>
			public FragmentBuilder Bool(string name, bool on) { _parts.Add(Html.Bool(name, on)); return this; }

			/// <summary>
			/// Adds a <c>disabled</c> attribute when enabled.
			/// </summary>
			public FragmentBuilder Disabled(bool on = true) { _parts.Add(Html.Disabled(on)); return this; }

			/// <summary>
			/// Adds a <c>checked</c> attribute when enabled.
			/// </summary>
			public FragmentBuilder Checked(bool on = true) { _parts.Add(Html.Checked(on)); return this; }

			/// <summary>
			/// Adds a <c>selected</c> attribute when enabled.
			/// </summary>
			public FragmentBuilder Selected(bool on = true) { _parts.Add(Html.Selected(on)); return this; }

			/// <summary>
			/// Adds a <c>readonly</c> attribute when enabled.
			/// </summary>
			public FragmentBuilder ReadOnly(bool on = true) { _parts.Add(Html.ReadOnly(on)); return this; }

			/// <summary>
			/// Adds a <c>required</c> attribute when enabled.
			/// </summary>
			public FragmentBuilder Required(bool on = true) { _parts.Add(Html.Required(on)); return this; }

			/// <summary>
			/// Adds a <c>multiple</c> attribute when enabled.
			/// </summary>
			public FragmentBuilder Multiple(bool on = true) { _parts.Add(Html.Multiple(on)); return this; }

			/// <summary>
			/// Adds an <c>autofocus</c> attribute when enabled.
			/// </summary>
			public FragmentBuilder AutoFocus(bool on = true) { _parts.Add(Html.AutoFocus(on)); return this; }

			/// <summary>
			/// Adds a <c>placeholder</c> attribute.
			/// </summary>
			public FragmentBuilder Placeholder(string value) { _parts.Add(Html.Placeholder(value)); return this; }

			/// <summary>
			/// Adds an <c>autocomplete</c> attribute.
			/// </summary>
			public FragmentBuilder AutoComplete(string value) { _parts.Add(Html.AutoComplete(value)); return this; }

			/// <summary>
			/// Adds a <c>min</c> attribute.
			/// </summary>
			public FragmentBuilder Min(string value) { _parts.Add(Html.Min(value)); return this; }

			/// <summary>
			/// Adds a <c>max</c> attribute.
			/// </summary>
			public FragmentBuilder Max(string value) { _parts.Add(Html.Max(value)); return this; }

			/// <summary>
			/// Adds a <c>step</c> attribute.
			/// </summary>
			public FragmentBuilder Step(string value) { _parts.Add(Html.Step(value)); return this; }

			/// <summary>
			/// Adds a <c>pattern</c> attribute.
			/// </summary>
			public FragmentBuilder Pattern(string value) { _parts.Add(Html.Pattern(value)); return this; }

			/// <summary>
			/// Adds a <c>maxlength</c> attribute.
			/// </summary>
			public FragmentBuilder MaxLength(int value) { _parts.Add(Html.MaxLength(value)); return this; }

			/// <summary>
			/// Adds a <c>minlength</c> attribute.
			/// </summary>
			public FragmentBuilder MinLength(int value) { _parts.Add(Html.MinLength(value)); return this; }

			/// <summary>
			/// Adds a <c>rows</c> attribute.
			/// </summary>
			public FragmentBuilder Rows(int value) { _parts.Add(Html.Rows(value)); return this; }

			/// <summary>
			/// Adds a <c>cols</c> attribute.
			/// </summary>
			public FragmentBuilder Cols(int value) { _parts.Add(Html.Cols(value)); return this; }

			/// <summary>
			/// Adds a <c>style</c> attribute.
			/// </summary>
			public FragmentBuilder Style(string css) { _parts.Add(Html.Style(css)); return this; }

			/// <summary>
			/// Adds an <c>href</c> attribute.
			/// </summary>
			public FragmentBuilder Href(string href) { _parts.Add(Html.Href(href)); return this; }

			/// <summary>
			/// Adds a <c>src</c> attribute.
			/// </summary>
			public FragmentBuilder Src(string src) { _parts.Add(Html.Src(src)); return this; }

			/// <summary>
			/// Adds an <c>alt</c> attribute.
			/// </summary>
			public FragmentBuilder Alt(string alt) { _parts.Add(Html.Alt(alt)); return this; }

			/// <summary>
			/// Adds a <c>role</c> attribute.
			/// </summary>
			public FragmentBuilder Role(string role) { _parts.Add(Html.Role(role)); return this; }

			/// <summary>
			/// Adds a <c>content</c> attribute.
			/// </summary>
			public FragmentBuilder ContentAttr(string value) { _parts.Add(Html.Content(value)); return this; }

			/// <summary>
			/// Adds a <c>title</c> attribute.
			/// </summary>
			public FragmentBuilder TitleAttr(string value) { _parts.Add(Html.TitleAttr(value)); return this; }

			/// <summary>
			/// Adds a generic HTML attribute.
			/// </summary>
			public FragmentBuilder Attr(string name, string? value) { _parts.Add(Html.Attr(name, value)); return this; }

			/// <summary>
			/// Adds a <c>data-*</c> attribute.
			/// </summary>
			public FragmentBuilder Data(string key, string value) { _parts.Add(Html.Data(key, value)); return this; }

			/// <summary>
			/// Adds an <c>aria-*</c> attribute.
			/// </summary>
			public FragmentBuilder Aria(string key, string value) { _parts.Add(Html.Aria(key, value)); return this; }

			/// <summary>
			/// Adds an <c>action</c> attribute.
			/// </summary>
			public FragmentBuilder Action(string value) { _parts.Add(Html.Action(value)); return this; }

			/// <summary>
			/// Adds a <c>method</c> attribute.
			/// </summary>
			public FragmentBuilder Method(string value) { _parts.Add(Html.Method(value)); return this; }

			/// <summary>
			/// Adds an <c>enctype</c> attribute.
			/// </summary>
			public FragmentBuilder EncType(string value) { _parts.Add(Html.EncType(value)); return this; }

			/// <summary>
			/// Adds a <c>rel</c> attribute.
			/// </summary>
			public FragmentBuilder Rel(string value) { _parts.Add(Html.Rel(value)); return this; }

			/// <summary>
			/// Adds a <c>target</c> attribute.
			/// </summary>
			public FragmentBuilder Target(string value) { _parts.Add(Html.Target(value)); return this; }

			/// <summary>
			/// Adds a nested standard HTML element.
			/// </summary>
			public FragmentBuilder Tag(string name, Action<ElementBuilder> build) { _parts.Add(FluentHtml.Tag(name, build)); return this; }

			/// <summary>
			/// Adds a nested void HTML element.
			/// </summary>
			public FragmentBuilder VoidTag(string name, Action<ElementBuilder> build) { _parts.Add(FluentHtml.VoidTag(name, build)); return this; }

			/// <summary>
			/// Adds a nested <c>html</c> element.
			/// </summary>
			public FragmentBuilder HtmlTag(Action<ElementBuilder> b) => Tag("html", b);

			/// <summary>
			/// Adds a nested <c>head</c> element.
			/// </summary>
			public FragmentBuilder Head(Action<ElementBuilder> b) => Tag("head", b);

			/// <summary>
			/// Adds a nested <c>body</c> element.
			/// </summary>
			public FragmentBuilder Body(Action<ElementBuilder> b) => Tag("body", b);

			/// <summary>
			/// Adds a nested <c>header</c> element.
			/// </summary>
			public FragmentBuilder Header(Action<ElementBuilder> b) => Tag("header", b);

			/// <summary>
			/// Adds a nested <c>main</c> element.
			/// </summary>
			public FragmentBuilder Main(Action<ElementBuilder> b) => Tag("main", b);

			/// <summary>
			/// Adds a nested <c>section</c> element.
			/// </summary>
			public FragmentBuilder Section(Action<ElementBuilder> b) => Tag("section", b);

			/// <summary>
			/// Adds a nested <c>article</c> element.
			/// </summary>
			public FragmentBuilder Article(Action<ElementBuilder> b) => Tag("article", b);

			/// <summary>
			/// Adds a nested <c>aside</c> element.
			/// </summary>
			public FragmentBuilder Aside(Action<ElementBuilder> b) => Tag("aside", b);

			/// <summary>
			/// Adds a nested <c>footer</c> element.
			/// </summary>
			public FragmentBuilder Footer(Action<ElementBuilder> b) => Tag("footer", b);

			/// <summary>
			/// Adds a nested <c>nav</c> element.
			/// </summary>
			public FragmentBuilder Nav(Action<ElementBuilder> b) => Tag("nav", b);

			/// <summary>
			/// Adds a nested <c>title</c> element.
			/// </summary>
			public FragmentBuilder Title(Action<ElementBuilder> b) => Tag("title", b);

			/// <summary>
			/// Adds a nested <c>script</c> element.
			/// </summary>
			public FragmentBuilder Script(Action<ElementBuilder> b) => Tag("script", b);

			/// <summary>
			/// Adds a nested <c>noscript</c> element.
			/// </summary>
			public FragmentBuilder NoScript(Action<ElementBuilder> b) => Tag("noscript", b);

			/// <summary>
			/// Adds a nested <c>meta</c> element.
			/// </summary>
			public FragmentBuilder Meta(Action<ElementBuilder> b) => VoidTag("meta", b);

			/// <summary>
			/// Adds a nested <c>link</c> element.
			/// </summary>
			public FragmentBuilder Link(Action<ElementBuilder> b) => VoidTag("link", b);

			/// <summary>
			/// Adds a nested <c>div</c> element.
			/// </summary>
			public FragmentBuilder Div(Action<ElementBuilder> b) => Tag("div", b);

			/// <summary>
			/// Adds a nested <c>span</c> element.
			/// </summary>
			public FragmentBuilder Span(Action<ElementBuilder> b) => Tag("span", b);

			/// <summary>
			/// Adds a nested <c>strong</c> element.
			/// </summary>
			public FragmentBuilder Strong(Action<ElementBuilder> b) => Tag("strong", b);

			/// <summary>
			/// Adds a nested <c>p</c> element.
			/// </summary>
			public FragmentBuilder P(Action<ElementBuilder> b) => Tag("p", b);

			/// <summary>
			/// Adds a nested <c>h1</c> element.
			/// </summary>
			public FragmentBuilder H1(Action<ElementBuilder> b) => Tag("h1", b);

			/// <summary>
			/// Adds a nested <c>h2</c> element.
			/// </summary>
			public FragmentBuilder H2(Action<ElementBuilder> b) => Tag("h2", b);

			/// <summary>
			/// Adds a nested <c>h3</c> element.
			/// </summary>
			public FragmentBuilder H3(Action<ElementBuilder> b) => Tag("h3", b);

			/// <summary>
			/// Adds a nested <c>h4</c> element.
			/// </summary>
			public FragmentBuilder H4(Action<ElementBuilder> b) => Tag("h4", b);

			/// <summary>
			/// Adds a nested <c>h5</c> element.
			/// </summary>
			public FragmentBuilder H5(Action<ElementBuilder> b) => Tag("h5", b);

			/// <summary>
			/// Adds a nested <c>h6</c> element.
			/// </summary>
			public FragmentBuilder H6(Action<ElementBuilder> b) => Tag("h6", b);

			/// <summary>
			/// Adds a nested <c>ul</c> element.
			/// </summary>
			public FragmentBuilder Ul(Action<ElementBuilder> b) => Tag("ul", b);

			/// <summary>
			/// Adds a nested <c>li</c> element.
			/// </summary>
			public FragmentBuilder Li(Action<ElementBuilder> b) => Tag("li", b);

			/// <summary>
			/// Adds a nested <c>a</c> element.
			/// </summary>
			public FragmentBuilder A(Action<ElementBuilder> b) => Tag("a", b);

			/// <summary>
			/// Adds a nested <c>button</c> element.
			/// </summary>
			public FragmentBuilder Button(Action<ElementBuilder> b) => Tag("button", b);

			/// <summary>
			/// Adds a nested <c>i</c> element.
			/// </summary>
			public FragmentBuilder I(Action<ElementBuilder> b) => Tag("i", b);

			/// <summary>
			/// Adds a nested <c>code</c> element.
			/// </summary>
			public FragmentBuilder Code(Action<ElementBuilder> b) => Tag("code", b);

			/// <summary>
			/// Adds a nested <c>pre</c> element.
			/// </summary>
			public FragmentBuilder Pre(Action<ElementBuilder> b) => Tag("pre", b);

			/// <summary>
			/// Adds a nested <c>template</c> element.
			/// </summary>
			public FragmentBuilder Template(Action<ElementBuilder> b) => Tag("template", b);

			/// <summary>
			/// Adds a nested <c>figure</c> element.
			/// </summary>
			public FragmentBuilder Figure(Action<ElementBuilder> b) => Tag("figure", b);

			/// <summary>
			/// Adds a nested <c>figcaption</c> element.
			/// </summary>
			public FragmentBuilder FigCaption(Action<ElementBuilder> b) => Tag("figcaption", b);

			/// <summary>
			/// Adds a nested <c>form</c> element.
			/// </summary>
			public FragmentBuilder Form(Action<ElementBuilder> b) => Tag("form", b);

			/// <summary>
			/// Adds a nested <c>label</c> element.
			/// </summary>
			public FragmentBuilder Label(Action<ElementBuilder> b) => Tag("label", b);

			/// <summary>
			/// Adds a nested <c>textarea</c> element.
			/// </summary>
			public FragmentBuilder TextArea(Action<ElementBuilder> b) => Tag("textarea", b);

			/// <summary>
			/// Adds a nested <c>textarea</c> element.
			/// </summary>
			public FragmentBuilder Textarea(Action<ElementBuilder> b) => Tag("textarea", b);

			/// <summary>
			/// Adds a nested <c>fieldset</c> element.
			/// </summary>
			public FragmentBuilder Fieldset(Action<ElementBuilder> b) => Tag("fieldset", b);

			/// <summary>
			/// Adds a nested <c>legend</c> element.
			/// </summary>
			public FragmentBuilder Legend(Action<ElementBuilder> b) => Tag("legend", b);

			/// <summary>
			/// Adds a nested <c>select</c> element.
			/// </summary>
			public FragmentBuilder Select(Action<ElementBuilder> b) => Tag("select", b);

			/// <summary>
			/// Adds a nested <c>option</c> element.
			/// </summary>
			public FragmentBuilder Option(Action<ElementBuilder> b) => Tag("option", b);

			/// <summary>
			/// Adds a nested <c>optgroup</c> element.
			/// </summary>
			public FragmentBuilder OptGroup(Action<ElementBuilder> b) => Tag("optgroup", b);

			/// <summary>
			/// Adds a nested <c>datalist</c> element.
			/// </summary>
			public FragmentBuilder DataList(Action<ElementBuilder> b) => Tag("datalist", b);

			/// <summary>
			/// Adds a nested <c>details</c> element.
			/// </summary>
			public FragmentBuilder Details(Action<ElementBuilder> b) => Tag("details", b);

			/// <summary>
			/// Adds a nested <c>summary</c> element.
			/// </summary>
			public FragmentBuilder Summary(Action<ElementBuilder> b) => Tag("summary", b);

			/// <summary>
			/// Adds a nested <c>dialog</c> element.
			/// </summary>
			public FragmentBuilder Dialog(Action<ElementBuilder> b) => Tag("dialog", b);

			/// <summary>
			/// Adds a nested code block.
			/// </summary>
			public FragmentBuilder CodeBlock(string language, Action<ElementBuilder> build)
			{
				_parts.Add(FluentHtml.CodeBlock(language, build));
				return this;
			}

			/// <summary>
			/// Adds a nested <c>thead</c> element.
			/// </summary>
			public FragmentBuilder TableHead(Action<ElementBuilder> b) => Tag("thead", b);

			/// <summary>
			/// Adds a nested <c>tbody</c> element.
			/// </summary>
			public FragmentBuilder TableBody(Action<ElementBuilder> b) => Tag("tbody", b);

			/// <summary>
			/// Adds a nested <c>tfoot</c> element.
			/// </summary>
			public FragmentBuilder TableFoot(Action<ElementBuilder> b) => Tag("tfoot", b);

			/// <summary>
			/// Adds a nested <c>tr</c> element.
			/// </summary>
			public FragmentBuilder TableRow(Action<ElementBuilder> b) => Tag("tr", b);

			/// <summary>
			/// Adds a nested <c>th</c> element.
			/// </summary>
			public FragmentBuilder TableHeaderCell(Action<ElementBuilder> b) => Tag("th", b);

			/// <summary>
			/// Adds a nested <c>td</c> element.
			/// </summary>
			public FragmentBuilder TableDataCell(Action<ElementBuilder> b) => Tag("td", b);

			/// <summary>
			/// Adds a nested <c>caption</c> element.
			/// </summary>
			public FragmentBuilder Caption(Action<ElementBuilder> b) => Tag("caption", b);

			/// <summary>
			/// Adds a nested <c>br</c> element.
			/// </summary>
			public FragmentBuilder Br(Action<ElementBuilder> b) => VoidTag("br", b);

			/// <summary>
			/// Adds a nested <c>hr</c> element.
			/// </summary>
			public FragmentBuilder Hr(Action<ElementBuilder> b) => VoidTag("hr", b);

			/// <summary>
			/// Adds a nested <c>img</c> element.
			/// </summary>
			public FragmentBuilder Img(Action<ElementBuilder> b) => VoidTag("img", b);

			/// <summary>
			/// Adds a nested <c>input</c> element.
			/// </summary>
			public FragmentBuilder Input(Action<ElementBuilder> b) => VoidTag("input", b);

			/// <summary>
			/// Adds a nested <c>input</c> element with the specified type.
			/// </summary>
			public FragmentBuilder Input(Html.InputType type, Action<ElementBuilder> build)
			{
				_parts.Add(FluentHtml.Input(type, build));
				return this;
			}
		}

		/// <summary>
		/// Builds a standard or void HTML element from a fluent builder callback.
		/// </summary>
		/// <param name="name">The tag name to render.</param>
		/// <param name="isVoid">Determines whether the element should be rendered as a void tag.</param>
		/// <param name="build">The builder callback used to populate the element.</param>
		/// <returns>An <see cref="IHtmlContent"/> instance representing the rendered element.</returns>
		private static IHtmlContent BuildTag(string name, bool isVoid, Action<ElementBuilder> build)
		{
			using var b = new ElementBuilder(initialCapacity: 12);
			build(b);

			var parts = b.ToArray();
			return isVoid ? Html.VoidTag(name, parts) : Html.Tag(name, parts);
		}

		/// <summary>
		/// Provides a lightweight pooled buffer used to collect builder parts with minimal allocations.
		/// </summary>
		/// <typeparam name="T">The buffer item type.</typeparam>
		private struct PooledBuffer<T>
		{
			private T[]? _arr;
			private int _count;

			/// <summary>
			/// Initializes the pooled buffer using the requested starting capacity.
			/// </summary>
			/// <param name="initialCapacity">The initial capacity to rent from the shared pool.</param>
			public void Init(int initialCapacity = 8)
			{
				_arr = ArrayPool<T>.Shared.Rent(initialCapacity);
				_count = 0;
			}

			/// <summary>
			/// Adds an item to the buffer, growing the rented array when needed.
			/// </summary>
			/// <param name="item">The item to append.</param>
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
			/// Copies the buffered items into a compact array.
			/// </summary>
			/// <returns>A new array containing only the populated items.</returns>
			public T[] ToArray()
			{
				if (_arr is null || _count == 0)
					return Array.Empty<T>();

				var result = new T[_count];
				Array.Copy(_arr, 0, result, 0, _count);
				return result;
			}

			/// <summary>
			/// Returns the rented array to the shared pool and resets the buffer state.
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
	}
}
#pragma warning restore CS7022