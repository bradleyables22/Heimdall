using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    private static void AddAssemblyToContentRegistry(Assembly assembly)
    {
        var registryType = typeof(ContentInvocationAttribute).Assembly.GetType(
            "Heimdall.Server.ContentRegistry",
            throwOnError: true)!;
        var registry = Activator.CreateInstance(registryType, nonPublic: true)!;
        using var services = new ServiceCollection().BuildServiceProvider();
        var addFromAssembly = registryType.GetMethod(
            "AddFromAssembly",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to find ContentRegistry.AddFromAssembly.");

        try
        {
            addFromAssembly.Invoke(registry, [assembly, services]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static Assembly CreateDuplicateInvocationAssembly()
    {
        var assemblyName = new AssemblyName($"HeimdallDuplicateInvocationTests{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");

        DefineContentActionType(
            module,
            "PrefixedCollisionActions",
            prefix: "tests.collision",
            invocation: "refresh",
            methodName: "Refresh");
        DefineContentActionType(
            module,
            "ExplicitCollisionActions",
            prefix: null,
            invocation: "tests.collision.refresh",
            methodName: "Refresh");

        return assembly;
    }

    private static void DefineContentActionType(
        ModuleBuilder module,
        string typeName,
        string? prefix,
        string invocation,
        string methodName)
    {
        var type = module.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

        if (prefix is not null)
        {
            var prefixCtor = typeof(ContentInvocationPrefixAttribute).GetConstructor([typeof(string)])
                ?? throw new InvalidOperationException("Unable to find ContentInvocationPrefixAttribute constructor.");
            type.SetCustomAttribute(new CustomAttributeBuilder(prefixCtor, [prefix]));
        }

        var method = type.DefineMethod(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(IHtmlContent),
            Type.EmptyTypes);
        var invocationCtor = typeof(ContentInvocationAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("Unable to find ContentInvocationAttribute constructor.");
        method.SetCustomAttribute(new CustomAttributeBuilder(invocationCtor, [invocation]));

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);

        type.CreateType();
    }
}
