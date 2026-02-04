using Heimdall.Server;
using Microsoft.AspNetCore.Html;
using Scriban;

namespace Heimdall.Example.Raw.Heimdall.Layouts
{
    public static class MainLayout
    {
        [ContentInvocation]
        public static IHtmlContent RenderMenu(HttpContext ctx,string? pathName)
        {
            var path = pathName.ToLowerInvariant() ?? "/";

            var active = path switch
            {
                "/" => "home",
                var p when p.StartsWith("/dashboard") => "dashboard",
                var p when p.StartsWith("/settings") => "settings",
                _ => null
            };

            var templatePath = Path.Combine(
                ctx.RequestServices
                   .GetRequiredService<IWebHostEnvironment>()
                   .WebRootPath,
                "components",
                "MenuComponent.html"
            );

            var templateText = File.ReadAllText(templatePath);

            var template = Template.Parse(templateText);

            if (template.HasErrors)
                throw new InvalidOperationException(
                    string.Join(
                        Environment.NewLine,
                        template.Messages.Select(m => m.Message)
                    )
                );

            var html = template.Render(new
            {
                active,
                path
            });

            return new HtmlString(html);
        }

        [ContentInvocation]
        public static IHtmlContent ServerTime()
        {
            return new HtmlString($"Server Time: {System.DateTime.Now} UTC");
        }
    }
}
