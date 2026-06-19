using Bs = Heimdall.Bootstrap.Bootstrap;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.E2E.Rendering.Layouts
{
	public static class MainLayout
	{
		public static IHtmlContent Render(IHtmlContent page, string title)
			=> FluentHtml.Fragment(f =>
			{
				f.Raw("<!DOCTYPE html>")
				.HtmlTag(html =>
				{
					html.Attr("lang", "en")
					.Head(head =>
					{
						head.Meta(m => m.Attr("charset", "utf-8"))
						.Meta(m =>
						{
							m.Name("viewport")
							.ContentAttr("width=device-width, initial-scale=1");
						})
						.Title(t => t.Text(title))
						.Link(l =>
						{
							l.Attr("rel", "stylesheet")
							.Href("css/app.css");
						})
						.Link(l =>
						{
							l.Attr("rel", "stylesheet")
							.Href("css/bootstrap.css");
						})
						.Script(s => s.Src("/_content/HeimdallFramework.Web/heimdall-bundle.min.js"));

						if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLowerInvariant() == "development")
							head.Script(s => s.Src("js/heimdall.debug.js"));
					})
					.Body(body =>
					{
						body.Div(d =>
						{
							d.Class(Bs.Layout.ContainerFluid, Bs.Spacing.Py(2));
							d.Add(page);
						});
					});
				});
			});
	}
}
