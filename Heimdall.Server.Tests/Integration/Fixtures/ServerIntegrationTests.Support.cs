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
    private sealed class PayloadDto
    {
        public string? Name { get; set; }

        public int Count { get; set; }

        public bool Enabled { get; set; }

        public PayloadMode Mode { get; set; }
    }

    private sealed class InstancePayload
    {
        public string? Name { get; set; }
    }

    private sealed class MvcPartialPayload
    {
        public string? ViewName { get; set; }

        public string? Name { get; set; }
    }

    private enum PayloadMode
    {
        Alpha,
        Beta
    }

    private sealed class ServiceLikePayload
    {
        public string? Value { get; set; }
    }

    private sealed class GreetingService(string message)
    {
        public string Message { get; } = message;
    }

    private sealed class ConstructedService
    {
        private static int constructionCount;
        private static bool throwOnConstruct;

        public ConstructedService()
        {
            Interlocked.Increment(ref constructionCount);

            if (throwOnConstruct)
            {
                throw new InvalidOperationException("Service construction should not happen during action discovery.");
            }
        }

        public static int ConstructionCount => constructionCount;

        public static void Reset(bool throwOnConstruct)
        {
            Interlocked.Exchange(ref constructionCount, 0);
            ConstructedService.throwOnConstruct = throwOnConstruct;
        }
    }

    private sealed class ThrowingAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
            => throw new InvalidOperationException("Expected CSRF issuance failure.");

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext)
            => throw new NotSupportedException();

        public Task<bool> IsRequestValidAsync(HttpContext httpContext)
            => Task.FromResult(false);

        public void SetCookieTokenAndHeader(HttpContext httpContext)
            => throw new NotSupportedException();

        public Task ValidateRequestAsync(HttpContext httpContext)
            => throw new AntiforgeryValidationException("Expected antiforgery validation failure.");
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly object gate = new();
        private readonly List<TestLogEntry> entries = [];

        public IReadOnlyList<TestLogEntry> Entries
        {
            get
            {
                lock (gate)
                {
                    return entries.ToArray();
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
            => new TestLogger(categoryName, this);

        public void Add(TestLogEntry entry)
        {
            lock (gate)
            {
                entries.Add(entry);
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger(string categoryName, TestLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => TestLoggerScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            provider.Add(new TestLogEntry(
                categoryName,
                logLevel,
                eventId,
                formatter(state, exception),
                exception));
        }
    }

    private sealed class TestLoggerScope : IDisposable
    {
        public static readonly TestLoggerScope Instance = new();

        private TestLoggerScope()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed record TestLogEntry(
        string Category,
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception);
}
