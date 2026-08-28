using Heimdall.Server.Rendering;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Heimdall.Server.Tests;

public sealed class LocalTimeRenderingTests
{
    public static TheoryData<string, string> SupportedFormatCases => new()
    {
        { "d", "08/26/2026" },
        { "D", "Wednesday, 26 August 2026" },
        { "t", "18:05" },
        { "T", "18:05:07" },
        { "g", "08/26/2026 18:05" },
        { "G", "08/26/2026 18:05:07" },
        { "%d", "26" },
        { "dd", "26" },
        { "ddd", "Wed" },
        { "dddd", "Wednesday" },
        { "%M", "8" },
        { "MM", "08" },
        { "MMM", "Aug" },
        { "MMMM", "August" },
        { "%y", "26" },
        { "yy", "26" },
        { "yyy", "2026" },
        { "yyyy", "2026" },
        { "%h", "6" },
        { "hh", "06" },
        { "%H", "18" },
        { "HH", "18" },
        { "%m", "5" },
        { "mm", "05" },
        { "%s", "7" },
        { "ss", "07" },
        { "%t", "P" },
        { "tt", "PM" },
        { "%f", "1" },
        { "ff", "12" },
        { "fff", "123" },
        { "%z", "+2" },
        { "zz", "+02" },
        { "zzz", "+02:00" },
        { "'literal' yyyy", "literal 2026" },
        { "\"double literal\" yyyy", "double literal 2026" },
        { "yyyy \\y", "2026 y" },
        { "yyyy-MM-dd'T'HH:mm:ss.fff zzz", "2026-08-26T18:05:07.123 +02:00" }
    };

    public static TheoryData<string> UnsupportedFormatCases => new()
    {
        "M", "m", "y", "s", "f", "z", "h", "H",
        "O", "o", "R", "r", "U", "u", "Y",
        "ddddd", "MMMMM", "yyyyy", "hhh", "HHH", "mmm", "sss", "ttt", "ffff", "zzzz",
        "yyyy K", "yyyy F", "gg", "%D", "%", "yyyy-MM-dd\\", "yyyy 'unfinished", "yyyy \"unfinished"
    };

    [Fact]
    public void LocalizeTime_RendersCanonicalTimestampFormatAndFallback()
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = new DateTimeOffset(2026, 8, 26, 18, 30, 5, 123, TimeSpan.FromHours(2));

        var html = FluentHtml.Span(span => span
            .Class("timestamp")
            .LocalizeTime(value, "yyyy-MM-dd'T'HH:mm:ss.fff zzz"))
            .ToHtmlString();

