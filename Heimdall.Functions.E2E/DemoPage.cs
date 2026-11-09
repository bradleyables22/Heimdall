using Microsoft.AspNetCore.Html;
using System.Text.Json;

namespace Heimdall.Functions.E2E;

public static class DemoPage
{
    public static IHtmlContent Render(string? key = null)
    {
        var markup = """
        <!doctype html>
        <html lang="en"><head><meta charset="utf-8"><title>Heimdall Functions E2E</title>
        <script src="/api/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
        <script>
        Object.assign(Heimdall.config.endpoints, {
          contentInvocations: '/api/__heimdall/v1/content/invocations',
          csrf: '/api/__heimdall/v1/csrf',
          bifrostToken: '/api/__heimdall/v1/bifrost/token',
          bifrost: '/api/__heimdall/v1/bifrost'
        });
        </script></head><body>
        <h1>Heimdall Functions</h1>
        <form id="counter-form">
          <input name="count" value="4">
          <button type="button" id="increment" heimdall-content-click="functions.increment"
            heimdall-payload-from="closest-form" heimdall-content-target="#counter" heimdall-content-swap="inner">Increment</button>
        </form><output id="counter">0</output>
        <form id="upload-form" enctype="multipart/form-data"
          heimdall-content-submit="functions.upload" heimdall-payload-from="closest-form"
          heimdall-content-target="#upload-result" heimdall-content-swap="inner">
          <input type="file" name="file"><button type="submit">Upload</button>
        </form><output id="upload-result"></output>
        <div id="live" heimdall-sse-topic="functions-e2e" heimdall-sse-swap="inner">Waiting</div>
        <button id="publish" heimdall-content-click="functions.publish" heimdall-content-swap="none">Publish</button>
        <button id="directives" heimdall-content-click="functions.directives" heimdall-content-target="#counter" heimdall-content-swap="inner">Directives</button>
        <div id="notice"></div>
        <button id="mvc" heimdall-content-click="functions.mvc" heimdall-content-target="#renderer" heimdall-content-swap="inner">MVC</button>
        <button id="blazor" heimdall-content-click="functions.blazor" heimdall-content-target="#renderer" heimdall-content-swap="inner">Blazor</button>
        <div id="renderer"></div>
        </body></html>
        """;
        if (key is not null)
        {
            markup = markup.Replace("/api/__heimdall/", "/api/keyed/__heimdall/");
            var script = "const key = " + JsonSerializer.Serialize(key) + ";" +
                "Heimdall.config.requestHeaders = c => new URL(c.url).origin === location.origin ? {'x-functions-key': key} : {};" +
                "Heimdall.config.endpoints.bifrost += '?code=' + encodeURIComponent(key);";
            markup = markup.Replace("</script></head>", script + "</script></head>");
        }
        return new HtmlString(markup);
    }
}
