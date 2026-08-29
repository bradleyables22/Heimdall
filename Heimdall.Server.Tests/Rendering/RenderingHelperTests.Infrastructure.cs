using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    private static string Render(IHtmlContent content)
        => content.ToHtmlString();
}
