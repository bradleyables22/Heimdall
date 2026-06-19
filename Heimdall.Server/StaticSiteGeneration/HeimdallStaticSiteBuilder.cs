using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server
{
    internal sealed class HeimdallStaticSiteBuilder : IHeimdallStaticSiteBuilder
    {
        private const string NotFoundRoute = "/404.html";
        private readonly IServiceCollection _services;

        public HeimdallStaticSiteBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IHeimdallStaticSiteBuilder WithStaticPage(
            string route,
            Func<IHtmlContent> render)
        {
            if (render is null)
                throw new ArgumentNullException(nameof(render));

            return WithStaticPage(route, _ => render());
        }

        public IHeimdallStaticSiteBuilder WithStaticPage(
            string route,
            Func<HeimdallStaticPageContext, IHtmlContent> render)
        {
            if (render is null)
                throw new ArgumentNullException(nameof(render));

            return WithStaticPage(route, ctx => Task.FromResult(render(ctx)));
        }

        public IHeimdallStaticSiteBuilder WithStaticPage(
            string route,
            Func<HeimdallStaticPageContext, Task<IHtmlContent>> renderAsync)
        {
            if (renderAsync is null)
                throw new ArgumentNullException(nameof(renderAsync));

            var normalizedRoute = HeimdallStaticSitePaths.NormalizeRoute(route);
            _services.AddSingleton<IHeimdallStaticPage>(
                new HeimdallStaticPage(normalizedRoute, renderAsync));

            return this;
        }

        public IHeimdallStaticSiteBuilder WithNotFoundPage(
            Func<IHtmlContent> render)
        {
            if (render is null)
                throw new ArgumentNullException(nameof(render));

            return WithStaticPage(NotFoundRoute, render);
        }

        public IHeimdallStaticSiteBuilder WithNotFoundPage(
            Func<HeimdallStaticPageContext, IHtmlContent> render)
        {
            if (render is null)
                throw new ArgumentNullException(nameof(render));

            return WithStaticPage(NotFoundRoute, render);
        }

        public IHeimdallStaticSiteBuilder WithNotFoundPage(
            Func<HeimdallStaticPageContext, Task<IHtmlContent>> renderAsync)
        {
            if (renderAsync is null)
                throw new ArgumentNullException(nameof(renderAsync));

            return WithStaticPage(NotFoundRoute, renderAsync);
        }
    }
}
