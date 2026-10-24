using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

namespace Heimdall.Server.Tests;

internal sealed class AsyncTestHtmlContent : IHtmlAsyncContent
{
    private readonly string _syncMarkup;
    private readonly string _asyncMarkup;

    public AsyncTestHtmlContent(string syncMarkup, string asyncMarkup)
    {
        _syncMarkup = syncMarkup;
        _asyncMarkup = asyncMarkup;
    }

    public void WriteTo(TextWriter writer, HtmlEncoder encoder)
        => writer.Write(_syncMarkup);

    public async ValueTask WriteToAsync(TextWriter writer)
    {
        await Task.Yield();
        await writer.WriteAsync(_asyncMarkup);
    }
}

internal sealed class AsyncOnlyTestHtmlContent(string markup) : IHtmlAsyncContent
{
    public void WriteTo(TextWriter writer, HtmlEncoder encoder)
        => throw new InvalidOperationException("The synchronous writer must not be used.");

    public async ValueTask WriteToAsync(TextWriter writer)
    {
        await Task.Yield();
        await writer.WriteAsync(markup);
    }
}
