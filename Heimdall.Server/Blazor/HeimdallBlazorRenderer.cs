using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Server
{
    internal sealed class HeimdallBlazorRenderer(
        IServiceProvider services,
        ILoggerFactory loggerFactory,
        IHttpContextAccessor httpContextAccessor) : IHeimdallBlazorRenderer
    {
        private bool _navigationInitialized;

        public Task<IHtmlContent> RenderAsync<TComponent>(
            CancellationToken cancellationToken = default)
            where TComponent : IComponent
            => RenderAsync<TComponent>(ParameterView.Empty, cancellationToken);

        public Task<IHtmlContent> RenderAsync<TComponent>(
            object? parameters,
            CancellationToken cancellationToken = default)
            where TComponent : IComponent
            => RenderAsync<TComponent>(CreateParameterView(parameters), cancellationToken);

        public Task<IHtmlContent> RenderAsync<TComponent>(
            ParameterView parameters,
            CancellationToken cancellationToken = default)
            where TComponent : IComponent
            => RenderAsync(typeof(TComponent), parameters, cancellationToken);

        public Task<IHtmlContent> RenderAsync(
            Type componentType,
            object? parameters = null,
            CancellationToken cancellationToken = default)
            => RenderAsync(componentType, CreateParameterView(parameters), cancellationToken);

        private async Task<IHtmlContent> RenderAsync(
            Type componentType,
            ParameterView parameters,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(componentType);
            if (!typeof(IComponent).IsAssignableFrom(componentType))
            {
                throw new ArgumentException(
                    $"The component type '{componentType.FullName}' must implement {typeof(IComponent).FullName}.",
                    nameof(componentType));
            }

            cancellationToken.ThrowIfCancellationRequested();
            InitializeRequestNavigation();

            await using var renderer = new HtmlRenderer(services, loggerFactory);
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var root = await renderer.RenderComponentAsync(componentType, parameters);
                return root.ToHtmlString();
            });

            cancellationToken.ThrowIfCancellationRequested();
            return new HtmlString(html);
        }

        private void InitializeRequestNavigation()
        {
            if (_navigationInitialized)
                return;

            var context = httpContextAccessor.HttpContext;
            var navigation = services.GetService<NavigationManager>() as IHostEnvironmentNavigationManager;
            if (context is null || navigation is null)
                return;

            var request = context.Request;
            var origin = $"{request.Scheme}://{request.Host}";
            var pathBase = request.PathBase.HasValue ? request.PathBase.Value!.TrimEnd('/') : string.Empty;
            var baseUri = $"{origin}{pathBase}/";
            var uri = $"{origin}{pathBase}{request.Path}{request.QueryString}";

            navigation.Initialize(baseUri, uri);
            _navigationInitialized = true;
        }

        private static ParameterView CreateParameterView(object? parameters)
        {
            if (parameters is null)
                return ParameterView.Empty;

            if (parameters is ParameterView parameterView)
                return parameterView;

            if (parameters is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                return ParameterView.FromDictionary(
                    new Dictionary<string, object?>(readOnlyDictionary, StringComparer.Ordinal));
            }

            if (parameters is IDictionary dictionary)
                return ParameterView.FromDictionary(ToParameterDictionary(dictionary));

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in parameters.GetType()
                         .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0))
            {
                values.Add(property.Name, property.GetValue(parameters));
            }

            return ParameterView.FromDictionary(values);
        }

        private static Dictionary<string, object?> ToParameterDictionary(IDictionary parameters)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (DictionaryEntry entry in parameters)
            {
                if (entry.Key is not string name)
                {
                    throw new ArgumentException(
                        "Blazor component parameter dictionary keys must be strings.",
                        nameof(parameters));
                }

                result.Add(name, entry.Value);
            }

            return result;
        }
    }
}
