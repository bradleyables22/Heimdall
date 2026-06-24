using Heimdall.E2E.Rendering.Layouts;
using Heimdall.E2E.Rendering.Pages;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery();
builder.Services.AddCors();
builder.Services
	.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/e2e-signin";
		options.AccessDeniedPath = "/e2e-denied";
	});
builder.Services.AddAuthorization();
builder.Services.AddHeimdall(options =>
	options.EnableDetailedErrors = true
);

var staticOutputPath = Environment.GetEnvironmentVariable("HEIMDALL_E2E_STATIC_OUTPUT");
if (string.IsNullOrWhiteSpace(staticOutputPath))
	staticOutputPath = Path.Combine("artifacts", "static-site");

builder.Services
	.AddHeimdallStaticSiteGeneration(options =>
	{
		options.UseContentRootPath(staticOutputPath);
		options.CleanOutputPath = true;
		options.CopyWebRootAssets = true;
		options.CopyStaticWebAssets = true;
		options.UsePathBase("/e2e-static");
		options.UseSitemap("https://heimdall-e2e.example");
		options.UseRobotsTxt();
	})
	.WithStaticPage("/", ctx => StaticSitePage.Render(ctx, "Static Home", "home"))
	.WithStaticPage("/e2e", ctx => StaticSitePage.Render(ctx, "Static E2E", "e2e"))
	.WithStaticPage("/docs/start", ctx => StaticSitePage.Render(ctx, "Static Docs", "docs"))
	.WithStaticPage("/feed.xml", () => Html.Raw("<?xml version=\"1.0\" encoding=\"utf-8\"?><feed><title>Heimdall E2E</title></feed>"))
	.WithNotFoundPage(ctx => StaticSitePage.Render(ctx, "Static Not Found", "not-found"));

var app = builder.Build();

StaticAssets.Discover(app.Environment.WebRootPath);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseCors();

app.UseHttpsRedirection();

app.MapStaticAssets();
app.UseStaticFiles();

app.UseHeimdall();

app.MapHeimdallPage("/", (_, ctx) =>
{
	return MainLayout.Render(E2EHarnessPage.Render(), "E2E Harness");
});
app.MapHeimdallPage("/e2e", (_, ctx) =>
{
	return MainLayout.Render(E2EHarnessPage.Render(), "E2E Harness");
});

app.MapGet("/e2e-signin", () =>
	Results.Content(
		"<!doctype html><html><body><main id=\"e2e-signin-page\">Sign in required</main></body></html>",
		"text/html; charset=utf-8"));

await app.RunWithHeimdallStaticSiteGenerationAsync(args);

