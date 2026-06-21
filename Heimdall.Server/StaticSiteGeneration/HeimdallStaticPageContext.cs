using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server
{
    /// <summary>
    /// Provides per-page services and metadata while a Heimdall static page is rendered.
    /// </summary>
    public sealed class HeimdallStaticPageContext
    {
        internal HeimdallStaticPageContext(
            IServiceProvider services,
            string route,
            string outputRootPath,
            string outputFilePath,
            string contentRootPath,
            string? webRootPath,
            string pathBase,
            CancellationToken cancellationToken)
        {
            Services = services;
            Route = route;
            OutputRootPath = outputRootPath;
            OutputFilePath = outputFilePath;
            ContentRootPath = contentRootPath;
            WebRootPath = webRootPath;
            PathBase = pathBase;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the scoped service provider for this page render.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the normalized route being generated, such as <c>/</c> or <c>/docs/start</c>.
        /// </summary>
        public string Route { get; }

        /// <summary>
        /// Gets the absolute root directory that receives generated files.
        /// </summary>
        public string OutputRootPath { get; }

        /// <summary>
        /// Gets the absolute file path that receives the rendered HTML for this page.
        /// </summary>
        public string OutputFilePath { get; }

        /// <summary>
        /// Gets the application content root used to resolve relative output paths.
        /// </summary>
        public string ContentRootPath { get; }

        /// <summary>
        /// Gets the application web root path when available.
        /// </summary>
        public string? WebRootPath { get; }

        /// <summary>
        /// Gets the public path base used when generating rooted URLs.
        /// </summary>
        public string PathBase { get; }

        /// <summary>
        /// Gets the cancellation token for this static generation run.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Resolves a required service from the scoped page provider.
        /// </summary>
        /// <typeparam name="T">The service type to resolve.</typeparam>
        /// <returns>The resolved service instance.</returns>
        public T GetRequiredService<T>()
            where T : notnull
            => Services.GetRequiredService<T>();

        /// <summary>
        /// Combines a rooted or relative site path with <see cref="PathBase"/>.
        /// </summary>
        /// <param name="path">The public path to map, such as <c>"/css/site.css"</c>.</param>
        /// <returns>The path with the configured static site path base applied.</returns>
        public string ToSitePath(string path)
            => HeimdallStaticSitePaths.CombinePathBase(PathBase, path);
    }
}
