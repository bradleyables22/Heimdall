using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides a fluent, builder-based API for composing HTML content while preserving
	/// </summary>
	public static partial class FluentHtml
	{
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
			/// Adds a nested <c>table</c> element.
			/// </summary>
			public FragmentBuilder Table(Action<ElementBuilder> b) => Tag("table", b);
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
	}
}
