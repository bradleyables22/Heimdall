using Heimdall.Functions;
using Heimdall.Functions.E2E;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Heimdall.Server;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Services.AddAuthentication("E2E").AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>("E2E", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<DemoStreamControl>();
builder.Services.AddHeimdallFunctionsMvc<Functions>();
builder.Services.AddHeimdallBlazor();
builder.Services.AddHeimdallFunctions(options =>
{
    options.BifrostHeartbeatInterval = TimeSpan.FromSeconds(1);
    options.AuthorizeBifrostTopic = (_, topic) => ValueTask.FromResult(topic == "functions-e2e");
}, typeof(DemoInvocations).Assembly);
builder.Build().Run();
