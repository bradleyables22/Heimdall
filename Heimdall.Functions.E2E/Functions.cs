using Heimdall.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Functions.E2E;

public sealed class Functions(HeimdallFunctions heimdall, DemoStreamControl streams, HeimdallFunctionAssets assets,
    HeimdallFunctionStaticWebAssets staticWebAssets, IConfiguration configuration)
{
    [Function("Heimdall")]
    public Task<IActionResult> Pipeline(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "__heimdall/v1/{*path}")] HttpRequest request,
        string? path) => path == "bifrost"
            ? streams.RunAsync(request.HttpContext, () => heimdall.HandleAsync(request, path))
            : heimdall.HandleAsync(request, path);

    [Function("Home")]
    public Task<IActionResult> Home(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "demo")] HttpRequest request)
        => heimdall.RenderPageAsync(request, (_, _) => Task.FromResult(DemoPage.Render()));

    [Function("Runtime")]
    public IActionResult Runtime(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", Route = "assets/{*path}")] HttpRequest request,
        string? path) => assets.Serve(request, path);

    [Function("RclAssets")]
    public async Task<IActionResult> RclAssets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", Route = "_content/{*path}")] HttpRequest request,
        string? path) => await staticWebAssets.TryServeAsync(request, "_content/" + path) ?? new NotFoundResult();

    [Function("KeyedHeimdall")]
    public Task<IActionResult> KeyedPipeline(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "keyed/__heimdall/v1/{*path}")] HttpRequest request,
        string? path) => path == "bifrost"
            ? streams.RunAsync(request.HttpContext, () => heimdall.HandleAsync(request, path))
            : heimdall.HandleAsync(request, path);

    [Function("KeyedHome")]
    public Task<IActionResult> KeyedHome(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "keyed/demo")] HttpRequest request)
        => heimdall.RenderPageAsync(request, (_, _) => Task.FromResult(DemoPage.Render(configuration["E2EFunctionKey"])));
}
