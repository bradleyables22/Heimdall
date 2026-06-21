using Microsoft.AspNetCore.Html;

namespace Heimdall.Server
{
    /// <summary>
    /// Registers pages for Heimdall static site generation.
    /// </summary>
    public interface IHeimdallStaticSiteBuilder
    {
        /// <summary>
        /// Registers a static page rendered without access to the page context.
        /// </summary>
        /// <param name="route">The public route to generate.</param>
        /// <param name="render">The page renderer.</param>
        /// <returns>The same builder for chaining.</returns>
        IHeimdallStaticSiteBuilder WithStaticPage(
            string route,
            Func<IHtmlContent> render);

        /// <summary>
        /// Registers a static page rendered with scoped services and generation metadata.
        /// </summary>
        /// <param name="route">The public route to generate.</param>
        /// <param name="render">The page renderer.</param>
        /// <returns>The same builder for chaining.</returns>
        IHeimdallStaticSiteBuilder WithStaticPage(
            string route,
            Func<HeimdallStaticPageContext, IHtmlContent> render);

        /// <summary>
        /// Registers an asynchronous static page rendered with scoped services and generation metadata.
        /// </summary>
        /// <param name="route">The public route to generate.</param>
        /// <param name="renderAsync">The asynchronous page renderer.</param>
        /// <returns>The same builder for chaining.</returns>
        IHeimdallStaticSiteBuilder WithStaticPage(
            string route,
            Func<HeimdallStaticPageContext, Task<IHtmlContent>> renderAsync);

        /// <summary>
        /// Registers a static 404 page at <c>/404.html</c>.
        /// </summary>
        /// <param name="render">The page renderer.</param>
        /// <returns>The same builder for chaining.</returns>
        IHeimdallStaticSiteBuilder WithNotFoundPage(
            Func<IHtmlContent> render);

        /// <summary>
        /// Registers a static 404 page at <c>/404.html</c>.
        /// </summary>
        /// <param name="render">The page renderer.</param>
        /// <returns>The same builder for chaining.</returns>
        IHeimdallStaticSiteBuilder WithNotFoundPage(
            Func<HeimdallStaticPageContext, IHtmlContent> render);

        /// <summary>
        /// Registers an asynchronous static 404 page at <c>/404.html</c>.
        /// </summary>
        /// <param name="renderAsync">The asynchronous page renderer.</param>
        /// <returns>The same builder for chaining.</returns>
        IHeimdallStaticSiteBuilder WithNotFoundPage(
            Func<HeimdallStaticPageContext, Task<IHtmlContent>> renderAsync);
    }
}
