using System.Text.Json;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Heimdall.Server.Tests;

public sealed partial class StaticSiteGenerationTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"heimdall-ssg-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private sealed class ScopedRenderProbe : IDisposable
    {
        private static int nextInstanceId;
        private static int disposedCount;

        public ScopedRenderProbe()
        {
            InstanceId = Interlocked.Increment(ref nextInstanceId);
        }

        public int InstanceId { get; }

        public static int DisposedCount => Volatile.Read(ref disposedCount);

        public static void Reset()
        {
            Interlocked.Exchange(ref nextInstanceId, 0);
            Interlocked.Exchange(ref disposedCount, 0);
        }

        public void Dispose()
        {
            Interlocked.Increment(ref disposedCount);
        }
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = Path.GetFullPath(contentRootPath);
        }

        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Heimdall.Server.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestWebHostEnvironment : TestHostEnvironment, IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath, string webRootPath)
            : base(contentRootPath)
        {
            WebRootPath = Path.GetFullPath(webRootPath);
        }

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
