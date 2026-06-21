namespace Heimdall.Server
{
    /// <summary>
    /// Configures Heimdall static site generation.
    /// </summary>
    public sealed class HeimdallStaticSiteGenerationOptions
    {
        /// <summary>
        /// Gets or sets the output directory used for generated static files.
        /// Relative paths are resolved from <see cref="OutputRoot"/>.
        /// </summary>
        public string OutputPath { get; set; } = "dist";

        /// <summary>
        /// Gets or sets the base directory used to resolve relative output paths.
        /// </summary>
        public HeimdallStaticSiteOutputRoot OutputRoot { get; set; } = HeimdallStaticSiteOutputRoot.ContentRoot;

        /// <summary>
        /// Gets or sets whether existing generated files may be overwritten.
        /// </summary>
        public bool OverwriteExistingFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets whether files from the application's physical web root should be copied to the output root.
        /// </summary>
        public bool CopyWebRootAssets { get; set; } = true;

        /// <summary>
        /// Gets or sets whether static web assets exposed under <c>/_content</c> should be copied to the output root.
        /// </summary>
        public bool CopyStaticWebAssets { get; set; } = true;

        /// <summary>
        /// Gets or sets whether stale generated output should be removed before generation.
        /// </summary>
        public bool CleanOutputPath { get; set; }

        /// <summary>
        /// Gets or sets whether a generation manifest should be written to the output root.
        /// </summary>
        public bool WriteManifest { get; set; } = true;

        /// <summary>
        /// Gets or sets the manifest file name written beneath the output root.
        /// </summary>
        public string ManifestFileName { get; set; } = "heimdall.static.manifest.json";

        /// <summary>
        /// Gets or sets the canonical site URL used for generated sitemap and robots.txt entries.
        /// </summary>
        public string? SiteUrl { get; set; }

        /// <summary>
        /// Gets or sets the public path base used when generating rooted URLs.
        /// Use <c>"/"</c> for domain-root deployments or a value such as <c>"/app"</c> for subdirectory hosting.
        /// </summary>
        public string PathBase { get; set; } = "/";

        /// <summary>
        /// Gets or sets whether a sitemap XML file should be generated.
        /// </summary>
        public bool GenerateSitemap { get; set; }

        /// <summary>
        /// Gets or sets the sitemap route.
        /// </summary>
        public string SitemapPath { get; set; } = "/sitemap.xml";

        /// <summary>
        /// Gets or sets whether a robots.txt file should be generated.
        /// </summary>
        public bool GenerateRobotsTxt { get; set; }

        /// <summary>
        /// Gets or sets the robots.txt route.
        /// </summary>
        public string RobotsTxtPath { get; set; } = "/robots.txt";

        /// <summary>
        /// Gets or sets custom robots.txt content. When unset, Heimdall writes a permissive default.
        /// </summary>
        public string? RobotsTxtContent { get; set; }

        /// <summary>
        /// Configures generation to write directly to the ASP.NET Core web root.
        /// </summary>
        /// <param name="outputPath">
        /// An optional path beneath the web root. Use <c>"."</c> to write to the web root itself.
        /// </param>
        public void UseWebRootPath(string outputPath = ".")
        {
            OutputRoot = HeimdallStaticSiteOutputRoot.WebRoot;
            OutputPath = outputPath;
        }

        /// <summary>
        /// Configures generation to resolve output from the application content root.
        /// </summary>
        /// <param name="outputPath">
        /// An optional path beneath the content root.
        /// </param>
        public void UseContentRootPath(string outputPath = "dist")
        {
            OutputRoot = HeimdallStaticSiteOutputRoot.ContentRoot;
            OutputPath = outputPath;
        }

        /// <summary>
        /// Configures the public path base for generated rooted links and supplemental files.
        /// </summary>
        /// <param name="pathBase">The public path base, such as <c>"/"</c> or <c>"/app"</c>.</param>
        public void UsePathBase(string pathBase)
        {
            PathBase = pathBase;
        }

        /// <summary>
        /// Enables sitemap generation.
        /// </summary>
        /// <param name="siteUrl">The canonical absolute site URL.</param>
        /// <param name="sitemapPath">The sitemap route.</param>
        public void UseSitemap(string siteUrl, string sitemapPath = "/sitemap.xml")
        {
            SiteUrl = siteUrl;
            SitemapPath = sitemapPath;
            GenerateSitemap = true;
        }

        /// <summary>
        /// Enables robots.txt generation.
        /// </summary>
        /// <param name="content">Optional custom robots.txt content.</param>
        /// <param name="robotsTxtPath">The robots.txt route.</param>
        public void UseRobotsTxt(string? content = null, string robotsTxtPath = "/robots.txt")
        {
            RobotsTxtContent = content;
            RobotsTxtPath = robotsTxtPath;
            GenerateRobotsTxt = true;
        }
    }
}
