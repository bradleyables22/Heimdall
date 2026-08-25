using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Server.Tests;

public sealed class ObservabilityTests
{
    [Fact]
    public async Task ContentActions_EmitActivitiesAndMetricsWithoutPayloadTags()
    {
        using var diagnostics = new HeimdallDiagnosticsListener();
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var success = await PostContentActionAsync(
            client,
            "tests.telemetry.success",
            new { Secret = "must-not-appear" });
        var failure = await PostContentActionAsync(client, "tests.telemetry.failure");
        var timeout = await PostContentActionAsync(client, "tests.telemetry.timeout");

        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.Equal(HttpStatusCode.GatewayTimeout, timeout.StatusCode);

        var successActivity = diagnostics.GetActivity(
            HeimdallDiagnostics.ContentActionActivityName,
            "tests.telemetry.success");
        Assert.Equal(ActivityStatusCode.Ok, successActivity.Status);
        Assert.Equal("success", successActivity.Tags[HeimdallDiagnostics.OutcomeTagName]);
        Assert.Equal(StatusCodes.Status200OK, successActivity.Tags[HeimdallDiagnostics.ResponseStatusCodeTagName]);

        var failureActivity = diagnostics.GetActivity(
            HeimdallDiagnostics.ContentActionActivityName,
            "tests.telemetry.failure");
        Assert.Equal(ActivityStatusCode.Error, failureActivity.Status);
        Assert.Equal("error", failureActivity.Tags[HeimdallDiagnostics.OutcomeTagName]);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            failureActivity.Tags[HeimdallDiagnostics.ErrorTypeTagName]);

        var timeoutActivity = diagnostics.GetActivity(
            HeimdallDiagnostics.ContentActionActivityName,
            "tests.telemetry.timeout");
        Assert.Equal("timeout", timeoutActivity.Tags[HeimdallDiagnostics.OutcomeTagName]);
        Assert.Equal("timeout", timeoutActivity.Tags[HeimdallDiagnostics.CancellationReasonTagName]);

        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.ContentActionDurationMetricName &&
                measurement.HasTag(HeimdallDiagnostics.ActionIdTagName, "tests.telemetry.success") &&
                measurement.Value >= 0);
        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.ContentActionRequestBodySizeMetricName &&
                measurement.HasTag(HeimdallDiagnostics.ActionIdTagName, "tests.telemetry.success") &&
                measurement.Value > 0);
        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.ContentActionResponseBodySizeMetricName &&
                measurement.HasTag(HeimdallDiagnostics.ActionIdTagName, "tests.telemetry.success") &&
                measurement.Value > 0);
        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.ContentActionExceptionsMetricName &&
                measurement.HasTag(HeimdallDiagnostics.ActionIdTagName, "tests.telemetry.failure"));
        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.ContentActionCancellationsMetricName &&
                measurement.HasTag(HeimdallDiagnostics.ActionIdTagName, "tests.telemetry.timeout") &&
                measurement.HasTag(HeimdallDiagnostics.CancellationReasonTagName, "timeout"));

        Assert.DoesNotContain(
            diagnostics.AllTags(),
            tag => tag.Key.Contains("payload", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.Value?.ToString(), "must-not-appear", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bifrost_EmitsPublishDeliveryAndConnectionMetricsWithoutTopicTags()
    {
        using var diagnostics = new HeimdallDiagnosticsListener();
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var topic = $"private:user:{Guid.NewGuid():N}";
        var eventName = "telemetry.updated";
        var token = await GetBifrostTokenAsync(client, topic);
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var streamRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic={Uri.EscapeDataString(topic)}&st={Uri.EscapeDataString(token)}");
        using var streamResponse = await client.SendAsync(
            streamRequest,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        await using var stream = await streamResponse.Content.ReadAsStreamAsync(streamCts.Token);
        using var reader = new StreamReader(stream);

        await ReadUntilAsync(reader, "heimdall:connected", streamCts.Token);
        await app.Services.GetRequiredService<Bifrost>().PublishAsync(
            topic,
            eventName,
            Html.Span("observed"),
            TimeSpan.FromSeconds(5),
            streamCts.Token);
        await ReadUntilAsync(reader, "observed", streamCts.Token);

        streamCts.Cancel();
        streamResponse.Dispose();
        await WaitForMeasurementAsync(
            diagnostics,
            HeimdallDiagnostics.BifrostActiveConnectionsMetricName,
            -1);

        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.BifrostPublishedMessagesMetricName &&
                measurement.HasTag(HeimdallDiagnostics.BifrostEventNameTagName, eventName));
        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.BifrostDeliveredMessagesMetricName &&
                measurement.HasTag(HeimdallDiagnostics.BifrostEventNameTagName, eventName));
        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.BifrostActiveConnectionsMetricName &&
                measurement.Value == 1);
        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.BifrostActiveSubscribersMetricName &&
                measurement.Value == 1);

        var publishActivity = diagnostics.Activities.Single(
            activity => activity.Name == HeimdallDiagnostics.BifrostPublishActivityName);
        Assert.Equal(eventName, publishActivity.Tags[HeimdallDiagnostics.BifrostEventNameTagName]);
        Assert.DoesNotContain(
            diagnostics.AllTags(),
            tag => tag.Key.Contains("topic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.Value?.ToString(), topic, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bifrost_RecordsMessagesDroppedWithoutSubscribers()
    {
        using var diagnostics = new HeimdallDiagnosticsListener();
        var bifrost = new Bifrost();

        await bifrost.PublishAsync(
            "private-topic",
            "telemetry.orphaned",
            Html.Span("orphaned"),
            TimeSpan.FromSeconds(5));

        Assert.Contains(
            diagnostics.Measurements,
            measurement => measurement.InstrumentName == HeimdallDiagnostics.BifrostDroppedMessagesMetricName &&
                measurement.HasTag(HeimdallDiagnostics.BifrostEventNameTagName, "telemetry.orphaned") &&
                measurement.HasTag(HeimdallDiagnostics.BifrostDropReasonTagName, "no_subscribers"));
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddAntiforgery();
        builder.Services.AddHeimdall(typeof(ObservabilityTests).Assembly);

        var app = builder.Build();
        app.UseAntiforgery();
        app.UseHeimdall();
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> PostContentActionAsync(
        HttpClient client,
        string actionId,
        object? payload = null)
    {
        var csrf = await GetCsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", actionId);
        request.Headers.Add("RequestVerificationToken", csrf.RequestToken);
        request.Headers.Add("Cookie", csrf.CookieHeader);
        request.Content = JsonContent.Create(payload ?? new { });
        return await client.SendAsync(request);
    }

    private static async Task<string> GetBifrostTokenAsync(HttpClient client, string topic)
    {
        var csrf = await GetCsrfAsync(client);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost/token?topic={Uri.EscapeDataString(topic)}");
        request.Headers.Add("RequestVerificationToken", csrf.RequestToken);
        request.Headers.Add("Cookie", csrf.CookieHeader);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        return body?.Token ?? throw new InvalidOperationException("Bifrost token response was empty.");
    }

    private static async Task<CsrfToken> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/__heimdall/v1/csrf");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CsrfResponse>();
        var requestToken = body?.RequestToken
            ?? throw new InvalidOperationException("CSRF response was empty.");
        var cookieHeader = string.Join(
            "; ",
            response.Headers.GetValues("Set-Cookie").Select(value => value.Split(';', 2)[0]));
        return new CsrfToken(requestToken, cookieHeader);
    }

    private static async Task ReadUntilAsync(
        StreamReader reader,
        string expected,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;
            if (line.Contains(expected, StringComparison.Ordinal))
                return;
        }

        throw new TimeoutException($"SSE stream did not include expected content: {expected}");
    }

    private static async Task WaitForMeasurementAsync(
        HeimdallDiagnosticsListener diagnostics,
        string instrumentName,
        double value)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (diagnostics.Measurements.Any(measurement =>
                measurement.InstrumentName == instrumentName && measurement.Value == value))
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Metric '{instrumentName}' did not record '{value}'.");
    }

    private sealed record CsrfToken(string RequestToken, string CookieHeader);

    private sealed class CsrfResponse
    {
        public string? RequestToken { get; set; }
    }

    private sealed class BifrostTokenResponse
    {
        public string? Token { get; set; }
    }

    private static class TelemetryContentActions
    {
        [ContentInvocation("tests.telemetry.success")]
        public static IHtmlContent Success(TelemetryPayload payload)
            => Html.Span("observed");

        [ContentInvocation("tests.telemetry.failure")]
        public static IHtmlContent Failure()
            => throw new InvalidOperationException("Expected telemetry failure.");

        [RequestTimeout(50)]
        [ContentInvocation("tests.telemetry.timeout")]
        public static async Task<IHtmlContent> Timeout(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Html.Span("late");
        }
    }

    private sealed class TelemetryPayload
    {
        public string? Secret { get; set; }
    }
}

