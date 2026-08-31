using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.E2E.Rendering.Pages
{
	public static class StaticSitePage
	{
		public static IHtmlContent Render(HeimdallStaticPageContext ctx, string title, string kind = "page")
			=> FluentHtml.Fragment(fragment =>
			{
				fragment.Raw("<!DOCTYPE html>")
				.HtmlTag(html =>
				{
					html.Lang("en")
					.Head(head =>
					{
						head.Meta(meta => meta.Attr("charset", "utf-8"))
						.Meta(meta =>
						{
							meta.Name("viewport")
							.ContentAttr("width=device-width, initial-scale=1");
						})
						.Title(titleNode => titleNode.Text(title))
						.Link(link =>
						{
							link.Rel("stylesheet")
							.Href(ctx.ToSitePath("/css/app.css"));
						})
						.Script(script =>
						{
							script.Src(ctx.ToSitePath("/_content/HeimdallFramework.Web/heimdall-bundle.min.js"));
						});
					})
					.Body(body =>
					{
						body.Main(main =>
						{
							main.Id("static-page")
							.Data("route", ctx.Route)
							.Data("kind", kind)
							.Data("path-base", ctx.PathBase)
							.Data("output-file", Path.GetFileName(ctx.OutputFilePath))
							.H1(h => h.Text(title))
							.P(p =>
							{
								p.Id("static-route")
								.Text(ctx.Route);
							})
							.A(a =>
							{
								a.Id("static-home-link")
								.Href(ctx.ToSitePath("/"))
								.Text("Home");
							})
							.A(a =>
							{
								a.Id("static-docs-link")
								.Href(ctx.ToSitePath("/docs/start/"))
								.Text("Docs");
							})
							.A(a =>
							{
								a.Id("static-feed-link")
								.Href(ctx.ToSitePath("/feed.xml"))
								.Text("Feed");
							});
						});
					});
				});
			});
	}
}
