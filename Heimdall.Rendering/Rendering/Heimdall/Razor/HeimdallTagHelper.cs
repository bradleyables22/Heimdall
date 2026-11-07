using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Heimdall.Server.Rendering;

/// <summary>Applies a shared Heimdall fluent attribute configuration to any Razor HTML element.</summary>
/// <remarks>Explicit canonical markup attributes win. The authoring attribute is removed from output.</remarks>
[HtmlTargetElement(Attributes = AttributeName)]
public sealed class HeimdallTagHelper : TagHelper
{
    private const string AttributeName = "heimdall";

    /// <summary>The fluent attribute configuration to apply.</summary>
    [HtmlAttributeName(AttributeName)]
    public FluentHeimdall.HeimdallBuilder? Behavior { get; set; }

    /// <summary>Applies after the standard MVC Tag Helpers.</summary>
    public override int Order => 1000;

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);
        output.Attributes.RemoveAll(AttributeName);
        if (Behavior is not { } behavior) return;

        foreach (var attribute in behavior)
        {
            if (output.Attributes.ContainsName(attribute.Key)) continue;
            output.Attributes.Add(new TagHelperAttribute(attribute.Key, attribute.Value,
                attribute.Value is true ? HtmlAttributeValueStyle.Minimized : HtmlAttributeValueStyle.DoubleQuotes));
        }

        if (behavior.FallbackText is not { } fallback || output.TagMode == TagMode.SelfClosing) return;
        var child = output.Content.IsModified ? output.Content : await output.GetChildContentAsync();
        if (child.IsEmptyOrWhiteSpace) output.Content.SetContent(fallback);
    }
}
