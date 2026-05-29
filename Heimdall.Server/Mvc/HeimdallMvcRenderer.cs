using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace Heimdall.Server
{
    internal sealed class HeimdallMvcRenderer(
        IHttpContextAccessor httpContextAccessor,
        ICompositeViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IModelMetadataProvider metadataProvider) : IHeimdallMvcRenderer
    {
        public Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model = null,
            CancellationToken cancellationToken = default)
            => RenderPartialAsync(viewName, model, configureViewData: null, cancellationToken);

        public Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model,
            Action<ViewDataDictionary> configureViewData,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configureViewData);
            return RenderPartialAsync(viewName, model, configureViewData, cancellationToken);
        }

        private async Task<IHtmlContent> RenderPartialAsync(
            string viewName,
            object? model,
            Action<ViewDataDictionary>? configureViewData,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
            cancellationToken.ThrowIfCancellationRequested();

            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "Unable to render an MVC partial view because there is no active HttpContext.");

            var actionContext = CreateActionContext(httpContext);
            var view = ResolveView(actionContext, viewName);
            var viewData = new ViewDataDictionary(metadataProvider, new ModelStateDictionary())
            {
                Model = model
            };
            configureViewData?.Invoke(viewData);

            var tempData = new TempDataDictionary(httpContext, tempDataProvider);

            using var writer = new StringWriter();
            var viewContext = new ViewContext(
                actionContext,
                view,
                viewData,
                tempData,
                writer,
                new HtmlHelperOptions());

            await view.RenderAsync(viewContext);
            cancellationToken.ThrowIfCancellationRequested();

            return new HtmlString(writer.ToString());
        }

        private static ActionContext CreateActionContext(HttpContext httpContext)
        {
            var routeData = httpContext.GetRouteData() ?? new RouteData();
            return new ActionContext(httpContext, routeData, new ActionDescriptor());
        }

        private IView ResolveView(ActionContext actionContext, string viewName)
        {
            var getViewResult = viewEngine.GetView(
                executingFilePath: null,
                viewPath: viewName,
                isMainPage: false);
            if (getViewResult.Success)
                return getViewResult.View!;

            var findViewResult = viewEngine.FindView(
                actionContext,
                viewName,
                isMainPage: false);
            if (findViewResult.Success)
                return findViewResult.View!;

            throw CreateMissingViewException(viewName, getViewResult, findViewResult);
        }

        private static InvalidOperationException CreateMissingViewException(
            string viewName,
            params ViewEngineResult[] results)
        {
            var searchedLocations = results
                .SelectMany(x => x.SearchedLocations ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (searchedLocations.Length == 0)
            {
                return new InvalidOperationException(
                    $"Unable to find MVC partial view '{viewName}'. No search locations were reported.");
            }

            var searched = string.Join(
                Environment.NewLine,
                searchedLocations.Select(x => $"  {x}"));

            return new InvalidOperationException(
                $"Unable to find MVC partial view '{viewName}'. Searched locations:" +
                $"{Environment.NewLine}{searched}");
        }
    }
}
