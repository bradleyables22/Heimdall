namespace Heimdall.Server
{
    internal static class HeimdallStaticSitePaths
    {
        private static readonly char[] InvalidRouteSegmentChars = Path.GetInvalidFileNameChars();

        public static string NormalizeRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Static page route is required.", nameof(route));

            var normalized = route.Trim().Replace('\\', '/');

            if (normalized.Contains("?", StringComparison.Ordinal) ||
                normalized.Contains("#", StringComparison.Ordinal))
            {
                throw new ArgumentException("Static page routes cannot include query strings or fragments.", nameof(route));
            }

            while (normalized.Contains("//", StringComparison.Ordinal))
                normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

            if (!normalized.StartsWith("/", StringComparison.Ordinal))
                normalized = "/" + normalized;

            if (normalized.Length > 1)
                normalized = normalized.TrimEnd('/');

            var segments = normalized.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var segment in segments)
            {
                if (segment is "." or "..")
                    throw new ArgumentException("Static page routes cannot contain '.' or '..' path segments.", nameof(route));

                if (segment.IndexOfAny(InvalidRouteSegmentChars) >= 0)
                    throw new ArgumentException($"Static page route segment '{segment}' contains invalid file path characters.", nameof(route));
            }

            return normalized;
        }

        public static string ResolveOutputRootPath(
            HeimdallStaticSiteGenerationOptions options,
            string contentRootPath,
            string? webRootPath)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            var configured = string.IsNullOrWhiteSpace(options.OutputPath)
                ? ResolveDefaultOutputPath(options.OutputRoot)
                : options.OutputPath.Trim();

            var root = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(ResolveOutputBasePath(options.OutputRoot, contentRootPath, webRootPath), configured);

            return Path.GetFullPath(root);
        }

        public static string NormalizePathBase(string? pathBase)
        {
            if (string.IsNullOrWhiteSpace(pathBase))
                return "/";

            var normalized = pathBase.Trim().Replace('\\', '/');
            if (normalized == "/")
                return "/";

            if (normalized.Contains("?", StringComparison.Ordinal) ||
                normalized.Contains("#", StringComparison.Ordinal))
            {
                throw new ArgumentException("Static site path base cannot include query strings or fragments.", nameof(pathBase));
            }

            if (Uri.TryCreate(normalized, UriKind.Absolute, out _))
                throw new ArgumentException("Static site path base must be an application path, not an absolute URL.", nameof(pathBase));

            while (normalized.Contains("//", StringComparison.Ordinal))
                normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

            if (!normalized.StartsWith("/", StringComparison.Ordinal))
                normalized = "/" + normalized;

            normalized = normalized.TrimEnd('/');

            var segments = normalized.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var segment in segments)
            {
                if (segment is "." or "..")
                    throw new ArgumentException("Static site path base cannot contain '.' or '..' path segments.", nameof(pathBase));

                if (segment.IndexOfAny(InvalidRouteSegmentChars) >= 0)
                    throw new ArgumentException($"Static site path base segment '{segment}' contains invalid file path characters.", nameof(pathBase));
            }

            return normalized.Length == 0 ? "/" : normalized;
        }

        public static string CombinePathBase(string pathBase, string path)
        {
            var normalizedPathBase = NormalizePathBase(pathBase);

            if (string.IsNullOrWhiteSpace(path))
                return normalizedPathBase == "/" ? "/" : normalizedPathBase + "/";

            var normalizedPath = path.Trim().Replace('\\', '/');

            if (normalizedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith("//", StringComparison.Ordinal))
            {
                return normalizedPath;
            }

            if (normalizedPath.StartsWith("#", StringComparison.Ordinal) ||
                normalizedPath.StartsWith("?", StringComparison.Ordinal))
            {
                return normalizedPath;
            }

            var trimmedPath = normalizedPath.TrimStart('/');
            if (string.IsNullOrEmpty(trimmedPath))
                return normalizedPathBase == "/" ? "/" : normalizedPathBase + "/";

            return normalizedPathBase == "/"
                ? "/" + trimmedPath
                : normalizedPathBase + "/" + trimmedPath;
        }

        private static string ResolveDefaultOutputPath(HeimdallStaticSiteOutputRoot outputRoot)
            => outputRoot == HeimdallStaticSiteOutputRoot.WebRoot ? "." : "dist";

        private static string ResolveOutputBasePath(
            HeimdallStaticSiteOutputRoot outputRoot,
            string contentRootPath,
            string? webRootPath)
        {
            if (outputRoot == HeimdallStaticSiteOutputRoot.ContentRoot)
                return contentRootPath;

            if (outputRoot == HeimdallStaticSiteOutputRoot.WebRoot)
            {
                if (!string.IsNullOrWhiteSpace(webRootPath))
                    return webRootPath;

                throw new InvalidOperationException(
                    "Static site generation is configured to use the ASP.NET Core web root, " +
                    "but IWebHostEnvironment.WebRootPath is not available. " +
                    "Use a WebApplication host or configure OutputPath with an absolute path.");
            }

            throw new InvalidOperationException($"Unsupported static site output root '{outputRoot}'.");
        }

        public static string ResolveOutputFilePath(string outputRootPath, string normalizedRoute)
        {
            var relativePath = RouteToRelativeOutputPath(normalizedRoute);
            var fullPath = Path.GetFullPath(Path.Combine(outputRootPath, relativePath));

            EnsurePathStaysInsideRoot(outputRootPath, fullPath);
            return fullPath;
        }

        public static string ResolveAssetOutputFilePath(string outputRootPath, string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(outputRootPath, relativePath));
            EnsurePathStaysInsideRoot(outputRootPath, fullPath);
            return fullPath;
        }

        public static bool PathEquals(string first, string second)
            => string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        public static bool IsSameOrInsideRoot(string rootPath, string candidatePath)
        {
            var root = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(candidatePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
                return true;

            return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string RouteToRelativeOutputPath(string normalizedRoute)
        {
            if (normalizedRoute == "/")
                return "index.html";

            var segments = normalizedRoute
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            var last = segments[^1];
            if (Path.HasExtension(last))
                return Path.Combine(segments);

            return Path.Combine(segments.Append("index.html").ToArray());
        }

        private static void EnsurePathStaysInsideRoot(string outputRootPath, string filePath)
        {
            if (!IsSameOrInsideRoot(outputRootPath, filePath))
            {
                throw new InvalidOperationException(
                    $"Generated static file path '{Path.GetFullPath(filePath)}' is outside the output root '{outputRootPath}'.");
            }
        }
    }
}
