using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Heimdall.Server.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Server
{
    internal sealed class HeimdallStaticSiteGenerator : IHeimdallStaticSiteGenerator
    {
        private const int ManifestSchemaVersion = 1;
        private const string PageKind = "page";
        private const string AssetKind = "asset";
        private const string SitemapKind = "sitemap";
        private const string RobotsTxtKind = "robots";
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly IServiceProvider _services;
        private readonly IReadOnlyList<IHeimdallStaticPage> _pages;
        private readonly IOptions<HeimdallStaticSiteGenerationOptions> _options;
        private readonly IHostEnvironment? _hostEnvironment;
        private readonly IWebHostEnvironment? _webHostEnvironment;
        private readonly ILogger<HeimdallStaticSiteGenerator> _logger;

        public HeimdallStaticSiteGenerator(
            IServiceProvider services,
            IEnumerable<IHeimdallStaticPage> pages,
            IOptions<HeimdallStaticSiteGenerationOptions> options,
            ILogger<HeimdallStaticSiteGenerator> logger,
            IHostEnvironment? hostEnvironment = null,
            IWebHostEnvironment? webHostEnvironment = null)
        {
            _services = services;
            _pages = pages.ToArray();
            _options = options;
            _hostEnvironment = hostEnvironment;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        public async Task<HeimdallStaticSiteGenerationResult> GenerateAsync(
            CancellationToken cancellationToken = default)
        {
            var options = _options.Value;
            var pathBase = HeimdallStaticSitePaths.NormalizePathBase(options.PathBase);
            var contentRootPath = ResolveContentRootPath();
            var outputRootPath = HeimdallStaticSitePaths.ResolveOutputRootPath(
                options,
                contentRootPath,
                _webHostEnvironment?.WebRootPath);
            var manifestFilePath = ResolveManifestFilePath(options, outputRootPath, force: options.WriteManifest);

            _logger.LogInformation(
                "Generating Heimdall static site with {PageCount} page(s) into {OutputRootPath}.",
                _pages.Count,
                outputRootPath);

            Directory.CreateDirectory(outputRootPath);

            var pageOutputPaths = BuildPageOutputPaths(outputRootPath);
            var assetPlan = BuildAssetPlan(outputRootPath, pageOutputPaths);
            var supplementalPlan = BuildSupplementalPlan(options, outputRootPath, pathBase);
            ReserveOutputPaths(pageOutputPaths, assetPlan, supplementalPlan, manifestFilePath);

            await CleanOutputIfRequestedAsync(
                options,
                outputRootPath,
                contentRootPath,
                manifestFilePath ?? ResolveManifestFilePath(options, outputRootPath, force: true),
                cancellationToken).ConfigureAwait(false);

            var pages = new List<HeimdallStaticPageOutput>(_pages.Count);
            foreach (var page in _pages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var outputFilePath = pageOutputPaths[page];
                await GeneratePageAsync(
                    page,
                    contentRootPath,
                    outputRootPath,
                    outputFilePath,
                    pathBase,
                    pages,
                    cancellationToken).ConfigureAwait(false);
            }

            var copiedAssets = await CopyAssetsAsync(assetPlan, cancellationToken)
                .ConfigureAwait(false);
            var supplementalFiles = await WriteSupplementalFilesAsync(
                supplementalPlan,
                cancellationToken).ConfigureAwait(false);

            var writtenManifestFilePath = await WriteManifestAsync(
                options,
                outputRootPath,
                manifestFilePath,
                pages,
                copiedAssets,
                supplementalFiles,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Generated Heimdall static site with {PageCount} page(s), {AssetCount} asset(s), and {SupplementalFileCount} supplemental file(s) into {OutputRootPath}.",
                pages.Count,
                copiedAssets.Count,
                supplementalFiles.Count,
                outputRootPath);

            return new HeimdallStaticSiteGenerationResult(
                outputRootPath,
                pages,
                copiedAssets,
                supplementalFiles,
                writtenManifestFilePath);
        }

        private IReadOnlyDictionary<IHeimdallStaticPage, string> BuildPageOutputPaths(string outputRootPath)
        {
            var pageOutputPaths = new Dictionary<IHeimdallStaticPage, string>();
            var outputPaths = new Dictionary<string, IHeimdallStaticPage>(StringComparer.OrdinalIgnoreCase);

            foreach (var page in _pages)
            {
                var outputFilePath = HeimdallStaticSitePaths.ResolveOutputFilePath(outputRootPath, page.Route);

                if (outputPaths.TryGetValue(outputFilePath, out var existing))
                {
                    throw new InvalidOperationException(
                        $"Static pages '{existing.Route}' and '{page.Route}' both generate '{outputFilePath}'.");
                }

                outputPaths.Add(outputFilePath, page);
                pageOutputPaths.Add(page, outputFilePath);
            }

            return pageOutputPaths;
        }

        private IReadOnlyList<AssetCopyPlan> BuildAssetPlan(
            string outputRootPath,
            IReadOnlyDictionary<IHeimdallStaticPage, string> pageOutputPaths)
        {
            var assets = new List<AssetCopyPlan>();
            var assetOutputPaths = new Dictionary<string, AssetCopyPlan>(StringComparer.OrdinalIgnoreCase);
            var pagePathsByOutput = pageOutputPaths.ToDictionary(
                pair => pair.Value,
                pair => pair.Key,
                StringComparer.OrdinalIgnoreCase);

            AddPhysicalWebRootAssetPlans(outputRootPath, pagePathsByOutput, assets, assetOutputPaths);
            AddStaticWebAssetPlans(outputRootPath, pagePathsByOutput, assets, assetOutputPaths);

            return assets;
        }

        private void AddPhysicalWebRootAssetPlans(
            string outputRootPath,
            IReadOnlyDictionary<string, IHeimdallStaticPage> pageOutputPaths,
            List<AssetCopyPlan> assets,
            Dictionary<string, AssetCopyPlan> assetOutputPaths)
        {
            if (!_options.Value.CopyWebRootAssets)
                return;

            var webRootPath = _webHostEnvironment?.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath) || !Directory.Exists(webRootPath))
                return;

            webRootPath = Path.GetFullPath(webRootPath);

            foreach (var sourceFilePath in Directory.EnumerateFiles(webRootPath, "*", SearchOption.AllDirectories))
            {
                var normalizedSourcePath = Path.GetFullPath(sourceFilePath);

                if (HeimdallStaticSitePaths.IsSameOrInsideRoot(outputRootPath, normalizedSourcePath))
                    continue;

                var relativePath = Path.GetRelativePath(webRootPath, normalizedSourcePath);
                var outputFilePath = HeimdallStaticSitePaths.ResolveAssetOutputFilePath(outputRootPath, relativePath);

                if (HeimdallStaticSitePaths.PathEquals(normalizedSourcePath, outputFilePath))
                    continue;

                var plan = AssetCopyPlan.FromPhysicalFile(normalizedSourcePath, outputFilePath);
                AddAssetPlan(pageOutputPaths, assets, assetOutputPaths, plan);
            }
        }

        private void AddStaticWebAssetPlans(
            string outputRootPath,
            IReadOnlyDictionary<string, IHeimdallStaticPage> pageOutputPaths,
            List<AssetCopyPlan> assets,
            Dictionary<string, AssetCopyPlan> assetOutputPaths)
        {
            if (!_options.Value.CopyStaticWebAssets)
                return;

            var provider = _webHostEnvironment?.WebRootFileProvider;
            if (provider is null)
                return;

            var contentRoot = provider.GetDirectoryContents("_content");
            if (!contentRoot.Exists)
                return;

            AddStaticWebAssetDirectory(
                provider,
                "_content",
                contentRoot,
                outputRootPath,
                pageOutputPaths,
                assets,
                assetOutputPaths);
        }

        private void AddStaticWebAssetDirectory(
            IFileProvider provider,
            string relativeDirectoryPath,
            IDirectoryContents contents,
            string outputRootPath,
            IReadOnlyDictionary<string, IHeimdallStaticPage> pageOutputPaths,
            List<AssetCopyPlan> assets,
            Dictionary<string, AssetCopyPlan> assetOutputPaths)
        {
            foreach (var item in contents)
            {
                if (!item.Exists)
                    continue;

                var relativePath = CombineProviderPath(relativeDirectoryPath, item.Name);

                if (item.IsDirectory)
                {
                    var childContents = provider.GetDirectoryContents(relativePath);
                    if (childContents.Exists)
                    {
                        AddStaticWebAssetDirectory(
                            provider,
                            relativePath,
                            childContents,
                            outputRootPath,
                            pageOutputPaths,
                            assets,
                            assetOutputPaths);
                    }

                    continue;
                }

                var physicalSourcePath = string.IsNullOrWhiteSpace(item.PhysicalPath)
                    ? null
                    : Path.GetFullPath(item.PhysicalPath);

                if (!string.IsNullOrWhiteSpace(physicalSourcePath) &&
                    HeimdallStaticSitePaths.IsSameOrInsideRoot(outputRootPath, physicalSourcePath))
                {
                    continue;
                }

                var outputFilePath = HeimdallStaticSitePaths.ResolveAssetOutputFilePath(outputRootPath, relativePath);
                if (!string.IsNullOrWhiteSpace(physicalSourcePath) &&
                    HeimdallStaticSitePaths.PathEquals(physicalSourcePath, outputFilePath))
                {
                    continue;
                }

                var plan = AssetCopyPlan.FromFileInfo(
                    item,
                    sourceDisplayPath: relativePath,
                    outputFilePath,
                    physicalSourcePath);

                AddAssetPlan(pageOutputPaths, assets, assetOutputPaths, plan);
            }
        }

        private IReadOnlyList<GeneratedFilePlan> BuildSupplementalPlan(
            HeimdallStaticSiteGenerationOptions options,
            string outputRootPath,
            string pathBase)
        {
            var plans = new List<GeneratedFilePlan>();
            Uri? siteUrl = null;

            if (options.GenerateSitemap)
            {
                siteUrl = ResolveSiteUrl(options.SiteUrl);
                var route = HeimdallStaticSitePaths.NormalizeRoute(options.SitemapPath);
                var outputFilePath = HeimdallStaticSitePaths.ResolveOutputFilePath(outputRootPath, route);

                plans.Add(new GeneratedFilePlan(
                    SitemapKind,
                    route,
                    outputFilePath,
                    () => RenderSitemap(siteUrl, route, pathBase)));
            }

            if (options.GenerateRobotsTxt)
            {
                var route = HeimdallStaticSitePaths.NormalizeRoute(options.RobotsTxtPath);
                var sitemapRoute = options.GenerateSitemap
                    ? HeimdallStaticSitePaths.NormalizeRoute(options.SitemapPath)
                    : null;
                siteUrl ??= options.GenerateSitemap ? ResolveSiteUrl(options.SiteUrl) : null;
                var outputFilePath = HeimdallStaticSitePaths.ResolveOutputFilePath(outputRootPath, route);

                plans.Add(new GeneratedFilePlan(
                    RobotsTxtKind,
                    route,
                    outputFilePath,
                    () => RenderRobotsTxt(options, siteUrl, sitemapRoute, pathBase)));
            }

            return plans;
        }

        private void ReserveOutputPaths(
            IReadOnlyDictionary<IHeimdallStaticPage, string> pageOutputPaths,
            IReadOnlyList<AssetCopyPlan> assetPlan,
            IReadOnlyList<GeneratedFilePlan> supplementalPlan,
            string? manifestFilePath)
        {
            var reserved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in pageOutputPaths)
                AddReservedOutputPath(reserved, pair.Value, $"static page '{pair.Key.Route}'");

            foreach (var asset in assetPlan)
                AddReservedOutputPath(reserved, asset.OutputPath, $"static asset '{asset.SourcePath}'");

            foreach (var file in supplementalPlan)
                AddReservedOutputPath(reserved, file.OutputPath, $"static {file.Kind} file '{file.Route}'");

            if (!string.IsNullOrWhiteSpace(manifestFilePath))
                AddReservedOutputPath(reserved, manifestFilePath, "static generation manifest");
        }

        private static void AddReservedOutputPath(
            Dictionary<string, string> reserved,
            string outputPath,
            string description)
        {
            if (reserved.TryGetValue(outputPath, out var existing))
            {
                throw new InvalidOperationException(
                    $"Static output collision: {existing} and {description} both generate '{outputPath}'.");
            }

            reserved.Add(outputPath, description);
        }

        private async Task CleanOutputIfRequestedAsync(
            HeimdallStaticSiteGenerationOptions options,
            string outputRootPath,
            string contentRootPath,
            string? manifestFilePath,
            CancellationToken cancellationToken)
        {
            if (!options.CleanOutputPath)
                return;

            cancellationToken.ThrowIfCancellationRequested();

            if (CanFullyCleanOutputRoot(outputRootPath, contentRootPath, _webHostEnvironment?.WebRootPath))
            {
                _logger.LogInformation("Cleaning Heimdall static site output root {OutputRootPath}.", outputRootPath);
                DeleteOutputRootContents(outputRootPath);
                Directory.CreateDirectory(outputRootPath);
                return;
            }

            var deletedCount = await DeletePreviousManifestFilesAsync(
                outputRootPath,
                manifestFilePath,
                cancellationToken).ConfigureAwait(false);

            if (deletedCount == 0)
            {
                _logger.LogInformation(
                    "Heimdall static site clean was requested for {OutputRootPath}, but the output root is also an application root and no previous generated files were removed. A previous manifest is required for safe root-level cleaning.",
                    outputRootPath);
            }
            else
            {
                _logger.LogInformation(
                    "Removed {DeletedFileCount} previously generated Heimdall static site file(s) from {OutputRootPath}.",
                    deletedCount,
                    outputRootPath);
            }
        }

        private async Task<int> DeletePreviousManifestFilesAsync(
            string outputRootPath,
            string? manifestFilePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(manifestFilePath) || !File.Exists(manifestFilePath))
                return 0;

            HeimdallStaticSiteManifest? manifest;
            try
            {
                await using var stream = new FileStream(
                    manifestFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);
                manifest = await JsonSerializer.DeserializeAsync<HeimdallStaticSiteManifest>(
                    stream,
                    ManifestJsonOptions,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not read Heimdall static site manifest {ManifestFilePath}. Skipping manifest-based clean.", manifestFilePath);
                return 0;
            }

            var directoriesToPrune = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deletedCount = 0;

            foreach (var file in manifest?.Files ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = ResolveManifestEntryPath(outputRootPath, file);
                if (filePath is null || !File.Exists(filePath))
                    continue;

                File.Delete(filePath);
                deletedCount++;
                AddDirectoryToPrune(directoriesToPrune, outputRootPath, Path.GetDirectoryName(filePath));
            }

            if (File.Exists(manifestFilePath))
            {
                File.Delete(manifestFilePath);
                deletedCount++;
                AddDirectoryToPrune(directoriesToPrune, outputRootPath, Path.GetDirectoryName(manifestFilePath));
            }

            PruneEmptyDirectories(outputRootPath, directoriesToPrune);
            return deletedCount;
        }

        private static string? ResolveManifestEntryPath(
            string outputRootPath,
            HeimdallStaticSiteManifestFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.RelativePath))
                return HeimdallStaticSitePaths.ResolveAssetOutputFilePath(outputRootPath, file.RelativePath);

            if (!string.IsNullOrWhiteSpace(file.FilePath) &&
                HeimdallStaticSitePaths.IsSameOrInsideRoot(outputRootPath, file.FilePath))
            {
                return Path.GetFullPath(file.FilePath);
            }

            return null;
        }

        private static void AddDirectoryToPrune(
            HashSet<string> directories,
            string outputRootPath,
            string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return;

            var current = Path.GetFullPath(directory);
            var root = Path.GetFullPath(outputRootPath);

            while (!HeimdallStaticSitePaths.PathEquals(root, current) &&
                HeimdallStaticSitePaths.IsSameOrInsideRoot(root, current))
            {
                directories.Add(current);
                var parent = Directory.GetParent(current);
                if (parent is null)
                    break;

                current = parent.FullName;
            }
        }

        private static void PruneEmptyDirectories(
            string outputRootPath,
            HashSet<string> directories)
        {
            foreach (var directory in directories.OrderByDescending(path => path.Length))
            {
                if (!Directory.Exists(directory))
                    continue;

                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    continue;

                if (!HeimdallStaticSitePaths.IsSameOrInsideRoot(outputRootPath, directory) ||
                    HeimdallStaticSitePaths.PathEquals(outputRootPath, directory))
                {
                    continue;
                }

                Directory.Delete(directory);
            }
        }

        private static bool CanFullyCleanOutputRoot(
            string outputRootPath,
            string contentRootPath,
            string? webRootPath)
        {
            if (IsFileSystemRoot(outputRootPath))
                return false;

            if (HeimdallStaticSitePaths.PathEquals(outputRootPath, contentRootPath) ||
                HeimdallStaticSitePaths.IsSameOrInsideRoot(outputRootPath, contentRootPath))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(webRootPath) &&
                (HeimdallStaticSitePaths.PathEquals(outputRootPath, webRootPath) ||
                HeimdallStaticSitePaths.IsSameOrInsideRoot(outputRootPath, webRootPath)))
            {
                return false;
            }

            return true;
        }

        private static bool IsFileSystemRoot(string path)
        {
            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = (Path.GetPathRoot(fullPath) ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteOutputRootContents(string outputRootPath)
        {
            if (!Directory.Exists(outputRootPath))
                return;

            foreach (var file in Directory.EnumerateFiles(outputRootPath))
                File.Delete(file);

            foreach (var directory in Directory.EnumerateDirectories(outputRootPath))
                Directory.Delete(directory, recursive: true);
        }

        private async Task GeneratePageAsync(
            IHeimdallStaticPage page,
            string contentRootPath,
            string outputRootPath,
            string outputFilePath,
            string pathBase,
            List<HeimdallStaticPageOutput> results,
            CancellationToken cancellationToken)
        {
            await using var scope = _services.CreateAsyncScope();
            var context = new HeimdallStaticPageContext(
                scope.ServiceProvider,
                page.Route,
                outputRootPath,
                outputFilePath,
                contentRootPath,
                _webHostEnvironment?.WebRootPath,
                pathBase,
                cancellationToken);

            IHtmlContent html;
            try
            {
                html = await page.RenderAsync(context).ConfigureAwait(false)
                    ?? HtmlString.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to render Heimdall static page '{page.Route}'.",
                    ex);
            }

            var bytes = Utf8NoBom.GetBytes(html.RenderHtml());
            try
            {
                await WriteBytesAsync(PageKind, outputFilePath, bytes, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"Failed to write Heimdall static page '{page.Route}' to '{outputFilePath}'.",
                    ex);
            }

            _logger.LogDebug(
                "Generated Heimdall static page {Route} at {OutputFilePath}.",
                page.Route,
                outputFilePath);

            results.Add(new HeimdallStaticPageOutput(
                page.Route,
                outputFilePath,
                bytes.Length));
        }

        private async Task<IReadOnlyList<HeimdallStaticAssetOutput>> CopyAssetsAsync(
            IReadOnlyList<AssetCopyPlan> assets,
            CancellationToken cancellationToken)
        {
            if (assets.Count == 0)
                return Array.Empty<HeimdallStaticAssetOutput>();

            var results = new List<HeimdallStaticAssetOutput>(assets.Count);

            foreach (var asset in assets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long bytesWritten;
                try
                {
                    if (!_options.Value.OverwriteExistingFiles && File.Exists(asset.OutputPath))
                    {
                        throw new IOException(
                            $"Static asset output '{asset.OutputPath}' already exists and overwriting is disabled.");
                    }

                    bytesWritten = await asset.CopyToAsync(_options.Value.OverwriteExistingFiles, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new IOException(
                        $"Failed to copy Heimdall static asset '{asset.SourcePath}' to '{asset.OutputPath}'.",
                        ex);
                }

                _logger.LogDebug(
                    "Copied Heimdall static asset {SourcePath} to {OutputFilePath}.",
                    asset.SourcePath,
                    asset.OutputPath);

                results.Add(new HeimdallStaticAssetOutput(
                    asset.SourcePath,
                    asset.OutputPath,
                    bytesWritten));
            }

            return results;
        }

        private async Task<IReadOnlyList<HeimdallStaticSupplementalFileOutput>> WriteSupplementalFilesAsync(
            IReadOnlyList<GeneratedFilePlan> files,
            CancellationToken cancellationToken)
        {
            if (files.Count == 0)
                return Array.Empty<HeimdallStaticSupplementalFileOutput>();

            var results = new List<HeimdallStaticSupplementalFileOutput>(files.Count);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] bytes;
                try
                {
                    bytes = Utf8NoBom.GetBytes(file.RenderContent());
                    await WriteBytesAsync(file.Kind, file.OutputPath, bytes, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new IOException(
                        $"Failed to write Heimdall static {file.Kind} file '{file.Route}' to '{file.OutputPath}'.",
                        ex);
                }

                results.Add(new HeimdallStaticSupplementalFileOutput(
                    file.Kind,
                    file.OutputPath,
                    bytes.Length));
            }

            return results;
        }

        private async Task<string?> WriteManifestAsync(
            HeimdallStaticSiteGenerationOptions options,
            string outputRootPath,
            string? manifestFilePath,
            IReadOnlyList<HeimdallStaticPageOutput> pages,
            IReadOnlyList<HeimdallStaticAssetOutput> assets,
            IReadOnlyList<HeimdallStaticSupplementalFileOutput> supplementalFiles,
            CancellationToken cancellationToken)
        {
            if (!options.WriteManifest)
                return null;

            if (string.IsNullOrWhiteSpace(manifestFilePath))
                throw new InvalidOperationException("Static site manifest output path could not be resolved.");

            var manifest = new HeimdallStaticSiteManifest
            {
                SchemaVersion = ManifestSchemaVersion,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                OutputRootPath = outputRootPath,
                ManifestFilePath = manifestFilePath,
                ManifestRelativePath = ToOutputRelativePath(outputRootPath, manifestFilePath),
                Files = CreateManifestFiles(outputRootPath, pages, assets, supplementalFiles)
            };

            var json = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
            var bytes = Utf8NoBom.GetBytes(json + Environment.NewLine);
            try
            {
                await WriteBytesAsync("manifest", manifestFilePath, bytes, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"Failed to write Heimdall static generation manifest '{manifestFilePath}'.",
                    ex);
            }

            _logger.LogDebug("Wrote Heimdall static site manifest {ManifestFilePath}.", manifestFilePath);

            return manifestFilePath;
        }

        private async Task WriteBytesAsync(
            string kind,
            string outputFilePath,
            byte[] bytes,
            CancellationToken cancellationToken)
        {
            if (!_options.Value.OverwriteExistingFiles && File.Exists(outputFilePath))
            {
                throw new IOException(
                    $"Static {kind} output '{outputFilePath}' already exists and overwriting is disabled.");
            }

            var directory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporaryFilePath = CreateTemporaryOutputFilePath(outputFilePath);
            try
            {
                await File.WriteAllBytesAsync(temporaryFilePath, bytes, cancellationToken)
                    .ConfigureAwait(false);

                File.Move(
                    temporaryFilePath,
                    outputFilePath,
                    overwrite: _options.Value.OverwriteExistingFiles);
                temporaryFilePath = null;
            }
            finally
            {
                DeleteTemporaryOutputFileIfNeeded(temporaryFilePath);
            }
        }

        private List<HeimdallStaticSiteManifestFile> CreateManifestFiles(
            string outputRootPath,
            IReadOnlyList<HeimdallStaticPageOutput> pages,
            IReadOnlyList<HeimdallStaticAssetOutput> assets,
            IReadOnlyList<HeimdallStaticSupplementalFileOutput> supplementalFiles)
        {
            var files = new List<HeimdallStaticSiteManifestFile>(
                pages.Count + assets.Count + supplementalFiles.Count);

            files.AddRange(pages.Select(page => new HeimdallStaticSiteManifestFile
            {
                Kind = PageKind,
                Route = page.Route,
                FilePath = page.FilePath,
                RelativePath = ToOutputRelativePath(outputRootPath, page.FilePath),
                BytesWritten = page.BytesWritten
            }));

            files.AddRange(assets.Select(asset => new HeimdallStaticSiteManifestFile
            {
                Kind = AssetKind,
                SourcePath = asset.SourcePath,
                FilePath = asset.FilePath,
                RelativePath = ToOutputRelativePath(outputRootPath, asset.FilePath),
                BytesWritten = asset.BytesWritten
            }));

            files.AddRange(supplementalFiles.Select(file => new HeimdallStaticSiteManifestFile
            {
                Kind = file.Kind,
                FilePath = file.FilePath,
                RelativePath = ToOutputRelativePath(outputRootPath, file.FilePath),
                BytesWritten = file.BytesWritten
            }));

            return files;
        }

        private string RenderSitemap(Uri siteUrl, string sitemapRoute, string pathBase)
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var urls = _pages
                .Select(page => page.Route)
                .Where(IsSitemapEligibleRoute)
                .Select(route => new XElement(
                    ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl(siteUrl, pathBase, ToSitemapUrlPath(route)))));

            var document = new XDocument(new XElement(ns + "urlset", urls));
            _logger.LogDebug("Generated Heimdall sitemap {SitemapRoute}.", sitemapRoute);

            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine +
                document.ToString(SaveOptions.DisableFormatting) +
                Environment.NewLine;
        }

        private static string RenderRobotsTxt(
            HeimdallStaticSiteGenerationOptions options,
            Uri? siteUrl,
            string? sitemapRoute,
            string pathBase)
        {
            if (options.RobotsTxtContent is not null)
                return EnsureTrailingNewLine(options.RobotsTxtContent);

            var builder = new StringBuilder();
            builder.AppendLine("User-agent: *");
            builder.Append("Allow: ");
            builder.AppendLine(HeimdallStaticSitePaths.CombinePathBase(pathBase, "/"));

            if (siteUrl is not null && !string.IsNullOrWhiteSpace(sitemapRoute))
            {
                builder.AppendLine();
                builder.Append("Sitemap: ");
                builder.AppendLine(BuildAbsoluteUrl(siteUrl, pathBase, ToSitemapUrlPath(sitemapRoute)));
            }

            return builder.ToString();
        }

        private static bool IsSitemapEligibleRoute(string route)
        {
            if (string.Equals(route, "/404.html", StringComparison.OrdinalIgnoreCase))
                return false;

            var lastSegment = route.Trim('/').Split('/').LastOrDefault();
            if (string.IsNullOrWhiteSpace(lastSegment) || !Path.HasExtension(lastSegment))
                return true;

            var extension = Path.GetExtension(lastSegment);
            return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToSitemapUrlPath(string route)
        {
            if (route == "/")
                return "/";

            var lastSegment = route.Trim('/').Split('/').LastOrDefault();
            if (!string.IsNullOrWhiteSpace(lastSegment) && Path.HasExtension(lastSegment))
                return route;

            return route.TrimEnd('/') + "/";
        }

        private static Uri ResolveSiteUrl(string? siteUrl)
        {
            if (string.IsNullOrWhiteSpace(siteUrl))
                throw new InvalidOperationException("Static sitemap generation requires options.SiteUrl or options.UseSitemap(siteUrl).");

            if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Static sitemap generation requires an absolute HTTP or HTTPS site URL. Received '{siteUrl}'.");
            }

            return uri;
        }

        private static string BuildAbsoluteUrl(Uri siteUrl, string pathBase, string path)
            => siteUrl.ToString().TrimEnd('/') + "/" +
                HeimdallStaticSitePaths.CombinePathBase(pathBase, path).TrimStart('/');

        private static string EnsureTrailingNewLine(string content)
            => content.EndsWith(Environment.NewLine, StringComparison.Ordinal)
                ? content
                : content + Environment.NewLine;

        private static string CombineProviderPath(string directoryPath, string name)
            => string.IsNullOrWhiteSpace(directoryPath)
                ? name.Replace('\\', '/')
                : $"{directoryPath.TrimEnd('/')}/{name.Replace('\\', '/')}";

        private static void AddAssetPlan(
            IReadOnlyDictionary<string, IHeimdallStaticPage> pageOutputPaths,
            List<AssetCopyPlan> assets,
            Dictionary<string, AssetCopyPlan> assetOutputPaths,
            AssetCopyPlan plan)
        {
            if (pageOutputPaths.TryGetValue(plan.OutputPath, out var page))
            {
                throw new InvalidOperationException(
                    $"Static page '{page.Route}' and static asset '{plan.SourcePath}' both generate '{plan.OutputPath}'.");
            }

            if (assetOutputPaths.TryGetValue(plan.OutputPath, out var existing))
            {
                if (existing.IsSamePhysicalSource(plan))
                    return;

                throw new InvalidOperationException(
                    $"Static assets '{existing.SourcePath}' and '{plan.SourcePath}' both generate '{plan.OutputPath}'.");
            }

            assetOutputPaths.Add(plan.OutputPath, plan);
            assets.Add(plan);
        }

        private static string? ResolveManifestFilePath(
            HeimdallStaticSiteGenerationOptions options,
            string outputRootPath,
            bool force)
        {
            if (!force)
                return null;

            var manifestFileName = string.IsNullOrWhiteSpace(options.ManifestFileName)
                ? "heimdall.static.manifest.json"
                : options.ManifestFileName.Trim();

            return HeimdallStaticSitePaths.ResolveAssetOutputFilePath(outputRootPath, manifestFileName);
        }

        private static string ToOutputRelativePath(string outputRootPath, string filePath)
            => Path.GetRelativePath(outputRootPath, filePath).Replace('\\', '/');

        private static string CreateTemporaryOutputFilePath(string outputFilePath)
        {
            var directory = Path.GetDirectoryName(outputFilePath);
            var fileName = Path.GetFileName(outputFilePath);
            var temporaryFileName = $".{fileName}.{Guid.NewGuid():N}.tmp";

            return string.IsNullOrWhiteSpace(directory)
                ? temporaryFileName
                : Path.Combine(directory, temporaryFileName);
        }

        private static void DeleteTemporaryOutputFileIfNeeded(string? temporaryFilePath)
        {
            if (!string.IsNullOrWhiteSpace(temporaryFilePath) && File.Exists(temporaryFilePath))
                File.Delete(temporaryFilePath);
        }

        private string ResolveContentRootPath()
        {
            var contentRoot = _hostEnvironment?.ContentRootPath;
            if (!string.IsNullOrWhiteSpace(contentRoot))
                return Path.GetFullPath(contentRoot);

            return Path.GetFullPath(Directory.GetCurrentDirectory());
        }

        private sealed class AssetCopyPlan
        {
            private readonly Func<Stream> _openReadStream;

            private AssetCopyPlan(
                string sourcePath,
                string outputPath,
                string? physicalSourcePath,
                long length,
                Func<Stream> openReadStream)
            {
                SourcePath = sourcePath;
                OutputPath = outputPath;
                PhysicalSourcePath = physicalSourcePath;
                Length = length;
                _openReadStream = openReadStream;
            }

            public string SourcePath { get; }

            public string OutputPath { get; }

            public string? PhysicalSourcePath { get; }

            public long Length { get; }

            public static AssetCopyPlan FromPhysicalFile(
                string sourceFilePath,
                string outputFilePath)
            {
                var normalizedSourcePath = Path.GetFullPath(sourceFilePath);
                var info = new FileInfo(normalizedSourcePath);

                return new AssetCopyPlan(
                    normalizedSourcePath,
                    outputFilePath,
                    normalizedSourcePath,
                    info.Length,
                    () => new FileStream(
                        normalizedSourcePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 81920,
                        useAsync: true));
            }

            public static AssetCopyPlan FromFileInfo(
                IFileInfo file,
                string sourceDisplayPath,
                string outputFilePath,
                string? physicalSourcePath)
                => new(
                    physicalSourcePath ?? sourceDisplayPath,
                    outputFilePath,
                    physicalSourcePath,
                    file.Length,
                    file.CreateReadStream);

            public bool IsSamePhysicalSource(AssetCopyPlan other)
                => !string.IsNullOrWhiteSpace(PhysicalSourcePath) &&
                    !string.IsNullOrWhiteSpace(other.PhysicalSourcePath) &&
                    HeimdallStaticSitePaths.PathEquals(PhysicalSourcePath, other.PhysicalSourcePath);

            public async Task<long> CopyToAsync(bool overwrite, CancellationToken cancellationToken)
            {
                var directory = Path.GetDirectoryName(OutputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var temporaryFilePath = CreateTemporaryOutputFilePath(OutputPath);
                try
                {
                    long bytesWritten;
                    await using (var source = _openReadStream())
                    await using (var destination = new FileStream(
                        temporaryFilePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true))
                    {
                        await source.CopyToAsync(destination, cancellationToken)
                            .ConfigureAwait(false);

                        bytesWritten = Length >= 0 ? Length : destination.Length;
                    }

                    File.Move(temporaryFilePath, OutputPath, overwrite);
                    temporaryFilePath = null;

                    return bytesWritten;
                }
                finally
                {
                    DeleteTemporaryOutputFileIfNeeded(temporaryFilePath);
                }
            }
        }

        private sealed record GeneratedFilePlan(
            string Kind,
            string Route,
            string OutputPath,
            Func<string> RenderContent);

        private sealed class HeimdallStaticSiteManifest
        {
            public int SchemaVersion { get; set; }

            public DateTimeOffset GeneratedAtUtc { get; set; }

            public string? OutputRootPath { get; set; }

            public string? ManifestFilePath { get; set; }

            public string? ManifestRelativePath { get; set; }

            public List<HeimdallStaticSiteManifestFile> Files { get; set; } = [];
        }

        private sealed class HeimdallStaticSiteManifestFile
        {
            public string? Kind { get; set; }

            public string? Route { get; set; }

            public string? SourcePath { get; set; }

            public string? FilePath { get; set; }

            public string? RelativePath { get; set; }

            public long BytesWritten { get; set; }
        }
    }
}
