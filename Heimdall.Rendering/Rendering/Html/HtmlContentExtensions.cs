using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides serialization helpers for rendered HTML content.
	/// </summary>
	public static class HtmlContentExtensions
	{
		/// <summary>
		/// Renders HTML content to a string using the supplied encoder.
		/// </summary>
		/// <param name="content">The HTML content to render.</param>
		/// <param name="encoder">
		/// The encoder used while rendering. When omitted, <see cref="HtmlEncoder.Default"/> is used.
		/// </param>
		/// <returns>The rendered HTML string.</returns>
		public static string ToHtmlString(
			this IHtmlContent content,
			HtmlEncoder? encoder = null)
		{
			ArgumentNullException.ThrowIfNull(content);

			using var writer = new StringWriter();
			content.WriteTo(writer, encoder ?? HtmlEncoder.Default);
			return writer.ToString();
		}

		/// <summary>
		/// Renders HTML content to a string, honoring <see cref="IHtmlAsyncContent"/> when provided.
		/// </summary>
		/// <param name="content">The HTML content to render.</param>
		/// <param name="cancellationToken">The token used to cancel the asynchronous rendering operation.</param>
		/// <returns>The rendered HTML string.</returns>
		public static async ValueTask<string> ToHtmlStringAsync(
			this IHtmlContent content,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(content);
			cancellationToken.ThrowIfCancellationRequested();

			using var writer = new StringWriter();
			if (content is IHtmlAsyncContent asyncContent)
				await asyncContent.WriteToAsync(writer).ConfigureAwait(false);
			else
				content.WriteTo(writer, HtmlEncoder.Default);

			cancellationToken.ThrowIfCancellationRequested();
			return writer.ToString();
		}
	}
}
