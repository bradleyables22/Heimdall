using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Heimdall.Server.Rendering;

public static partial class FluentHeimdall
{
    public readonly partial struct HeimdallBuilder : IReadOnlyDictionary<string, object>
    {
        private static readonly ImmutableDictionary<string, object> EmptyAttributes =
            ImmutableDictionary.Create<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly FluentHtml.ElementBuilder? _b;
        private readonly ImmutableDictionary<string, object>? _attributes;
        private readonly string? _fallbackText;

        private HeimdallBuilder(FluentHtml.ElementBuilder? element,
            ImmutableDictionary<string, object>? attributes, string? fallbackText)
        {
            _b = element;
            _attributes = attributes;
            _fallbackText = fallbackText;
        }

        /// <summary>The server-culture fallback text supplied by <c>LocalizeTime</c>.</summary>
        /// <remarks>In Blazor, render this text inside the element receiving the attributes.</remarks>
        public string? FallbackText => _fallbackText;

        private ImmutableDictionary<string, object> Snapshot()
        {
            if (_b is null) return _attributes ?? EmptyAttributes;
            var result = EmptyAttributes;
            foreach (var part in _b.ToArray())
                if (part is Html.HtmlAttr attribute && !attribute.IsEmpty)
                    result = result.SetItem(attribute.Name, AttributeValue(attribute));
            return result;
        }

        private static object AttributeValue(Html.HtmlAttr attribute)
            => attribute.Kind == Html.AttrKind.Boolean ? true : attribute.Value;

        private HeimdallBuilder AddAttributes(Html.HtmlAttr attribute)
        {
            if (_b is not null)
            {
                _b.Add(attribute);
                return this;
            }
            if (attribute.IsEmpty) return this;
            return new HeimdallBuilder(null,
                (_attributes ?? EmptyAttributes).SetItem(attribute.Name, AttributeValue(attribute)), _fallbackText);
        }

        private HeimdallBuilder AddAttributes(params Html.HtmlAttr[] attributes)
        {
            if (_b is not null)
            {
                foreach (var attribute in attributes) _b.Add(attribute);
                return this;
            }

            var result = _attributes ?? EmptyAttributes;
            foreach (var attribute in attributes)
                if (!attribute.IsEmpty)
                    result = result.SetItem(attribute.Name, AttributeValue(attribute));
            return new HeimdallBuilder(null, result, _fallbackText);
        }

        /// <summary>Sets the named request synchronization group.</summary>
        public HeimdallBuilder SyncGroup(string group) => AddAttributes(HeimdallHtml.SyncGroup(group));

        /// <summary>Configures browser-local time attributes and a server-culture fallback.</summary>
        public HeimdallBuilder LocalizeTime(DateTimeOffset value, string format = "G")
        {
            var normalizedFormat = LocalTimeFormatting.NormalizeAndValidateFormat(format);
            var fallback = value.ToString(normalizedFormat, CultureInfo.CurrentCulture);
            if (_b is not null)
            {
                _b.LocalizeTime(value, normalizedFormat);
                return new HeimdallBuilder(_b, null, fallback);
            }
            var result = AddAttributes(
                Html.Attr(LocalTimeFormatting.ValueAttribute, LocalTimeFormatting.ToBrowserTimestamp(value)),
                Html.Attr(LocalTimeFormatting.FormatAttribute, normalizedFormat));
            return new HeimdallBuilder(null, result._attributes, fallback);
        }

        /// <summary>Configures an absolute UTC or local time; unspecified values are rejected.</summary>
        public HeimdallBuilder LocalizeTime(DateTime value, string format = "G")
        {
            if (value.Kind == DateTimeKind.Unspecified)
                throw new ArgumentException("LocalizeTime requires an absolute point in time. Use DateTimeOffset or a DateTime whose Kind is Utc or Local.", nameof(value));
            return LocalizeTime(new DateTimeOffset(value), format);
        }

        /// <summary>The number of effective attributes in this configuration.</summary>
        public int Count => Snapshot().Count;
        /// <summary>The canonical attribute names.</summary>
        public IEnumerable<string> Keys => Snapshot().Keys;
        /// <summary>The unencoded attribute values.</summary>
        public IEnumerable<object> Values => Snapshot().Values;
        /// <summary>Gets an effective attribute value.</summary>
        public object this[string key] => Snapshot()[key];
        /// <summary>Determines whether a canonical attribute is present.</summary>
        public bool ContainsKey(string key) => Snapshot().ContainsKey(key);
        /// <summary>Tries to read an effective attribute value.</summary>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
            => Snapshot().TryGetValue(key, out value);
        /// <summary>Enumerates canonical attributes for Blazor splatting or Razor Tag Helpers.</summary>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Snapshot().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
