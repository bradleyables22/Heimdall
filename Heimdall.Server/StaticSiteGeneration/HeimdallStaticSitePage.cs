using Microsoft.AspNetCore.Html;

namespace Heimdall.Server
{
    internal interface IHeimdallStaticPage
    {
        string Route { get; }

        Task<IHtmlContent> RenderAsync(HeimdallStaticPageContext context);
    }

    internal sealed class HeimdallStaticPage : IHeimdallStaticPage
    {
        private readonly Func<HeimdallStaticPageContext, Task<IHtmlContent>> _renderAsync;

        public HeimdallStaticPage(
            string route,
            Func<HeimdallStaticPageContext, Task<IHtmlContent>> renderAsync)
        {
            Route = route;
            _renderAsync = renderAsync;
        }

        public string Route { get; }

        public Task<IHtmlContent> RenderAsync(HeimdallStaticPageContext context)
            => _renderAsync(context);
    }
}
