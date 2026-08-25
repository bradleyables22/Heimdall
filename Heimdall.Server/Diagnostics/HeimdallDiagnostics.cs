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

        /// <summary>Gets the content action activity name.</summary>
        public const string ContentActionActivityName = "heimdall.content_action";

        /// <summary>Gets the Bifrost SSE connection activity name.</summary>
        public const string BifrostConnectionActivityName = "heimdall.bifrost.connection";

        /// <summary>Gets the Bifrost publish activity name.</summary>
        public const string BifrostPublishActivityName = "heimdall.bifrost.publish";

        /// <summary>Gets the content action request counter name.</summary>
        public const string ContentActionRequestsMetricName = "heimdall.server.content_action.requests";

        /// <summary>Gets the content action duration histogram name.</summary>
        public const string ContentActionDurationMetricName = "heimdall.server.content_action.duration";

        /// <summary>Gets the content action request body size histogram name.</summary>
        public const string ContentActionRequestBodySizeMetricName = "heimdall.server.content_action.request.body.size";

        /// <summary>Gets the content action response body size histogram name.</summary>
        public const string ContentActionResponseBodySizeMetricName = "heimdall.server.content_action.response.body.size";

        /// <summary>Gets the content action exception counter name.</summary>
        public const string ContentActionExceptionsMetricName = "heimdall.server.content_action.exceptions";

        /// <summary>Gets the content action cancellation counter name.</summary>
        public const string ContentActionCancellationsMetricName = "heimdall.server.content_action.cancellations";

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

        /// <summary>Gets the content action identifier tag name.</summary>
        public const string ActionIdTagName = "heimdall.action.id";

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
        private const string UnresolvedActionId = "unresolved";

        private static readonly string? InstrumentationVersion =
            typeof(HeimdallDiagnostics).Assembly.GetName().Version?.ToString();

        private static readonly ActivitySource ActivitySource = new(
            HeimdallDiagnostics.ActivitySourceName,
            InstrumentationVersion);

        private static readonly Meter Meter = new(
            HeimdallDiagnostics.MeterName,
            InstrumentationVersion);

        private static readonly Counter<long> ContentActionRequests = Meter.CreateCounter<long>(
            HeimdallDiagnostics.ContentActionRequestsMetricName,
            description: "Number of Heimdall content action requests.");

        private static readonly Histogram<double> ContentActionDuration = Meter.CreateHistogram<double>(
            HeimdallDiagnostics.ContentActionDurationMetricName,
            unit: "s",
            description: "Duration of Heimdall content action requests.");

        private static readonly Histogram<long> ContentActionRequestBodySize = Meter.CreateHistogram<long>(
            HeimdallDiagnostics.ContentActionRequestBodySizeMetricName,
            unit: "By",
            description: "Size of Heimdall content action request bodies.");

        private static readonly Histogram<long> ContentActionResponseBodySize = Meter.CreateHistogram<long>(
            HeimdallDiagnostics.ContentActionResponseBodySizeMetricName,
            unit: "By",
            description: "Size of rendered Heimdall content action response bodies.");

        private static readonly Counter<long> ContentActionExceptions = Meter.CreateCounter<long>(
            HeimdallDiagnostics.ContentActionExceptionsMetricName,
            description: "Number of exceptions handled by the Heimdall content action pipeline.");

        private static readonly Counter<long> ContentActionCancellations = Meter.CreateCounter<long>(
            HeimdallDiagnostics.ContentActionCancellationsMetricName,
            description: "Number of cancelled Heimdall content action requests.");

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

        internal static ContentActionTelemetryScope StartContentAction(HttpContext context)
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

        internal sealed class ContentActionTelemetryScope : IDisposable
        {
            private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
            private long? _requestBodySize;
            private readonly Activity? _activity;
            private string _actionId = UnresolvedActionId;
            private bool _completed;

            internal ContentActionTelemetryScope(long? requestBodySize)
            {
                _requestBodySize = requestBodySize;
                _activity = ActivitySource.StartActivity(
                    HeimdallDiagnostics.ContentActionActivityName,
                    ActivityKind.Internal);
                _activity?.SetTag(HeimdallDiagnostics.ActionIdTagName, _actionId);
            }

            internal void SetActionId(string actionId)
            {
                _actionId = string.IsNullOrWhiteSpace(actionId) ? UnresolvedActionId : actionId;
                _activity?.SetTag(HeimdallDiagnostics.ActionIdTagName, _actionId);
            }

            internal void SetRequestBodySize(long requestBodySize)
            {
                if (requestBodySize >= 0)
                    _requestBodySize = requestBodySize;
            }

            internal bool ShouldMeasureRequestBodySize
                => !_requestBodySize.HasValue && ContentActionRequestBodySize.Enabled;

            internal bool ShouldMeasureResponseBodySize
                => ContentActionResponseBodySize.Enabled;

            internal void Complete(int statusCode, long? responseBodySize = null, string? outcome = null)
            {
                if (_completed)
                    return;

                _completed = true;
                outcome ??= statusCode >= StatusCodes.Status400BadRequest ? "error" : "success";

                var tags = CreateContentActionTags(statusCode, outcome);
                ContentActionRequests.Add(1, tags);
                ContentActionDuration.Record(
                    Stopwatch.GetElapsedTime(_startedTimestamp).TotalSeconds,
                    tags);

                if (_requestBodySize is >= 0)
                    ContentActionRequestBodySize.Record(_requestBodySize.Value, tags);

                if (responseBodySize is >= 0)
                    ContentActionResponseBodySize.Record(responseBodySize.Value, tags);

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
                    { HeimdallDiagnostics.ActionIdTagName, _actionId },
                    { HeimdallDiagnostics.ErrorTypeTagName, errorType }
                };

                ContentActionExceptions.Add(1, tags);
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
                    { HeimdallDiagnostics.ActionIdTagName, _actionId },
                    { HeimdallDiagnostics.CancellationReasonTagName, reason }
                };

                ContentActionCancellations.Add(1, tags);
                _activity?.SetTag(HeimdallDiagnostics.CancellationReasonTagName, reason);
                Complete(statusCode, outcome: reason == "timeout" ? "timeout" : "cancelled");
            }

            public void Dispose()
            {
                if (!_completed)
                    Complete(StatusCodes.Status500InternalServerError, outcome: "error");

                _activity?.Dispose();
            }

            private TagList CreateContentActionTags(int statusCode, string outcome)
            {
                var tags = new TagList
                {
                    { HeimdallDiagnostics.ActionIdTagName, _actionId },
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
