using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Heimdall.Server
{
    /// <summary>
    /// Renders MVC partial views from within Heimdall content actions.
    /// </summary>
    public interface IHeimdallMvcRenderer
    {
        /// <summary>
        /// Renders an MVC partial view using the current HTTP request context.
        /// </summary>
        /// <param name="viewName">The partial view name or application-relative path.</param>
        /// <param name="model">The model supplied to the partial view.</param>
        /// <param name="cancellationToken">A token used to observe cancellation before rendering starts.</param>
        /// <returns>The rendered partial view as trusted HTML content.</returns>
        Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Renders an MVC partial view using the current HTTP request context and custom view data.
        /// </summary>
        /// <param name="viewName">The partial view name or application-relative path.</param>
        /// <param name="model">The model supplied to the partial view.</param>
        /// <param name="configureViewData">A callback that can populate the view data dictionary before rendering.</param>
        /// <param name="cancellationToken">A token used to observe cancellation before rendering starts.</param>
        /// <returns>The rendered partial view as trusted HTML content.</returns>
        Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model,
            Action<ViewDataDictionary> configureViewData,
            CancellationToken cancellationToken = default);
    }
}
