using Microsoft.AspNetCore.Html;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Heimdall.Server.Rendering;

public static partial class HeimdallHtml
{
    private static readonly Regex PluginExtensionName = new(
        "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Defines when a registered browser plugin command executes.</summary>
    public enum PluginCommandTiming
    {
        /// <summary>Runs before response swaps and mutations.</summary>
        Before,
        /// <summary>Runs after response swaps and target behavior initialization.</summary>
        After
    }

    /// <summary>Emits the default attribute for a fully qualified plugin trigger name.</summary>
    public static Html.HtmlAttr OnPluginTrigger(string name, InvocationId invocation)
        => Html.Attr("heimdall-content-" + ValidatePluginExtensionName(name).Replace('.', '-'), invocation.Value);

    /// <summary>Reads a payload using a fully qualified, registered browser plugin payload source.</summary>
    public static Html.HtmlAttr PayloadFromPlugin(string name)
        => Html.Attr("heimdall-payload-from", "plugin:" + ValidatePluginExtensionName(name));

    /// <summary>Emits a registered browser plugin command with encoded JSON arguments.</summary>
    /// <remarks>
    /// Names include the owning plugin id. Without a target the handler is called once with no element.
    /// With a target the first match is used, or every match when allTargets is true.
    /// JSON property names follow web defaults. This directive also works in Bifrost payloads.
    /// </remarks>
    public static IHtmlContent PluginCommand(
        string name,
        object? args = null,
        string? targetSelector = null,
        PluginCommandTiming timing = PluginCommandTiming.After,
        bool allTargets = false)
    {
        ValidatePluginExtensionName(name);
        if (targetSelector is not null && string.IsNullOrWhiteSpace(targetSelector))
            throw new ArgumentException("A target selector cannot be empty.", nameof(targetSelector));
        if (allTargets && targetSelector is null)
            throw new ArgumentException("allTargets requires a target selector.", nameof(allTargets));

        return Html.Tag("heimdall-command",
            Html.Attr("name", name),
            Html.Attr("args", JsonSerializer.Serialize(args ?? new { }, JsonSerializerOptions.Web)),
            Html.Attr("timing", timing switch
            {
                PluginCommandTiming.Before => "before",
                PluginCommandTiming.After => "after",
                _ => throw new ArgumentOutOfRangeException(nameof(timing))
            }),
            targetSelector is null ? null : Html.Attr("target", targetSelector),
            allTargets ? Html.Attr("all", "true") : null);
    }

    private static string ValidatePluginExtensionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 257 ||
            !name.Contains('.') || !PluginExtensionName.IsMatch(name))
            throw new ArgumentException("A fully qualified lowercase plugin extension name is required.", nameof(name));
        return name;
    }
}
