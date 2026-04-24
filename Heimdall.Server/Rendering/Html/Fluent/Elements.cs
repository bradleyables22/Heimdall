
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides a fluent, builder-based API for composing HTML content while preserving
	/// </summary>
	public static partial class FluentHtml 
	{
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
	}
}
