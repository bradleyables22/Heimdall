using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Server.Tests.Authoring;

public sealed class AuthoringModel
{
    public string Name { get; set; } = "Ada";
    public bool EnableBehavior { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = new(2026, 8, 26, 18, 30, 5, TimeSpan.Zero);
}

public sealed class AuthoringFixtureController : Controller
{
    [HttpPost("/authoring/save")]
    public IActionResult Save() => Ok();
}
