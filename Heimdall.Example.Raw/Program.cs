using Heimdall.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery();
builder.Services.AddCors();
builder.Services.AddHeimdall(options => options.EnableDetailedErrors = true);
var app = builder.Build();

//app.UseDefaultFiles();

app.UseAntiforgery();
app.UseCors();

app.UseHttpsRedirection();

app.MapStaticAssets();
app.UseStaticFiles();

app.UseHeimdall();
app.MapHeimdallPage(settings=>
{
    settings.Pattern = "/";
    settings.RelativePath = "Heimdall/Pages/Home/index.html";
});

app.Run();



