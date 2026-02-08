using Heimdall.Example.Raw.Rendering.Layouts;
using Heimdall.Example.Raw.Utilities.BackgroundServices;
using Heimdall.Server;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery();
builder.Services.AddCors();

builder.Services.AddSingleton<NoteService>();
builder.Services.AddHostedService<DummyNoteLoader>();

builder.Services.AddHeimdall(options => options.EnableDetailedErrors = true);

var app = builder.Build();

//app.UseDefaultFiles();

app.UseAntiforgery();
app.UseCors();

app.UseHttpsRedirection();

app.MapStaticAssets();
app.UseStaticFiles();

app.UseHeimdall();
app.MapHeimdallPage(settings =>
{
    settings.Pattern = "/";
    settings.PagePath = "index.html";
    settings.LayoutPath = "layouts/mainlayout.html";
    settings.LayoutPlaceholder = "{{page}}";
    settings.LayoutComponents.Add("{{MenuComponent}}", (sp, ctx) => MainLayout.RenderMenu(ctx, "/"));
});

app.MapHeimdallPage(settings =>
{
    settings.Pattern = "/dashboard";
    settings.PagePath = "index.html";
    settings.LayoutPath = "layouts/mainlayout.html";
    settings.LayoutPlaceholder = "{{page}}";
    settings.LayoutComponents.Add("{{MenuComponent}}", (sp, ctx) => MainLayout.RenderMenu(ctx, "/dashboard"));
});
app.MapHeimdallPage(settings =>
{
    settings.Pattern = "/settings";
    settings.PagePath = "index.html";
    settings.LayoutPath = "layouts/mainlayout.html";
    settings.LayoutPlaceholder = "{{page}}";
    settings.LayoutComponents.Add("{{MenuComponent}}", (sp, ctx) => MainLayout.RenderMenu(ctx, "/settings"));
});
app.Run();