internal sealed class HeimdallDiagnosticsListener : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public HeimdallDiagnosticsListener()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == HeimdallDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Activities.Enqueue(ActivitySnapshot.Create(activity))
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == HeimdallDiagnostics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>(RecordMeasurement);
        _meterListener.SetMeasurementEventCallback<double>(RecordMeasurement);
        _meterListener.Start();
    }

    public ConcurrentQueue<ActivitySnapshot> Activities { get; } = new();

    public ConcurrentQueue<MetricMeasurement> Measurements { get; } = new();

    public ActivitySnapshot GetActivity(string name, string actionId)
        => Activities.Single(activity =>
            activity.Name == name &&
            activity.Tags.TryGetValue(HeimdallDiagnostics.ActionIdTagName, out var value) &&
            string.Equals(value?.ToString(), actionId, StringComparison.Ordinal));

    public IEnumerable<KeyValuePair<string, object?>> AllTags()
        => Activities.SelectMany(activity => activity.Tags)
            .Concat(Measurements.SelectMany(measurement => measurement.Tags));

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private void RecordMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct
    {
        Measurements.Enqueue(new MetricMeasurement(
            instrument.Name,
            Convert.ToDouble(measurement),
            CopyTags(tags)));
    }

    private static IReadOnlyDictionary<string, object?> CopyTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
            result[tag.Key] = tag.Value;
        return result;
    }
}

internal sealed record ActivitySnapshot(
    string Name,
    ActivityStatusCode Status,
    IReadOnlyDictionary<string, object?> Tags)
{
    public static ActivitySnapshot Create(Activity activity)
        => new(
            activity.OperationName,
            activity.Status,
            activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal));
}

internal sealed record MetricMeasurement(
    string InstrumentName,
    double Value,
    IReadOnlyDictionary<string, object?> Tags)
{
    public bool HasTag(string name, object expected)
        => Tags.TryGetValue(name, out var actual) && Equals(actual, expected);
}
