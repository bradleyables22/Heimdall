using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Emit;
using Heimdall.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    [Fact]
    public async Task AddHeimdallInvocationAssembly_DiscoversContentFromRegisteredAssembly()
    {
        var libraryAssembly = CreateRegisteredContentAssembly();

        await using var app = await CreateAppAsync(services =>
        {
            services.AddHeimdallInvocationAssembly(libraryAssembly);
        });
        using var client = app.GetTestClient();

        var response = await PostContentInvocationAsync(
            client,
            "tests.library.registered");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostContentInvocationAsync(
        HttpClient client,
        string invocationId)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/__heimdall/v1/content/invocations");
        request.Headers.Add("X-Heimdall-Content-Invocation", invocationId);
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Content = JsonContent.Create(new { });
        return await client.SendAsync(request);
    }

    [Fact]
    public void AddHeimdallInvocationAssembly_DeduplicatesRegisteredAssemblies()
    {
        var services = new ServiceCollection();
        var libraryAssembly = CreateRegisteredContentAssembly();

        services.AddHeimdallInvocationAssembly(libraryAssembly);
        services.AddHeimdallInvocationAssembly(libraryAssembly);

        var registrationType = typeof(ContentInvocationAttribute).Assembly.GetType(
            "Heimdall.Server.HeimdallInvocationAssemblyRegistration",
            throwOnError: true)!;

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == registrationType)
            .ToArray();

        Assert.Single(registrations);
    }

    private static Assembly CreateRegisteredContentAssembly()
    {
        var assemblyName = new AssemblyName(
            $"HeimdallRegisteredContentTests{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");

        DefineContentActionType(
            module,
            "RegisteredLibraryActions",
            prefix: null,
            invocation: "tests.library.registered",
            methodName: "Registered");

        return assembly;
    }
}