        Assert.Equal(
            "<span class=\"timestamp\" " +
            "heimdall-time=\"2026-08-26T16:30:05.123Z\" " +
            "heimdall-time-format=\"yyyy-MM-dd&#x27;T&#x27;HH:mm:ss.fff zzz\">" +
            "2026-08-26T18:30:05.123 &#x2B;02:00</span>",
            html);
    }

    [Fact]
    public void LocalizeTime_UsesGeneralFormatByDefault()
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = new DateTimeOffset(2026, 8, 26, 18, 30, 5, TimeSpan.Zero);

        var html = FluentHtml.Div(div => div.LocalizeTime(value)).ToHtmlString();

        Assert.Equal(
            "<div heimdall-time=\"2026-08-26T18:30:05.000Z\" " +
            "heimdall-time-format=\"G\">08/26/2026 18:30:05</div>",
            html);
    }

    [Fact]
    public void LocalizeTime_AcceptsUtcDateTime()
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = new DateTime(2026, 8, 26, 18, 30, 5, DateTimeKind.Utc);

        var html = FluentHtml.Tag("time", time => time.LocalizeTime(value, "yyyy-MM-dd HH:mm"))
            .ToHtmlString();

        Assert.Contains("heimdall-time=\"2026-08-26T18:30:05.000Z\"", html);
        Assert.EndsWith(">2026-08-26 18:30</time>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizeTime_AcceptsLocalDateTimeAsAnAbsoluteInstant()
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = new DateTime(2026, 8, 26, 18, 30, 5, DateTimeKind.Local);
        var expectedTimestamp = new DateTimeOffset(value).UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture);

        var html = FluentHtml.Span(span => span.LocalizeTime(value, "G")).ToHtmlString();

        Assert.Contains($"heimdall-time=\"{expectedTimestamp}\"", html);
    }

    [Fact]
    public void LocalizeTime_RejectsUnspecifiedDateTime()
    {
        var value = new DateTime(2026, 8, 26, 18, 30, 5, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() =>
            FluentHtml.Span(span => span.LocalizeTime(value)));

        Assert.Equal("value", exception.ParamName);
        Assert.Contains("absolute point in time", exception.Message);
    }

    [Theory]
    [MemberData(nameof(SupportedFormatCases))]
    public void LocalizeTime_RendersEverySupportedFormat(string format, string expectedFallback)
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = new DateTimeOffset(2026, 8, 26, 18, 5, 7, 123, TimeSpan.FromHours(2));

        var html = FluentHtml.Span(span => span.LocalizeTime(value, format)).ToHtmlString();

        Assert.Contains("heimdall-time=\"2026-08-26T16:05:07.123Z\"", html);
        Assert.EndsWith(
            $">{HtmlEncoder.Default.Encode(expectedFallback)}</span>",
            html,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("O")]
    [InlineData("yyyy K")]
    [InlineData("yyyy-MM-dd ffff")]
    [InlineData("yyyy 'unfinished")]
    [InlineData("yyyy-MM-dd\\")]
    [InlineData("yyyy %")]
    public void LocalizeTime_RejectsUnsupportedOrMalformedFormats(string format)
    {
        var value = DateTimeOffset.Parse("2026-08-26T18:30:05Z", CultureInfo.InvariantCulture);

        var exception = Assert.Throws<ArgumentException>(() =>
            FluentHtml.Span(span => span.LocalizeTime(value, format)));

        Assert.Equal("format", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(UnsupportedFormatCases))]
    public void LocalizeTime_RejectsEveryUnsupportedFormatShape(string format)
    {
        var value = DateTimeOffset.Parse("2026-08-26T18:30:05Z", CultureInfo.InvariantCulture);

        var exception = Assert.Throws<ArgumentException>(() =>
            FluentHtml.Span(span => span.LocalizeTime(value, format)));

        Assert.Equal("format", exception.ParamName);
    }

    [Fact]
    public void LocalizeTime_RejectsFormatsLongerThanTheClientLimit()
    {
        var value = DateTimeOffset.Parse("2026-08-26T18:30:05Z", CultureInfo.InvariantCulture);
        var format = new string('-', 257);

        var exception = Assert.Throws<ArgumentException>(() =>
            FluentHtml.Span(span => span.LocalizeTime(value, format)));

        Assert.Equal("format", exception.ParamName);
        Assert.Contains("256", exception.Message);
    }

    [Fact]
    public void LocalizeTime_AcceptsFormatAtTheClientLimit()
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = DateTimeOffset.Parse("2026-08-26T18:30:05Z", CultureInfo.InvariantCulture);
        var format = new string('-', 256);

        var html = FluentHtml.Span(span => span.LocalizeTime(value, format)).ToHtmlString();

        Assert.EndsWith($">{format}</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizeTime_UsesCurrentCultureForTheFallback()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        using var _ = new CurrentCultureScope(culture);
        var value = new DateTimeOffset(2026, 8, 26, 18, 5, 7, TimeSpan.Zero);
        const string format = "dddd d MMMM yyyy HH:mm";
        var expected = value.ToString(format, culture);

        var html = FluentHtml.Span(span => span.LocalizeTime(value, format)).ToHtmlString();

        Assert.EndsWith(
            $">{HtmlEncoder.Default.Encode(expected)}</span>",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizeTime_NormalizesNullAndEmptyFormatsToGeneralFormat()
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = DateTimeOffset.Parse("2026-08-26T18:30:05Z", CultureInfo.InvariantCulture);

        var nullFormat = FluentHtml.Span(span => span.LocalizeTime(value, null!)).ToHtmlString();
        var emptyFormat = FluentHtml.Span(span => span.LocalizeTime(value, string.Empty)).ToHtmlString();

        Assert.Equal(nullFormat, emptyFormat);
        Assert.Contains("heimdall-time-format=\"G\"", nullFormat);
        Assert.EndsWith(">08/26/2026 18:30:05</span>", nullFormat, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("d")]
    [InlineData("D")]
    [InlineData("t")]
    [InlineData("T")]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("%d")]
    [InlineData("ddd, MMMM d, yyyy 'at' h:mm:ss tt zzz")]
    [InlineData("yyyy-MM-dd'T'HH:mm:ss.fff")]
    public void LocalizeTime_AcceptsDocumentedFormats(string format)
    {
        var value = DateTimeOffset.Parse("2026-08-26T18:30:05.123Z", CultureInfo.InvariantCulture);

        var html = FluentHtml.Span(span => span.LocalizeTime(value, format)).ToHtmlString();

        Assert.Contains("heimdall-time=", html);
        Assert.Contains("heimdall-time-format=", html);
        Assert.DoesNotContain("heimdall-time-zone", html);
    }

    [Fact]
    public void LocalizeTime_EncodesFormatAndFallbackContent()
    {
        using var _ = new CurrentCultureScope(CultureInfo.InvariantCulture);
        var value = DateTimeOffset.Parse("2026-08-26T18:30:05Z", CultureInfo.InvariantCulture);

        var html = FluentHtml.Span(span =>
            span.LocalizeTime(value, "yyyy '<&\"'"))
            .ToHtmlString();

        Assert.Contains("heimdall-time-format=\"yyyy &#x27;&lt;&amp;&quot;&#x27;\"", html);
        Assert.EndsWith(">2026 &lt;&amp;&quot;</span>", html, StringComparison.Ordinal);
    }

    private sealed class CurrentCultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;

        public CurrentCultureScope(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}
