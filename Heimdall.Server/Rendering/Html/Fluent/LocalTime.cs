using System.Globalization;

namespace Heimdall.Server.Rendering
{
	public static partial class FluentHtml
	{
		public sealed partial class ElementBuilder
		{
			/// <summary>
			/// Renders an absolute time value with a server-side fallback and marks the element for
			/// conversion to the browser user's local timezone by the Heimdall web runtime.
			/// </summary>
			/// <param name="value">The absolute time value to render.</param>
			/// <param name="format">
			/// A supported C#-style date/time format. Defaults to the general date/time pattern.
			/// </param>
			/// <returns>The current builder instance.</returns>
			/// <remarks>
			/// This method owns the element's localizable text content. The fallback uses the current
			/// server request culture and the offset carried by <paramref name="value"/>. Applications
			/// that require a predetermined timezone should convert and render the value directly instead.
			/// Supported standard formats are <c>d</c>, <c>D</c>, <c>t</c>, <c>T</c>, <c>g</c>, and
			/// <c>G</c>. Supported custom tokens are <c>d</c>, <c>M</c>, <c>y</c>, <c>h</c>, <c>H</c>,
			/// <c>m</c>, <c>s</c>, <c>t</c>, <c>f</c> (up to milliseconds), and <c>z</c>, including
			/// quoted and escaped literals.
			/// </remarks>
			public ElementBuilder LocalizeTime(
				DateTimeOffset value,
				string format = LocalTimeFormatting.DefaultFormat)
			{
				var normalizedFormat = LocalTimeFormatting.NormalizeAndValidateFormat(format);

				_parts.Add(Html.Attr(
					LocalTimeFormatting.ValueAttribute,
					LocalTimeFormatting.ToBrowserTimestamp(value)));
				_parts.Add(Html.Attr(LocalTimeFormatting.FormatAttribute, normalizedFormat));
				_parts.Add(Html.Text(value.ToString(normalizedFormat, CultureInfo.CurrentCulture)));

				return this;
			}

			/// <summary>
			/// Renders an absolute <see cref="DateTime"/> value with a server-side fallback and marks
			/// the element for conversion to the browser user's local timezone.
			/// </summary>
			/// <param name="value">
			/// A UTC or local time value. <see cref="DateTimeKind.Unspecified"/> is rejected because it
			/// does not identify an absolute instant.
			/// </param>
			/// <param name="format">
			/// A supported C#-style date/time format. Defaults to the general date/time pattern.
			/// </param>
			/// <returns>The current builder instance.</returns>
			public ElementBuilder LocalizeTime(
				DateTime value,
				string format = LocalTimeFormatting.DefaultFormat)
			{
				if (value.Kind == DateTimeKind.Unspecified)
				{
					throw new ArgumentException(
						"LocalizeTime requires an absolute point in time. Use DateTimeOffset or a DateTime whose Kind is Utc or Local.",
						nameof(value));
				}

				return LocalizeTime(new DateTimeOffset(value), format);
			}
		}
	}
}
