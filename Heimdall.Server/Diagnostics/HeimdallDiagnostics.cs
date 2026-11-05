using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
    /// <summary>
    /// Identifies the diagnostic sources, activities, metrics, and tags emitted by Heimdall.
    /// </summary>
    /// <remarks>
    /// Register <see cref="ActivitySourceName"/> with an OpenTelemetry tracing provider and
    /// <see cref="MeterName"/> with an OpenTelemetry metrics provider. Heimdall diagnostics
    /// intentionally exclude action payloads, user identities, and Bifrost topic names.
    /// </remarks>
    public static class HeimdallDiagnostics
    {
        /// <summary>Gets the name of the Heimdall activity source.</summary>
        public const string ActivitySourceName = "Heimdall.Server";

        /// <summary>Gets the name of the Heimdall meter.</summary>
        public const string MeterName = "Heimdall.Server";

        /// <summary>Gets the content invocation activity name.</summary>
        public const string ContentInvocationActivityName = "heimdall.content_invocation";

        /// <summary>Compatibility alias for the pre-invocation activity name.</summary>
        public const string ContentActionActivityName = ContentInvocationActivityName;

        /// <summary>Gets the Bifrost SSE connection activity name.</summary>
        public const string BifrostConnectionActivityName = "heimdall.bifrost.connection";

        /// <summary>Gets the Bifrost publish activity name.</summary>
        public const string BifrostPublishActivityName = "heimdall.bifrost.publish";

        /// <summary>Gets the content invocation request counter name.</summary>
        public const string ContentInvocationRequestsMetricName = "heimdall.server.content_invocation.requests";

        /// <summary>Compatibility alias for the pre-invocation metric name.</summary>
        public const string ContentActionRequestsMetricName = ContentInvocationRequestsMetricName;

        /// <summary>Gets the content invocation duration histogram name.</summary>
        public const string ContentInvocationDurationMetricName = "heimdall.server.content_invocation.duration";

        /// <summary>Compatibility alias for the pre-invocation metric name.</summary>
        public const string ContentActionDurationMetricName = ContentInvocationDurationMetricName;

        /// <summary>Gets the content invocation request body size histogram name.</summary>
        public const string ContentInvocationRequestBodySizeMetricName = "heimdall.server.content_invocation.request.body.size";

        /// <summary>Compatibility alias for the pre-invocation metric name.</summary>
        public const string ContentActionRequestBodySizeMetricName = ContentInvocationRequestBodySizeMetricName;

        /// <summary>Gets the content invocation response body size histogram name.</summary>
        public const string ContentInvocationResponseBodySizeMetricName = "heimdall.server.content_invocation.response.body.size";

        /// <summary>Compatibility alias for the pre-invocation metric name.</summary>
        public const string ContentActionResponseBodySizeMetricName = ContentInvocationResponseBodySizeMetricName;

        /// <summary>Gets the content invocation exception counter name.</summary>
        public const string ContentInvocationExceptionsMetricName = "heimdall.server.content_invocation.exceptions";

        /// <summary>Compatibility alias for the pre-invocation metric name.</summary>
        public const string ContentActionExceptionsMetricName = ContentInvocationExceptionsMetricName;

        /// <summary>Gets the content invocation cancellation counter name.</summary>
        public const string ContentInvocationCancellationsMetricName = "heimdall.server.content_invocation.cancellations";

        /// <summary>Compatibility alias for the pre-invocation metric name.</summary>
        public const string ContentActionCancellationsMetricName = ContentInvocationCancellationsMetricName;

        /// <summary>Gets the active Bifrost SSE connection up-down counter name.</summary>
        public const string BifrostActiveConnectionsMetricName = "heimdall.server.bifrost.connections.active";

        /// <summary>Gets the active Bifrost subscriber up-down counter name.</summary>
        public const string BifrostActiveSubscribersMetricName = "heimdall.server.bifrost.subscribers.active";

        /// <summary>Gets the Bifrost published message counter name.</summary>
        public const string BifrostPublishedMessagesMetricName = "heimdall.server.bifrost.messages.published";

        /// <summary>Gets the Bifrost delivered message counter name.</summary>
        public const string BifrostDeliveredMessagesMetricName = "heimdall.server.bifrost.messages.delivered";

        /// <summary>Gets the Bifrost expired message counter name.</summary>
        public const string BifrostExpiredMessagesMetricName = "heimdall.server.bifrost.messages.expired";

        /// <summary>Gets the Bifrost dropped message counter name.</summary>
        public const string BifrostDroppedMessagesMetricName = "heimdall.server.bifrost.messages.dropped";

        /// <summary>Gets the content invocation identifier tag name.</summary>
        public const string InvocationIdTagName = "heimdall.invocation.id";

        /// <summary>Compatibility alias for the pre-invocation tag name.</summary>
        public const string ActionIdTagName = InvocationIdTagName;

        /// <summary>Gets the operation outcome tag name.</summary>
        public const string OutcomeTagName = "heimdall.outcome";

        /// <summary>Gets the HTTP response status code tag name.</summary>
        public const string ResponseStatusCodeTagName = "http.response.status_code";

        /// <summary>Gets the exception type tag name.</summary>
        public const string ErrorTypeTagName = "error.type";

        /// <summary>Gets the cancellation reason tag name.</summary>
        public const string CancellationReasonTagName = "heimdall.cancellation.reason";

        /// <summary>Gets the Bifrost event name tag name.</summary>
        public const string BifrostEventNameTagName = "heimdall.bifrost.event.name";

        /// <summary>Gets the Bifrost message drop reason tag name.</summary>
        public const string BifrostDropReasonTagName = "heimdall.bifrost.drop.reason";
    }

    internal static class HeimdallTelemetry
    {
        private const string UnresolvedInvocationId = "unresolved";

        private static readonly string? InstrumentationVersion =
            typeof(HeimdallDiagnostics).Assembly.GetName().Version?.ToString();

        private static readonly ActivitySource ActivitySource = new(
            HeimdallDiagnostics.ActivitySourceName,
            InstrumentationVersion);

        private static readonly Meter Meter = new(
            HeimdallDiagnostics.MeterName,
            InstrumentationVersion);

        private static readonly Counter<long> ContentInvocationRequests = Meter.CreateCounter<long>(
            HeimdallDiagnostics.ContentInvocationRequestsMetricName,
            description: "Number of Heimdall content invocation requests.");

        private static readonly Histogram<double> ContentInvocationDuration = Meter.CreateHistogram<double>(
            HeimdallDiagnostics.ContentInvocationDurationMetricName,
            unit: "s",
            description: "Duration of Heimdall content invocation requests.");

        private static readonly Histogram<long> ContentInvocationRequestBodySize = Meter.CreateHistogram<long>(
            HeimdallDiagnostics.ContentInvocationRequestBodySizeMetricName,
            unit: "By",
            description: "Size of Heimdall content invocation request bodies.");

        private static readonly Histogram<long> ContentInvocationResponseBodySize = Meter.CreateHistogram<long>(
            HeimdallDiagnostics.ContentInvocationResponseBodySizeMetricName,
            unit: "By",
            description: "Size of rendered Heimdall content invocation response bodies.");

        private static readonly Counter<long> ContentInvocationExceptions = Meter.CreateCounter<long>(
            HeimdallDiagnostics.ContentInvocationExceptionsMetricName,
            description: "Number of exceptions handled by the Heimdall content invocation pipeline.");

        private static readonly Counter<long> ContentInvocationCancellations = Meter.CreateCounter<long>(
            HeimdallDiagnostics.ContentInvocationCancellationsMetricName,
            description: "Number of cancelled Heimdall content invocation requests.");

        private static readonly UpDownCounter<long> BifrostActiveConnections = Meter.CreateUpDownCounter<long>(
            HeimdallDiagnostics.BifrostActiveConnectionsMetricName,
            description: "Number of active Heimdall Bifrost SSE connections.");

        private static readonly UpDownCounter<long> BifrostActiveSubscribers = Meter.CreateUpDownCounter<long>(
            HeimdallDiagnostics.BifrostActiveSubscribersMetricName,
            description: "Number of active Heimdall Bifrost topic subscribers.");

        private static readonly Counter<long> BifrostPublishedMessages = Meter.CreateCounter<long>(
            HeimdallDiagnostics.BifrostPublishedMessagesMetricName,
            description: "Number of messages published to Heimdall Bifrost.");

        private static readonly Counter<long> BifrostDeliveredMessages = Meter.CreateCounter<long>(
            HeimdallDiagnostics.BifrostDeliveredMessagesMetricName,
            description: "Number of Bifrost messages delivered to subscriber buffers.");

        private static readonly Counter<long> BifrostExpiredMessages = Meter.CreateCounter<long>(
            HeimdallDiagnostics.BifrostExpiredMessagesMetricName,
            description: "Number of expired Bifrost messages discarded before writing to an SSE connection.");

        private static readonly Counter<long> BifrostDroppedMessages = Meter.CreateCounter<long>(
            HeimdallDiagnostics.BifrostDroppedMessagesMetricName,
            description: "Number of Bifrost messages dropped before delivery.");

        internal static ContentInvocationTelemetryScope StartContentInvocation(HttpContext context)
            => new(context.Request.ContentLength);

        internal static BifrostConnectionTelemetryScope OpenBifrostConnection()
            => new();

        internal static Activity? StartBifrostPublish(string eventName)
        {
            var activity = ActivitySource.StartActivity(
                HeimdallDiagnostics.BifrostPublishActivityName,
                ActivityKind.Internal);
            activity?.SetTag(HeimdallDiagnostics.BifrostEventNameTagName, eventName);
            return activity;
        }

        internal static void RecordBifrostPublished(string eventName)
            => BifrostPublishedMessages.Add(1, CreateBifrostEventTags(eventName));

        internal static void RecordBifrostDelivered(string eventName)
            => BifrostDeliveredMessages.Add(1, CreateBifrostEventTags(eventName));

        internal static void RecordBifrostExpired(string eventName)
            => BifrostExpiredMessages.Add(1, CreateBifrostEventTags(eventName));

        internal static void RecordBifrostDropped(string eventName, string reason)
        {
            var tags = CreateBifrostEventTags(eventName);
            tags.Add(HeimdallDiagnostics.BifrostDropReasonTagName, reason);
            BifrostDroppedMessages.Add(1, tags);
        }

        internal static void SubscriberOpened()
            => BifrostActiveSubscribers.Add(1);

        internal static void SubscriberClosed()
            => BifrostActiveSubscribers.Add(-1);

        private static TagList CreateBifrostEventTags(string eventName)
        {
            var tags = new TagList
            {
                { HeimdallDiagnostics.BifrostEventNameTagName, eventName }
            };
            return tags;
        }

        internal sealed class ContentInvocationTelemetryScope : IDisposable
        {
            private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
            private long? _requestBodySize;
            private readonly Activity? _activity;
            private string _invocationId = UnresolvedInvocationId;
            private bool _completed;

            internal ContentInvocationTelemetryScope(long? requestBodySize)
            {
                _requestBodySize = requestBodySize;
                _activity = ActivitySource.StartActivity(
                    HeimdallDiagnostics.ContentInvocationActivityName,
                    ActivityKind.Internal);
                _activity?.SetTag(HeimdallDiagnostics.InvocationIdTagName, _invocationId);
            }

            internal void SetInvocationId(string invocationId)
            {
                _invocationId = string.IsNullOrWhiteSpace(invocationId) ? UnresolvedInvocationId : invocationId;
                _activity?.SetTag(HeimdallDiagnostics.InvocationIdTagName, _invocationId);
            }

            internal void SetRequestBodySize(long requestBodySize)
            {
                if (requestBodySize >= 0)
                    _requestBodySize = requestBodySize;
            }

            internal bool ShouldMeasureRequestBodySize
                => !_requestBodySize.HasValue && ContentInvocationRequestBodySize.Enabled;

            internal bool ShouldMeasureResponseBodySize
                => ContentInvocationResponseBodySize.Enabled;

            internal void Complete(int statusCode, long? responseBodySize = null, string? outcome = null)
            {
                if (_completed)
                    return;

                _completed = true;
                outcome ??= statusCode >= StatusCodes.Status400BadRequest ? "error" : "success";

                var tags = CreateContentInvocationTags(statusCode, outcome);
                ContentInvocationRequests.Add(1, tags);
                ContentInvocationDuration.Record(
                    Stopwatch.GetElapsedTime(_startedTimestamp).TotalSeconds,
                    tags);

                if (_requestBodySize is >= 0)
                    ContentInvocationRequestBodySize.Record(_requestBodySize.Value, tags);

                if (responseBodySize is >= 0)
                    ContentInvocationResponseBodySize.Record(responseBodySize.Value, tags);

                _activity?.SetTag(HeimdallDiagnostics.ResponseStatusCodeTagName, statusCode);
                _activity?.SetTag(HeimdallDiagnostics.OutcomeTagName, outcome);

                if (statusCode >= StatusCodes.Status400BadRequest)
                    _activity?.SetStatus(ActivityStatusCode.Error);
                else
                    _activity?.SetStatus(ActivityStatusCode.Ok);

                _activity?.Stop();
            }

            internal void RecordException(Exception exception, int statusCode)
            {
                var errorType = exception.GetType().FullName ?? exception.GetType().Name;
                var tags = new TagList
                {
                    { HeimdallDiagnostics.InvocationIdTagName, _invocationId },
                    { HeimdallDiagnostics.ErrorTypeTagName, errorType }
                };

                ContentInvocationExceptions.Add(1, tags);
                _activity?.SetTag(HeimdallDiagnostics.ErrorTypeTagName, errorType);
                _activity?.AddEvent(new ActivityEvent(
                    "exception",
                    tags: new ActivityTagsCollection
                    {
                        [HeimdallDiagnostics.ErrorTypeTagName] = errorType
                    }));
                Complete(statusCode, outcome: "error");
            }

            internal void RecordCancellation(string reason, int statusCode)
            {
                var tags = new TagList
                {
                    { HeimdallDiagnostics.InvocationIdTagName, _invocationId },
                    { HeimdallDiagnostics.CancellationReasonTagName, reason }
                };

                ContentInvocationCancellations.Add(1, tags);
                _activity?.SetTag(HeimdallDiagnostics.CancellationReasonTagName, reason);
                Complete(statusCode, outcome: reason == "timeout" ? "timeout" : "cancelled");
            }

            public void Dispose()
            {
                if (!_completed)
                    Complete(StatusCodes.Status500InternalServerError, outcome: "error");

                _activity?.Dispose();
            }

            private TagList CreateContentInvocationTags(int statusCode, string outcome)
            {
                var tags = new TagList
                {
                    { HeimdallDiagnostics.InvocationIdTagName, _invocationId },
                    { HeimdallDiagnostics.ResponseStatusCodeTagName, statusCode },
                    { HeimdallDiagnostics.OutcomeTagName, outcome }
                };
                return tags;
            }
        }

        internal sealed class BifrostConnectionTelemetryScope : IDisposable
        {
            private readonly Activity? _activity;
            private bool _completed;

            internal BifrostConnectionTelemetryScope()
            {
                BifrostActiveConnections.Add(1);
                _activity = ActivitySource.StartActivity(
                    HeimdallDiagnostics.BifrostConnectionActivityName,
                    ActivityKind.Internal);
            }

            internal void Complete(string outcome)
            {
                if (_completed)
                    return;

                _completed = true;
                _activity?.SetTag(HeimdallDiagnostics.OutcomeTagName, outcome);
                _activity?.SetStatus(outcome == "error" ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            }

            internal void RecordException(Exception exception)
            {
                var errorType = exception.GetType().FullName ?? exception.GetType().Name;
                _activity?.SetTag(HeimdallDiagnostics.ErrorTypeTagName, errorType);
                _activity?.AddEvent(new ActivityEvent(
                    "exception",
                    tags: new ActivityTagsCollection
                    {
                        [HeimdallDiagnostics.ErrorTypeTagName] = errorType
                    }));
                Complete("error");
            }

            public void Dispose()
            {
                Complete("closed");
                BifrostActiveConnections.Add(-1);
                _activity?.Dispose();
            }
        }
    }
}
