using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heimdall.Server
{
    /// <summary>
    /// Provides extension methods for registering and running Heimdall static site generation.
    /// </summary>
    public static class HeimdallStaticSiteGenerationServiceCollection
    {
        private static readonly string[] StaticSiteGenerationArguments =
        [
            "--heimdall-generate-static",
            "--generate-static",
            "generate-static"
        ];

        /// <summary>
        /// Registers Heimdall static site generation services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>A builder used to register static pages.</returns>
        public static IHeimdallStaticSiteBuilder AddHeimdallStaticSiteGeneration(
            this IServiceCollection services)
            => AddHeimdallStaticSiteGeneration(services, configure: null);

        /// <summary>
        /// Registers Heimdall static site generation services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configure">An optional options callback.</param>
        /// <returns>A builder used to register static pages.</returns>
        public static IHeimdallStaticSiteBuilder AddHeimdallStaticSiteGeneration(
            this IServiceCollection services,
            Action<HeimdallStaticSiteGenerationOptions>? configure)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            if (configure is null)
                services.AddOptions<HeimdallStaticSiteGenerationOptions>();
            else
                services.Configure(configure);

            services.AddLogging();
            services.TryAddSingleton<IHeimdallStaticSiteGenerator, HeimdallStaticSiteGenerator>();

            return new HeimdallStaticSiteBuilder(services);
        }

        /// <summary>
        /// Runs the registered Heimdall static site generator.
        /// </summary>
        /// <param name="services">The application service provider.</param>
        /// <param name="cancellationToken">A token used to cancel generation.</param>
        /// <returns>A generation result describing the written files.</returns>
        public static Task<HeimdallStaticSiteGenerationResult> GenerateHeimdallStaticSiteAsync(
            this IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            return services
                .GetRequiredService<IHeimdallStaticSiteGenerator>()
                .GenerateAsync(cancellationToken);
        }

        /// <summary>
        /// Runs Heimdall static site generation when the application was started with a generation command argument.
        /// </summary>
        /// <param name="app">The built web application.</param>
        /// <param name="args">The application command-line arguments.</param>
        /// <param name="cancellationToken">A token used to cancel generation.</param>
        /// <returns>
        /// <c>true</c> when static generation was requested and completed; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// When this method returns <c>true</c>, application startup should return before calling <c>Run()</c>.
        /// </remarks>
        public static async Task<bool> UseHeimdallStaticSiteGenerationAsync(
            this WebApplication app,
            IEnumerable<string> args,
            CancellationToken cancellationToken = default)
        {
            if (app is null)
                throw new ArgumentNullException(nameof(app));

            if (args is null)
                throw new ArgumentNullException(nameof(args));

            if (!ShouldGenerateStaticSite(args))
                return false;

            var generator = app.Services.GetService<IHeimdallStaticSiteGenerator>()
                ?? throw new InvalidOperationException(
                    "Heimdall static site generation was requested, but static generation services are not registered. " +
                    "Call builder.Services.AddHeimdallStaticSiteGeneration(...) before builder.Build().");

            var result = await generator.GenerateAsync(cancellationToken)
                .ConfigureAwait(false);

            WriteStaticSiteGenerationResult(result);

            return true;
        }

        /// <summary>
        /// Runs static site generation when requested; otherwise runs the web application normally.
        /// </summary>
        /// <param name="app">The built web application.</param>
        /// <param name="args">The application command-line arguments.</param>
        /// <param name="cancellationToken">A token used to stop the running application or cancel generation.</param>
        public static async Task RunWithHeimdallStaticSiteGenerationAsync(
            this WebApplication app,
            IEnumerable<string> args,
            CancellationToken cancellationToken = default)
        {
            if (app is null)
                throw new ArgumentNullException(nameof(app));

            if (await app.UseHeimdallStaticSiteGenerationAsync(args, cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }

            await ((IHost)app).RunAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        private static bool ShouldGenerateStaticSite(IEnumerable<string> args)
            => args.Any(arg => StaticSiteGenerationArguments.Contains(arg, StringComparer.OrdinalIgnoreCase));

        private static void WriteStaticSiteGenerationResult(HeimdallStaticSiteGenerationResult result)
        {
            Console.WriteLine($"Generated {result.Pages.Count} page(s) and {result.Assets.Count} asset(s).");
            Console.WriteLine(result.OutputRootPath);

            foreach (var page in result.Pages)
                Console.WriteLine($"page  {page.Route} -> {page.FilePath}");

            foreach (var asset in result.Assets)
                Console.WriteLine($"asset {asset.SourcePath} -> {asset.FilePath}");

            foreach (var file in result.SupplementalFiles)
                Console.WriteLine($"{file.Kind} -> {file.FilePath}");

            if (!string.IsNullOrWhiteSpace(result.ManifestFilePath))
                Console.WriteLine($"manifest -> {result.ManifestFilePath}");
        }
    }
}
