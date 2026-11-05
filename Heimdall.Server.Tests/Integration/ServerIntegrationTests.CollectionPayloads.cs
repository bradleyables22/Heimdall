using System.Net;
using System.Net.Http.Json;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    public static TheoryData<string, string, int> CollectionParameterCases
    {
        get
        {
            var cases = new TheoryData<string, string, int>();
            foreach (var shape in new[] { "array", "list", "enumerable", "readonly-list", "readonly-collection", "collection", "list-interface" })
            foreach (var transport in new[] { "json", "multipart", "urlencoded" })
            foreach (var count in new[] { 1, 3 })
                cases.Add(shape, transport, count);
            return cases;
        }
    }

    [Theory]
    [InlineData("json", 1)]
    [InlineData("json", 3)]
    [InlineData("multipart", 1)]
    [InlineData("multipart", 3)]
    [InlineData("urlencoded", 1)]
    [InlineData("urlencoded", 3)]
    public async Task ContentAction_BindsCollectionPropertiesFromSingleOrRepeatedFields(string transport, int count)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var names = new[] { "Alice", "", "Alice" }.Take(count).ToArray();
        var fields = names.Select(name => KeyValuePair.Create("Names", name))
            .Concat(names.Select(name => KeyValuePair.Create("Aliases", name)))
            .Concat(Enumerable.Range(1, count).Select(number => KeyValuePair.Create("Numbers", number.ToString())))
            .Concat(Enumerable.Range(1, count).Select(_ => KeyValuePair.Create("Flags", "on")))
            .ToArray();

        using var response = await PostCollectionFieldsAsync(client, "tests.collections.properties", transport, fields);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal(
            $"<span>{string.Join('|', names)};{string.Join('|', names)};{string.Join('|', Enumerable.Range(1, count))};{string.Join('|', Enumerable.Repeat(true, count))}</span>",
            html);
    }

    [Theory]
    [MemberData(nameof(CollectionParameterCases))]
    public async Task ContentAction_BindsCollectionParameterFromSingleOrRepeatedNamedFields(string shape, string transport, int count)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var names = new[] { "Alice", "", "Alice" }.Take(count).ToArray();
        var fields = names.Select(name => KeyValuePair.Create("NAMES", name))
            .Append(KeyValuePair.Create("unrelated", "ignored"))
            .ToArray();

        using var response = await PostCollectionFieldsAsync(client, $"tests.collections.{shape}", transport, fields);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal($"<span>{string.Join('|', names)}</span>", html);
    }

    [Theory]
    [InlineData("multipart", 1)]
    [InlineData("multipart", 3)]
    [InlineData("urlencoded", 1)]
    [InlineData("urlencoded", 3)]
    public async Task ContentAction_BindsCollectionParameterUsingFromFormAlias(string transport, int count)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var names = new[] { "Alice", "", "Alice" }.Take(count).ToArray();

        using var response = await PostCollectionFieldsAsync(
            client, "tests.collections.form-alias", transport,
            names.Select(name => KeyValuePair.Create("Names", name)).ToArray());
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal($"<span>{string.Join('|', names)}</span>", html);
    }

    [Theory]
    [InlineData("array")]
    [InlineData("list")]
    public async Task ContentAction_BindsCollectionParameterFromRootJsonArray(string shape)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await PostContentActionAsync(client, $"tests.collections.{shape}", new[] { "Alice", "Bob" });
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>Alice|Bob</span>", html);
    }

    [Theory]
    [InlineData("{\"names\":[]}", "empty")]
    [InlineData("{\"names\":null}", "null")]
    [InlineData("{\"unrelated\":\"ignored\"}", "null")]
    public async Task ContentAction_PreservesEmptyNullAndMissingCollectionParameters(string json, string expected)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        using var document = System.Text.Json.JsonDocument.Parse(json);

        using var response = await PostContentActionAsync(client, "tests.collections.optional", document.RootElement);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal($"<span>{expected}</span>", html);
    }

    [Theory]
    [InlineData("multipart", "123")]
    [InlineData("multipart", "null")]
    [InlineData("multipart", "\"Alice\"")]
    [InlineData("urlencoded", "123")]
    [InlineData("urlencoded", "null")]
    [InlineData("urlencoded", "\"Alice\"")]
    public async Task ContentAction_PreservesSingleCollectionFormFieldAsLiteralText(string transport, string name)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await PostCollectionFieldsAsync(
            client, "tests.collections.array", transport, [KeyValuePair.Create("names", name)]);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal($"<span>{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(name)}</span>", html);
    }

    [Theory]
    [InlineData("{\"numbers\":\"invalid\"}")]
    [InlineData("{\"numbers\":[\"1\",\"invalid\"]}")]
    public async Task ContentAction_RejectsInvalidCollectionElementValues(string json)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        using var document = System.Text.Json.JsonDocument.Parse(json);

        using var response = await PostContentActionAsync(client, "tests.collections.properties", document.RootElement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContentAction_PreservesDictionaryAndBase64ByteArrayPayloads()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await PostContentActionAsync(client, "tests.collections.other-types", new
        {
            Labels = new Dictionary<string, string> { ["name"] = "Alice" },
            Bytes = Convert.ToBase64String([1, 2, 3])
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>Alice|1,2,3</span>", html);
    }

    private static async Task<HttpResponseMessage> PostCollectionFieldsAsync(
        HttpClient client,
        string invocationId,
        string transport,
        IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        HttpContent content;
        if (transport == "json")
        {
            var payload = fields.GroupBy(field => field.Key).ToDictionary(
                group => group.Key,
                group => group.Count() == 1 ? (object)group.First().Value : group.Select(field => field.Value).ToArray());
            content = JsonContent.Create(payload);
        }
        else if (transport == "urlencoded")
        {
            content = new FormUrlEncodedContent(fields);
        }
        else
        {
            var multipart = new MultipartFormDataContent();
            foreach (var field in fields)
                multipart.Add(new StringContent(field.Value), field.Key);
            content = multipart;
        }

        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/invocations");
        request.Headers.Add("X-Heimdall-Content-Invocation", invocationId);
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed class CollectionFieldsPayload
    {
        public string[] Names { get; set; } = [];
        public List<string> Aliases { get; set; } = [];
        public int[] Numbers { get; set; } = [];
        public bool[] Flags { get; set; } = [];
    }

    private sealed class OtherCollectionTypesPayload
    {
        public Dictionary<string, string> Labels { get; set; } = [];
        public byte[] Bytes { get; set; } = [];
    }

    private static class CollectionContentActions
    {
        [ContentInvocation("tests.collections.properties")]
        public static IHtmlContent Properties([ContentPayload] CollectionFieldsPayload payload)
            => Html.Span($"{string.Join('|', payload.Names)};{string.Join('|', payload.Aliases)};{string.Join('|', payload.Numbers)};{string.Join('|', payload.Flags)}");

        [ContentInvocation("tests.collections.array")]
        public static IHtmlContent Array([ContentPayload] string[] names) => Render(names);

        [ContentInvocation("tests.collections.list")]
        public static IHtmlContent List([ContentPayload] List<string> names) => Render(names);

        [ContentInvocation("tests.collections.enumerable")]
        public static IHtmlContent Enumerable([ContentPayload] IEnumerable<string> names) => Render(names);

        [ContentInvocation("tests.collections.readonly-list")]
        public static IHtmlContent ReadOnlyList([ContentPayload] IReadOnlyList<string> names) => Render(names);

        [ContentInvocation("tests.collections.readonly-collection")]
        public static IHtmlContent ReadOnlyCollection([ContentPayload] IReadOnlyCollection<string> names) => Render(names);

        [ContentInvocation("tests.collections.collection")]
        public static IHtmlContent Collection([ContentPayload] ICollection<string> names) => Render(names);

        [ContentInvocation("tests.collections.list-interface")]
        public static IHtmlContent ListInterface([ContentPayload] IList<string> names) => Render(names);

        [ContentInvocation("tests.collections.form-alias")]
        public static IHtmlContent FormAlias([FromForm(Name = "Names")] string[] values) => Render(values);

        [ContentInvocation("tests.collections.optional")]
        public static IHtmlContent Optional([ContentPayload] string[]? names = null)
            => Html.Span(names is null ? "null" : names.Length == 0 ? "empty" : string.Join('|', names));

        [ContentInvocation("tests.collections.other-types")]
        public static IHtmlContent OtherTypes([ContentPayload] OtherCollectionTypesPayload payload)
            => Html.Span($"{payload.Labels["name"]}|{string.Join(',', payload.Bytes)}");

        private static IHtmlContent Render(IEnumerable<string> names) => Html.Span(string.Join('|', names));
    }
}
