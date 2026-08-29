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
    [Fact]
    public async Task ContentAction_BindsMultipartPayloadAndUploadedFile()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("Ada"), "Name");
        content.Add(new StringContent("7"), "Count");
        content.Add(new StringContent("on"), "Enabled");
        content.Add(new StringContent("beta"), "Mode");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("avatar bytes")), "avatar", "avatar.txt");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.single",
            content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>Ada|7|True|Beta|avatar.txt|12</span>", html);
    }

    [Theory]
    [InlineData("tests.upload.multiple")]
    [InlineData("tests.upload.enumerable")]
    [InlineData("tests.upload.readonly-collection")]
    [InlineData("tests.upload.collection")]
    [InlineData("tests.upload.list-interface")]
    [InlineData("tests.upload.list")]
    [InlineData("tests.upload.form-file-collection")]
    [InlineData("tests.upload.array")]
    public async Task ContentAction_BindsSupportedUploadedFileCollectionsByParameterName(string actionId)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1, 2]), "attachments", "one.bin");
        content.Add(new ByteArrayContent([3, 4, 5]), "attachments", "two.bin");
        content.Add(new ByteArrayContent([6]), "unrelated", "ignored.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            actionId,
            content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>one.bin:2|two.bin:3</span>", html);
    }

    [Fact]
    public async Task ContentAction_BindsMultipartPayloadWithParameterPrefixes()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("Grace"), "payload.Name");
        content.Add(new StringContent("11"), "payload[Count]");
        content.Add(new StringContent("true"), "payload.Enabled");
        content.Add(new StringContent("alpha"), "payload.Mode");
        content.Add(new ByteArrayContent([1, 2, 3]), "avatar", "profile.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.single",
            content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>Grace|11|True|Alpha|profile.bin|3</span>", html);
    }

    [Fact]
    public async Task ContentAction_BindsMultipartPayloadFromEmbeddedJsonField()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(
            new StringContent("{\"name\":\"Lin\",\"count\":4,\"enabled\":true,\"mode\":\"beta\"}"),
            "payload");
        content.Add(new ByteArrayContent([1]), "avatar", "profile.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.single",
            content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>Lin|4|True|Beta|profile.bin|1</span>", html);
    }

    [Fact]
    public async Task ContentAction_MissingOptionalUploadedFileBindsNull()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("no replacement"), "description");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.optional",
            content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>none</span>", html);
    }

    [Fact]
    public async Task ContentAction_BlankBrowserFileInputDoesNotSatisfyRequiredFile()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        var blankFile = new ByteArrayContent([]);
        blankFile.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"upload\"",
            FileName = "\"\""
        };
        content.Add(blankFile);

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.required",
            content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Missing required uploaded file", body);
    }

    [Fact]
    public async Task ContentAction_MissingRequiredUploadedFileReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("no file"), "description");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.required",
            content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Missing required uploaded file", body);
    }

    [Fact]
    public async Task ContentAction_FileParameterRejectsJsonRequests()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.upload.required");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Contains("multipart/form-data", body);
    }

    [Fact]
    public async Task ContentAction_RequestSizeLimitAttributeReturnsPayloadTooLarge()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[1024]), "upload", "large.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.request-size-limited",
            content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("Heimdall action request body is too large", body);
    }

    [Fact]
    public async Task ContentAction_TypeRequestSizeLimitAppliesToContentAction()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[1024]), "upload", "large.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.type-request-size-limited",
            content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData("tests.upload.request-size-disabled")]
    [InlineData("tests.upload.request-size-overridden")]
    public async Task ContentAction_MethodRequestSizeMetadataOverridesTypeLimit(string actionId)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[1024]), "upload", "large.bin");

        var response = await PostMultipartContentActionAsync(client, actionId, content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>1024</span>", html);
    }

    [Fact]
    public async Task ContentAction_RequestFormLimitsAttributeReturnsPayloadTooLarge()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1, 2, 3, 4, 5]), "upload", "limited.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.type-form-limited",
            content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("Heimdall action request body is too large", body);
    }

    [Fact]
    public async Task ContentAction_MethodRequestFormLimitsOverrideTypeLimit()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1, 2, 3, 4, 5]), "upload", "allowed.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.form-limit-overridden",
            content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>5</span>", html);
    }

    [Fact]
    public async Task ContentAction_GlobalAspNetFormOptionsAreStillHonored()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 4;
            });
        });
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1, 2, 3, 4, 5]), "upload", "limited.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.required",
            content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
