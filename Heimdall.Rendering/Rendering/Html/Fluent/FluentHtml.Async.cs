using Microsoft.AspNetCore.Html;

#pragma warning disable CS1591

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Async callback overloads for fluent HTML composition.
	/// </summary>
	public static partial class FluentHtml
	{
		private static async ValueTask<IHtmlContent> BuildTagAsync(
			string name,
			bool isVoid,
			Func<ElementBuilder, ValueTask> build)
		{
			ArgumentNullException.ThrowIfNull(build);

			using var b = new ElementBuilder(initialCapacity: 12);
			await build(b).ConfigureAwait(false);

			var parts = b.ToArray();
			return isVoid ? Html.VoidTag(name, parts) : Html.Tag(name, parts);
		}

		/// <summary>
		/// Creates a standard HTML element with an asynchronous fluent callback.
		/// </summary>
		public static ValueTask<IHtmlContent> Tag(
			string name,
			Func<ElementBuilder, ValueTask> build)
			=> BuildTagAsync(name, isVoid: false, build);

		/// <summary>
		/// Creates a void HTML element with an asynchronous fluent callback.
		/// </summary>
		public static ValueTask<IHtmlContent> VoidTag(
			string name,
			Func<ElementBuilder, ValueTask> build)
			=> BuildTagAsync(name, isVoid: true, build);

		/// <summary>
		/// Creates an HTML fragment with an asynchronous fluent callback.
		/// </summary>
		public static async ValueTask<IHtmlContent> Fragment(Func<FragmentBuilder, ValueTask> build)
		{
			ArgumentNullException.ThrowIfNull(build);

			using var fb = new FragmentBuilder(initialCapacity: 8);
			await build(fb).ConfigureAwait(false);
			return Html.Fragment(fb.ToArray());
		}

		public static ValueTask<IHtmlContent> HtmlTag(Func<ElementBuilder, ValueTask> build)
			=> Tag("html", build);

		public static ValueTask<IHtmlContent> Head(Func<ElementBuilder, ValueTask> build)
			=> Tag("head", build);

		public static ValueTask<IHtmlContent> Body(Func<ElementBuilder, ValueTask> build)
			=> Tag("body", build);

		public static ValueTask<IHtmlContent> Header(Func<ElementBuilder, ValueTask> build)
			=> Tag("header", build);

		public static ValueTask<IHtmlContent> Main(Func<ElementBuilder, ValueTask> build)
			=> Tag("main", build);

		public static ValueTask<IHtmlContent> Section(Func<ElementBuilder, ValueTask> build)
			=> Tag("section", build);

		public static ValueTask<IHtmlContent> Article(Func<ElementBuilder, ValueTask> build)
			=> Tag("article", build);

		public static ValueTask<IHtmlContent> Aside(Func<ElementBuilder, ValueTask> build)
			=> Tag("aside", build);

		public static ValueTask<IHtmlContent> Footer(Func<ElementBuilder, ValueTask> build)
			=> Tag("footer", build);

		public static ValueTask<IHtmlContent> Nav(Func<ElementBuilder, ValueTask> build)
			=> Tag("nav", build);

		public static ValueTask<IHtmlContent> Title(Func<ElementBuilder, ValueTask> build)
			=> Tag("title", build);

		public static ValueTask<IHtmlContent> Script(Func<ElementBuilder, ValueTask> build)
			=> Tag("script", build);

		public static ValueTask<IHtmlContent> NoScript(Func<ElementBuilder, ValueTask> build)
			=> Tag("noscript", build);

		public static ValueTask<IHtmlContent> Meta(Func<ElementBuilder, ValueTask> build)
			=> VoidTag("meta", build);

		public static ValueTask<IHtmlContent> Link(Func<ElementBuilder, ValueTask> build)
			=> VoidTag("link", build);

		public static ValueTask<IHtmlContent> Div(Func<ElementBuilder, ValueTask> build)
			=> Tag("div", build);

		public static ValueTask<IHtmlContent> Span(Func<ElementBuilder, ValueTask> build)
			=> Tag("span", build);

		public static ValueTask<IHtmlContent> Strong(Func<ElementBuilder, ValueTask> build)
			=> Tag("strong", build);

		public static ValueTask<IHtmlContent> P(Func<ElementBuilder, ValueTask> build)
			=> Tag("p", build);

		public static ValueTask<IHtmlContent> H1(Func<ElementBuilder, ValueTask> build)
			=> Tag("h1", build);

		public static ValueTask<IHtmlContent> H2(Func<ElementBuilder, ValueTask> build)
			=> Tag("h2", build);

		public static ValueTask<IHtmlContent> H3(Func<ElementBuilder, ValueTask> build)
			=> Tag("h3", build);

		public static ValueTask<IHtmlContent> H4(Func<ElementBuilder, ValueTask> build)
			=> Tag("h4", build);

		public static ValueTask<IHtmlContent> H5(Func<ElementBuilder, ValueTask> build)
			=> Tag("h5", build);

		public static ValueTask<IHtmlContent> H6(Func<ElementBuilder, ValueTask> build)
			=> Tag("h6", build);

		public static ValueTask<IHtmlContent> Ul(Func<ElementBuilder, ValueTask> build)
			=> Tag("ul", build);

		public static ValueTask<IHtmlContent> Li(Func<ElementBuilder, ValueTask> build)
			=> Tag("li", build);

		public static ValueTask<IHtmlContent> A(Func<ElementBuilder, ValueTask> build)
			=> Tag("a", build);

		public static ValueTask<IHtmlContent> Button(Func<ElementBuilder, ValueTask> build)
			=> Tag("button", build);

		public static ValueTask<IHtmlContent> I(Func<ElementBuilder, ValueTask> build)
			=> Tag("i", build);

		public static ValueTask<IHtmlContent> Code(Func<ElementBuilder, ValueTask> build)
			=> Tag("code", build);

		public static ValueTask<IHtmlContent> Pre(Func<ElementBuilder, ValueTask> build)
			=> Tag("pre", build);

		public static ValueTask<IHtmlContent> Template(Func<ElementBuilder, ValueTask> build)
			=> Tag("template", build);

		public static ValueTask<IHtmlContent> Figure(Func<ElementBuilder, ValueTask> build)
			=> Tag("figure", build);

		public static ValueTask<IHtmlContent> FigCaption(Func<ElementBuilder, ValueTask> build)
			=> Tag("figcaption", build);

		public static ValueTask<IHtmlContent> Form(Func<ElementBuilder, ValueTask> build)
			=> Tag("form", build);

		public static ValueTask<IHtmlContent> Label(Func<ElementBuilder, ValueTask> build)
			=> Tag("label", build);

		public static ValueTask<IHtmlContent> TextArea(Func<ElementBuilder, ValueTask> build)
			=> Tag("textarea", build);

		public static ValueTask<IHtmlContent> Textarea(Func<ElementBuilder, ValueTask> build)
			=> Tag("textarea", build);

		public static ValueTask<IHtmlContent> Fieldset(Func<ElementBuilder, ValueTask> build)
			=> Tag("fieldset", build);

		public static ValueTask<IHtmlContent> Legend(Func<ElementBuilder, ValueTask> build)
			=> Tag("legend", build);

		public static ValueTask<IHtmlContent> Select(Func<ElementBuilder, ValueTask> build)
			=> Tag("select", build);

		public static ValueTask<IHtmlContent> Option(Func<ElementBuilder, ValueTask> build)
			=> Tag("option", build);

		public static ValueTask<IHtmlContent> OptGroup(Func<ElementBuilder, ValueTask> build)
			=> Tag("optgroup", build);

		public static ValueTask<IHtmlContent> DataList(Func<ElementBuilder, ValueTask> build)
			=> Tag("datalist", build);

		public static ValueTask<IHtmlContent> Details(Func<ElementBuilder, ValueTask> build)
			=> Tag("details", build);

		public static ValueTask<IHtmlContent> Summary(Func<ElementBuilder, ValueTask> build)
			=> Tag("summary", build);

		public static ValueTask<IHtmlContent> Dialog(Func<ElementBuilder, ValueTask> build)
			=> Tag("dialog", build);

		public static ValueTask<IHtmlContent> Table(Func<ElementBuilder, ValueTask> build)
			=> Tag("table", build);

		public static ValueTask<IHtmlContent> TableHead(Func<ElementBuilder, ValueTask> build)
			=> Tag("thead", build);

		public static ValueTask<IHtmlContent> TableBody(Func<ElementBuilder, ValueTask> build)
			=> Tag("tbody", build);

		public static ValueTask<IHtmlContent> TableFoot(Func<ElementBuilder, ValueTask> build)
			=> Tag("tfoot", build);

		public static ValueTask<IHtmlContent> TableRow(Func<ElementBuilder, ValueTask> build)
			=> Tag("tr", build);

		public static ValueTask<IHtmlContent> TableHeaderCell(Func<ElementBuilder, ValueTask> build)
			=> Tag("th", build);

		public static ValueTask<IHtmlContent> TableDataCell(Func<ElementBuilder, ValueTask> build)
			=> Tag("td", build);

		public static ValueTask<IHtmlContent> Caption(Func<ElementBuilder, ValueTask> build)
			=> Tag("caption", build);

		public static ValueTask<IHtmlContent> Br(Func<ElementBuilder, ValueTask> build)
			=> VoidTag("br", build);

		public static ValueTask<IHtmlContent> Hr(Func<ElementBuilder, ValueTask> build)
			=> VoidTag("hr", build);

		public static ValueTask<IHtmlContent> Img(Func<ElementBuilder, ValueTask> build)
			=> VoidTag("img", build);

		public static async ValueTask<IHtmlContent> CodeBlock(
			string language,
			Func<ElementBuilder, ValueTask> build)
		{
			ArgumentNullException.ThrowIfNull(build);

			return await Tag("pre", async p =>
			{
				await p.Tag("code", async c =>
				{
					c.Class($"language-{language}");
					await build(c).ConfigureAwait(false);
				}).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		public static ValueTask<IHtmlContent> Input(Func<ElementBuilder, ValueTask> build)
			=> VoidTag("input", build);

		public static async ValueTask<IHtmlContent> Input(
			Html.InputType type,
			Func<ElementBuilder, ValueTask> build)
		{
			ArgumentNullException.ThrowIfNull(build);

			return await VoidTag("input", async b =>
			{
				b.Type(type);
				await build(b).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		public sealed partial class ElementBuilder
		{
			public async ValueTask<ElementBuilder> Tag(
				string name,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.Tag(name, build).ConfigureAwait(false));
				return this;
			}

			public async ValueTask<ElementBuilder> VoidTag(
				string name,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.VoidTag(name, build).ConfigureAwait(false));
				return this;
			}

			public ValueTask<ElementBuilder> HtmlTag(Func<ElementBuilder, ValueTask> build)
				=> Tag("html", build);

			public ValueTask<ElementBuilder> Head(Func<ElementBuilder, ValueTask> build)
				=> Tag("head", build);

			public ValueTask<ElementBuilder> Body(Func<ElementBuilder, ValueTask> build)
				=> Tag("body", build);

			public ValueTask<ElementBuilder> Header(Func<ElementBuilder, ValueTask> build)
				=> Tag("header", build);

			public ValueTask<ElementBuilder> Main(Func<ElementBuilder, ValueTask> build)
				=> Tag("main", build);

			public ValueTask<ElementBuilder> Section(Func<ElementBuilder, ValueTask> build)
				=> Tag("section", build);

			public ValueTask<ElementBuilder> Article(Func<ElementBuilder, ValueTask> build)
				=> Tag("article", build);

			public ValueTask<ElementBuilder> Aside(Func<ElementBuilder, ValueTask> build)
				=> Tag("aside", build);

			public ValueTask<ElementBuilder> Footer(Func<ElementBuilder, ValueTask> build)
				=> Tag("footer", build);

			public ValueTask<ElementBuilder> Nav(Func<ElementBuilder, ValueTask> build)
				=> Tag("nav", build);

			public ValueTask<ElementBuilder> Title(Func<ElementBuilder, ValueTask> build)
				=> Tag("title", build);

			public ValueTask<ElementBuilder> Script(Func<ElementBuilder, ValueTask> build)
				=> Tag("script", build);

			public ValueTask<ElementBuilder> NoScript(Func<ElementBuilder, ValueTask> build)
				=> Tag("noscript", build);

			public ValueTask<ElementBuilder> Meta(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("meta", build);

			public ValueTask<ElementBuilder> Link(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("link", build);

			public ValueTask<ElementBuilder> Div(Func<ElementBuilder, ValueTask> build)
				=> Tag("div", build);

			public ValueTask<ElementBuilder> Span(Func<ElementBuilder, ValueTask> build)
				=> Tag("span", build);

			public ValueTask<ElementBuilder> Strong(Func<ElementBuilder, ValueTask> build)
				=> Tag("strong", build);

			public ValueTask<ElementBuilder> P(Func<ElementBuilder, ValueTask> build)
				=> Tag("p", build);

			public ValueTask<ElementBuilder> H1(Func<ElementBuilder, ValueTask> build)
				=> Tag("h1", build);

			public ValueTask<ElementBuilder> H2(Func<ElementBuilder, ValueTask> build)
				=> Tag("h2", build);

			public ValueTask<ElementBuilder> H3(Func<ElementBuilder, ValueTask> build)
				=> Tag("h3", build);

			public ValueTask<ElementBuilder> H4(Func<ElementBuilder, ValueTask> build)
				=> Tag("h4", build);

			public ValueTask<ElementBuilder> H5(Func<ElementBuilder, ValueTask> build)
				=> Tag("h5", build);

			public ValueTask<ElementBuilder> H6(Func<ElementBuilder, ValueTask> build)
				=> Tag("h6", build);

			public ValueTask<ElementBuilder> Ul(Func<ElementBuilder, ValueTask> build)
				=> Tag("ul", build);

			public ValueTask<ElementBuilder> Li(Func<ElementBuilder, ValueTask> build)
				=> Tag("li", build);

			public ValueTask<ElementBuilder> A(Func<ElementBuilder, ValueTask> build)
				=> Tag("a", build);

			public ValueTask<ElementBuilder> Button(Func<ElementBuilder, ValueTask> build)
				=> Tag("button", build);

			public ValueTask<ElementBuilder> I(Func<ElementBuilder, ValueTask> build)
				=> Tag("i", build);

			public ValueTask<ElementBuilder> Code(Func<ElementBuilder, ValueTask> build)
				=> Tag("code", build);

			public ValueTask<ElementBuilder> Pre(Func<ElementBuilder, ValueTask> build)
				=> Tag("pre", build);

			public ValueTask<ElementBuilder> Template(Func<ElementBuilder, ValueTask> build)
				=> Tag("template", build);

			public ValueTask<ElementBuilder> Figure(Func<ElementBuilder, ValueTask> build)
				=> Tag("figure", build);

			public ValueTask<ElementBuilder> FigCaption(Func<ElementBuilder, ValueTask> build)
				=> Tag("figcaption", build);

			public ValueTask<ElementBuilder> Form(Func<ElementBuilder, ValueTask> build)
				=> Tag("form", build);

			public ValueTask<ElementBuilder> Label(Func<ElementBuilder, ValueTask> build)
				=> Tag("label", build);

			public ValueTask<ElementBuilder> TextArea(Func<ElementBuilder, ValueTask> build)
				=> Tag("textarea", build);

			public ValueTask<ElementBuilder> Textarea(Func<ElementBuilder, ValueTask> build)
				=> Tag("textarea", build);

			public ValueTask<ElementBuilder> Fieldset(Func<ElementBuilder, ValueTask> build)
				=> Tag("fieldset", build);

			public ValueTask<ElementBuilder> Legend(Func<ElementBuilder, ValueTask> build)
				=> Tag("legend", build);

			public ValueTask<ElementBuilder> Select(Func<ElementBuilder, ValueTask> build)
				=> Tag("select", build);

			public ValueTask<ElementBuilder> Option(Func<ElementBuilder, ValueTask> build)
				=> Tag("option", build);

			public ValueTask<ElementBuilder> OptGroup(Func<ElementBuilder, ValueTask> build)
				=> Tag("optgroup", build);

			public ValueTask<ElementBuilder> DataList(Func<ElementBuilder, ValueTask> build)
				=> Tag("datalist", build);

			public ValueTask<ElementBuilder> Details(Func<ElementBuilder, ValueTask> build)
				=> Tag("details", build);

			public ValueTask<ElementBuilder> Summary(Func<ElementBuilder, ValueTask> build)
				=> Tag("summary", build);

			public ValueTask<ElementBuilder> Dialog(Func<ElementBuilder, ValueTask> build)
				=> Tag("dialog", build);

			public ValueTask<ElementBuilder> Table(Func<ElementBuilder, ValueTask> build)
				=> Tag("table", build);

			public ValueTask<ElementBuilder> TableHead(Func<ElementBuilder, ValueTask> build)
				=> Tag("thead", build);

			public ValueTask<ElementBuilder> TableBody(Func<ElementBuilder, ValueTask> build)
				=> Tag("tbody", build);

			public ValueTask<ElementBuilder> TableFoot(Func<ElementBuilder, ValueTask> build)
				=> Tag("tfoot", build);

			public ValueTask<ElementBuilder> TableRow(Func<ElementBuilder, ValueTask> build)
				=> Tag("tr", build);

			public ValueTask<ElementBuilder> TableHeaderCell(Func<ElementBuilder, ValueTask> build)
				=> Tag("th", build);

			public ValueTask<ElementBuilder> TableDataCell(Func<ElementBuilder, ValueTask> build)
				=> Tag("td", build);

			public ValueTask<ElementBuilder> Caption(Func<ElementBuilder, ValueTask> build)
				=> Tag("caption", build);

			public ValueTask<ElementBuilder> Br(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("br", build);

			public ValueTask<ElementBuilder> Hr(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("hr", build);

			public ValueTask<ElementBuilder> Img(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("img", build);

			public async ValueTask<ElementBuilder> CodeBlock(
				string language,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.CodeBlock(language, build).ConfigureAwait(false));
				return this;
			}

			public ValueTask<ElementBuilder> Input(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("input", build);

			public async ValueTask<ElementBuilder> Input(
				Html.InputType type,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.Input(type, build).ConfigureAwait(false));
				return this;
			}
		}

		public sealed partial class FragmentBuilder
		{
			public async ValueTask<FragmentBuilder> Tag(
				string name,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.Tag(name, build).ConfigureAwait(false));
				return this;
			}

			public async ValueTask<FragmentBuilder> VoidTag(
				string name,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.VoidTag(name, build).ConfigureAwait(false));
				return this;
			}

			public ValueTask<FragmentBuilder> HtmlTag(Func<ElementBuilder, ValueTask> build)
				=> Tag("html", build);

			public ValueTask<FragmentBuilder> Head(Func<ElementBuilder, ValueTask> build)
				=> Tag("head", build);

			public ValueTask<FragmentBuilder> Body(Func<ElementBuilder, ValueTask> build)
				=> Tag("body", build);

			public ValueTask<FragmentBuilder> Header(Func<ElementBuilder, ValueTask> build)
				=> Tag("header", build);

			public ValueTask<FragmentBuilder> Main(Func<ElementBuilder, ValueTask> build)
				=> Tag("main", build);

			public ValueTask<FragmentBuilder> Section(Func<ElementBuilder, ValueTask> build)
				=> Tag("section", build);

			public ValueTask<FragmentBuilder> Article(Func<ElementBuilder, ValueTask> build)
				=> Tag("article", build);

			public ValueTask<FragmentBuilder> Aside(Func<ElementBuilder, ValueTask> build)
				=> Tag("aside", build);

			public ValueTask<FragmentBuilder> Footer(Func<ElementBuilder, ValueTask> build)
				=> Tag("footer", build);

			public ValueTask<FragmentBuilder> Nav(Func<ElementBuilder, ValueTask> build)
				=> Tag("nav", build);

			public ValueTask<FragmentBuilder> Title(Func<ElementBuilder, ValueTask> build)
				=> Tag("title", build);

			public ValueTask<FragmentBuilder> Script(Func<ElementBuilder, ValueTask> build)
				=> Tag("script", build);

			public ValueTask<FragmentBuilder> NoScript(Func<ElementBuilder, ValueTask> build)
				=> Tag("noscript", build);

			public ValueTask<FragmentBuilder> Meta(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("meta", build);

			public ValueTask<FragmentBuilder> Link(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("link", build);

			public ValueTask<FragmentBuilder> Div(Func<ElementBuilder, ValueTask> build)
				=> Tag("div", build);

			public ValueTask<FragmentBuilder> Span(Func<ElementBuilder, ValueTask> build)
				=> Tag("span", build);

			public ValueTask<FragmentBuilder> Strong(Func<ElementBuilder, ValueTask> build)
				=> Tag("strong", build);

			public ValueTask<FragmentBuilder> P(Func<ElementBuilder, ValueTask> build)
				=> Tag("p", build);

			public ValueTask<FragmentBuilder> H1(Func<ElementBuilder, ValueTask> build)
				=> Tag("h1", build);

			public ValueTask<FragmentBuilder> H2(Func<ElementBuilder, ValueTask> build)
				=> Tag("h2", build);

			public ValueTask<FragmentBuilder> H3(Func<ElementBuilder, ValueTask> build)
				=> Tag("h3", build);

			public ValueTask<FragmentBuilder> H4(Func<ElementBuilder, ValueTask> build)
				=> Tag("h4", build);

			public ValueTask<FragmentBuilder> H5(Func<ElementBuilder, ValueTask> build)
				=> Tag("h5", build);

			public ValueTask<FragmentBuilder> H6(Func<ElementBuilder, ValueTask> build)
				=> Tag("h6", build);

			public ValueTask<FragmentBuilder> Ul(Func<ElementBuilder, ValueTask> build)
				=> Tag("ul", build);

			public ValueTask<FragmentBuilder> Li(Func<ElementBuilder, ValueTask> build)
				=> Tag("li", build);

			public ValueTask<FragmentBuilder> A(Func<ElementBuilder, ValueTask> build)
				=> Tag("a", build);

			public ValueTask<FragmentBuilder> Button(Func<ElementBuilder, ValueTask> build)
				=> Tag("button", build);

			public ValueTask<FragmentBuilder> I(Func<ElementBuilder, ValueTask> build)
				=> Tag("i", build);

			public ValueTask<FragmentBuilder> Code(Func<ElementBuilder, ValueTask> build)
				=> Tag("code", build);

			public ValueTask<FragmentBuilder> Pre(Func<ElementBuilder, ValueTask> build)
				=> Tag("pre", build);

			public ValueTask<FragmentBuilder> Template(Func<ElementBuilder, ValueTask> build)
				=> Tag("template", build);

			public ValueTask<FragmentBuilder> Figure(Func<ElementBuilder, ValueTask> build)
				=> Tag("figure", build);

			public ValueTask<FragmentBuilder> FigCaption(Func<ElementBuilder, ValueTask> build)
				=> Tag("figcaption", build);

			public ValueTask<FragmentBuilder> Form(Func<ElementBuilder, ValueTask> build)
				=> Tag("form", build);

			public ValueTask<FragmentBuilder> Label(Func<ElementBuilder, ValueTask> build)
				=> Tag("label", build);

			public ValueTask<FragmentBuilder> TextArea(Func<ElementBuilder, ValueTask> build)
				=> Tag("textarea", build);

			public ValueTask<FragmentBuilder> Textarea(Func<ElementBuilder, ValueTask> build)
				=> Tag("textarea", build);

			public ValueTask<FragmentBuilder> Fieldset(Func<ElementBuilder, ValueTask> build)
				=> Tag("fieldset", build);

			public ValueTask<FragmentBuilder> Legend(Func<ElementBuilder, ValueTask> build)
				=> Tag("legend", build);

			public ValueTask<FragmentBuilder> Select(Func<ElementBuilder, ValueTask> build)
				=> Tag("select", build);

			public ValueTask<FragmentBuilder> Option(Func<ElementBuilder, ValueTask> build)
				=> Tag("option", build);

			public ValueTask<FragmentBuilder> OptGroup(Func<ElementBuilder, ValueTask> build)
				=> Tag("optgroup", build);

			public ValueTask<FragmentBuilder> DataList(Func<ElementBuilder, ValueTask> build)
				=> Tag("datalist", build);

			public ValueTask<FragmentBuilder> Details(Func<ElementBuilder, ValueTask> build)
				=> Tag("details", build);

			public ValueTask<FragmentBuilder> Summary(Func<ElementBuilder, ValueTask> build)
				=> Tag("summary", build);

			public ValueTask<FragmentBuilder> Dialog(Func<ElementBuilder, ValueTask> build)
				=> Tag("dialog", build);

			public ValueTask<FragmentBuilder> Table(Func<ElementBuilder, ValueTask> build)
				=> Tag("table", build);

			public ValueTask<FragmentBuilder> TableHead(Func<ElementBuilder, ValueTask> build)
				=> Tag("thead", build);

			public ValueTask<FragmentBuilder> TableBody(Func<ElementBuilder, ValueTask> build)
				=> Tag("tbody", build);

			public ValueTask<FragmentBuilder> TableFoot(Func<ElementBuilder, ValueTask> build)
				=> Tag("tfoot", build);

			public ValueTask<FragmentBuilder> TableRow(Func<ElementBuilder, ValueTask> build)
				=> Tag("tr", build);

			public ValueTask<FragmentBuilder> TableHeaderCell(Func<ElementBuilder, ValueTask> build)
				=> Tag("th", build);

			public ValueTask<FragmentBuilder> TableDataCell(Func<ElementBuilder, ValueTask> build)
				=> Tag("td", build);

			public ValueTask<FragmentBuilder> Caption(Func<ElementBuilder, ValueTask> build)
				=> Tag("caption", build);

			public ValueTask<FragmentBuilder> Br(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("br", build);

			public ValueTask<FragmentBuilder> Hr(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("hr", build);

			public ValueTask<FragmentBuilder> Img(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("img", build);

			public async ValueTask<FragmentBuilder> CodeBlock(
				string language,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.CodeBlock(language, build).ConfigureAwait(false));
				return this;
			}

			public ValueTask<FragmentBuilder> Input(Func<ElementBuilder, ValueTask> build)
				=> VoidTag("input", build);

			public async ValueTask<FragmentBuilder> Input(
				Html.InputType type,
				Func<ElementBuilder, ValueTask> build)
			{
				_parts.Add(await FluentHtml.Input(type, build).ConfigureAwait(false));
				return this;
			}
		}
	}
}

#pragma warning restore CS1591

