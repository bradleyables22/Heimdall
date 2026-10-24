using Microsoft.AspNetCore.Html;

namespace Heimdall.Server
{
    /// <summary>
    /// Provides convenience overloads for static-site renderers that return concrete HTML content types.
    /// </summary>
    public static class HeimdallStaticSiteBuilderExtensions
    {
        /// <summary>
        /// Registers an asynchronous static page returning a concrete HTML content implementation.
        /// </summary>
        public static IHeimdallStaticSiteBuilder WithStaticPage<THtml>(
            this IHeimdallStaticSiteBuilder builder,
            string route,
            Func<HeimdallStaticPageContext, Task<THtml>> renderAsync)
            where THtml : IHtmlContent
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(renderAsync);

            Func<HeimdallStaticPageContext, Task<IHtmlContent>> adapted = async ctx =>
                await renderAsync(ctx).ConfigureAwait(false);

            return builder.WithStaticPage(route, adapted);
        }

        /// <summary>
        /// Registers a value-task-based static page returning a concrete HTML content implementation.
        /// </summary>
        public static IHeimdallStaticSiteBuilder WithStaticPage<THtml>(
            this IHeimdallStaticSiteBuilder builder,
            string route,
            Func<HeimdallStaticPageContext, ValueTask<THtml>> renderAsync)
            where THtml : IHtmlContent
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(renderAsync);

            Func<HeimdallStaticPageContext, Task<IHtmlContent>> adapted = async ctx =>
                await renderAsync(ctx).ConfigureAwait(false);

            return builder.WithStaticPage(route, adapted);
        }

        /// <summary>
        /// Registers an asynchronous static 404 page returning a concrete HTML content implementation.
        /// </summary>
        public static IHeimdallStaticSiteBuilder WithNotFoundPage<THtml>(
            this IHeimdallStaticSiteBuilder builder,
            Func<HeimdallStaticPageContext, Task<THtml>> renderAsync)
            where THtml : IHtmlContent
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(renderAsync);

            Func<HeimdallStaticPageContext, Task<IHtmlContent>> adapted = async ctx =>
                await renderAsync(ctx).ConfigureAwait(false);

            return builder.WithStaticPage("/404.html", adapted);
        }

        /// <summary>
        /// Registers a value-task-based static 404 page returning a concrete HTML content implementation.
        /// </summary>
        public static IHeimdallStaticSiteBuilder WithNotFoundPage<THtml>(
            this IHeimdallStaticSiteBuilder builder,
            Func<HeimdallStaticPageContext, ValueTask<THtml>> renderAsync)
            where THtml : IHtmlContent
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(renderAsync);

            Func<HeimdallStaticPageContext, Task<IHtmlContent>> adapted = async ctx =>
                await renderAsync(ctx).ConfigureAwait(false);

            return builder.WithStaticPage("/404.html", adapted);
        }
    }
}
