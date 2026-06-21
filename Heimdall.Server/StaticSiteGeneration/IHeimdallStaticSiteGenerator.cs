namespace Heimdall.Server
{
    /// <summary>
    /// Generates the registered Heimdall static pages.
    /// </summary>
    public interface IHeimdallStaticSiteGenerator
    {
        /// <summary>
        /// Renders every registered static page and writes the generated files to disk.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel generation.</param>
        /// <returns>A generation result describing the written files.</returns>
        Task<HeimdallStaticSiteGenerationResult> GenerateAsync(
            CancellationToken cancellationToken = default);
    }
}
