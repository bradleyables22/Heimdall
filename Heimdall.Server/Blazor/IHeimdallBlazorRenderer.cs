using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server
{
    /// <summary>
    /// Renders Blazor components non-interactively as trusted HTML content.
    /// </summary>
    /// <remarks>
    /// This renderer performs the component's static render pass. It supports parameters,
    /// dependency injection, and asynchronous initialization, but it does not create a
    /// Blazor circuit or enable component event handlers.
    /// </remarks>
    public interface IHeimdallBlazorRenderer
    {
        /// <summary>
        /// Renders a component using its default parameters.
        /// </summary>
        /// <typeparam name="TComponent">The component type to render.</typeparam>
        /// <param name="cancellationToken">A token used to observe cancellation before and after rendering.</param>
        /// <returns>The rendered component as trusted HTML content.</returns>
        Task<IHtmlContent> RenderAsync<TComponent>(
            CancellationToken cancellationToken = default)
            where TComponent : IComponent;

        /// <summary>
        /// Renders a component using parameters supplied by an anonymous object,
        /// dictionary, or <see cref="ParameterView"/>.
        /// </summary>
        /// <typeparam name="TComponent">The component type to render.</typeparam>
        /// <param name="parameters">Component parameters, or <see langword="null"/> for no parameters.</param>
        /// <param name="cancellationToken">A token used to observe cancellation before and after rendering.</param>
        /// <returns>The rendered component as trusted HTML content.</returns>
        Task<IHtmlContent> RenderAsync<TComponent>(
            object? parameters,
            CancellationToken cancellationToken = default)
            where TComponent : IComponent;

        /// <summary>
        /// Renders a component using a native Blazor parameter view.
        /// </summary>
        /// <typeparam name="TComponent">The component type to render.</typeparam>
        /// <param name="parameters">The parameters supplied to the component.</param>
        /// <param name="cancellationToken">A token used to observe cancellation before and after rendering.</param>
        /// <returns>The rendered component as trusted HTML content.</returns>
        Task<IHtmlContent> RenderAsync<TComponent>(
            ParameterView parameters,
            CancellationToken cancellationToken = default)
            where TComponent : IComponent;

        /// <summary>
        /// Renders a component type using parameters supplied by an anonymous object,
        /// dictionary, or <see cref="ParameterView"/>.
        /// </summary>
        /// <param name="componentType">The component type to render.</param>
        /// <param name="parameters">Component parameters, or <see langword="null"/> for no parameters.</param>
        /// <param name="cancellationToken">A token used to observe cancellation before and after rendering.</param>
        /// <returns>The rendered component as trusted HTML content.</returns>
        Task<IHtmlContent> RenderAsync(
            Type componentType,
            object? parameters = null,
            CancellationToken cancellationToken = default);
    }
}
