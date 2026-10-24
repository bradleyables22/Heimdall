
using Microsoft.AspNetCore.Html;
using System.Text;

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
	}
}
