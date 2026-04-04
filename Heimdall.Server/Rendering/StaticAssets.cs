using Microsoft.AspNetCore.Html;
using System.Collections.Concurrent;
using System.Text.Encodings.Web;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides discovery and cached access to static markup assets stored on disk.
	/// </summary>
	/// <remarks>
	/// This class scans a root directory, maps relative asset keys to physical file paths,
	/// and caches loaded markup so repeated requests can be served efficiently.
	/// </remarks>
	public static class StaticAssets
	{
		private static readonly ConcurrentDictionary<string, IHtmlContent> _cache = new();
		private static readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Scans the specified root directory and registers all discovered files for later lookup.
		/// </summary>
		/// <param name="root">The root directory that contains the static assets.</param>
		public static void Discover(string root)
		{
			foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(root, file)
					.Replace('\\', '/');

				_paths[relative] = file;
			}
		}

		/// <summary>
		/// Returns the cached HTML content for the specified asset key.
		/// </summary>
		/// <param name="key">The relative asset key to load.</param>
		/// <returns>
		/// The cached HTML content for the requested asset when found; otherwise, an empty HTML result.
		/// </returns>
		public static IHtmlContent Get(string key)
		{
			return _cache.GetOrAdd(key, Load);
		}

		/// <summary>
		/// Loads the specified asset from disk and wraps it as trusted HTML content.
		/// </summary>
		/// <param name="key">The relative asset key to load.</param>
		/// <returns>
		/// A trusted HTML content instance for the requested asset when found; otherwise, <see cref="HtmlString.Empty"/>.
		/// </returns>
		private static IHtmlContent Load(string key)
		{
			if (!_paths.TryGetValue(key, out var path))
				return HtmlString.Empty;

			var markup = File.ReadAllText(path);
			return new TrustedMarkup(markup);
		}

		/// <summary>
		/// Represents pre-rendered markup that should be written directly to the response without additional encoding.
		/// </summary>
		private sealed class TrustedMarkup : IHtmlContent
		{
			private readonly string _markup;

			/// <summary>
			/// Initializes a new instance of the <see cref="TrustedMarkup"/> class.
			/// </summary>
			/// <param name="markup">The markup content to write directly to the output.</param>
			public TrustedMarkup(string markup)
			{
				_markup = markup;
			}

			/// <summary>
			/// Writes the stored markup directly to the specified writer.
			/// </summary>
			/// <param name="writer">The writer that receives the markup output.</param>
			/// <param name="encoder">The HTML encoder supplied by the rendering pipeline.</param>
			public void WriteTo(TextWriter writer, HtmlEncoder encoder)
			{
				writer.Write(_markup);
			}
		}
	}
}