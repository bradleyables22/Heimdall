using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering;

/// <summary>Provides fluent Heimdall attributes and renderable content for Blazor and Razor views.</summary>
/// <remarks>
/// Standalone attribute chains are immutable and emit canonical long-form names. Use them with
/// Blazor's <c>@attributes</c> or the Razor <c>heimdall</c> Tag Helper.
/// </remarks>
public static partial class Heimdall
{
    /// <summary>Starts an empty, reusable standalone attribute configuration.</summary>
    public static FluentHeimdall.HeimdallBuilder Attributes => default;

    /// <summary>Builds renderable HTML using the complete Heimdall fragment fluent API.</summary>
    public static IHtmlContent Fragment(Action<FluentHeimdall.HeimdallFragmentBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return FluentHtml.Fragment(fragment => build(fragment.Heimdall()));
    }

    /// <summary>Builds a Heimdall fragment with asynchronous content preparation.</summary>
    public static ValueTask<IHtmlContent> Fragment(Func<FluentHeimdall.HeimdallFragmentBuilder, ValueTask> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return FluentHtml.Fragment(async fragment => await build(fragment.Heimdall()).ConfigureAwait(false));
    }

    /// <summary>Adapts trusted, typed HTML content for rendering in a Blazor component.</summary>
    /// <remarks>Pass encoded content produced by Heimdall rendering helpers, not untrusted raw HTML.</remarks>
    public static MarkupString Markup(IHtmlContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new MarkupString(content.ToHtmlString());
    }

    /// <summary>Materializes asynchronous HTML content for rendering in a Blazor component.</summary>
    public static async Task<MarkupString> MarkupAsync(IHtmlContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new MarkupString(await content.ToHtmlStringAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Renders a localizable span with canonical attributes and server fallback text.</summary>
    public static IHtmlContent LocalizedTime(DateTimeOffset value, string format = "G")
        => FluentHtml.Span(span => span.LocalizeTime(value, format));

    /// <summary>Renders an absolute UTC or local time with a server fallback.</summary>
    public static IHtmlContent LocalizedTime(DateTime value, string format = "G")
        => FluentHtml.Span(span => span.LocalizeTime(value, format));
}
