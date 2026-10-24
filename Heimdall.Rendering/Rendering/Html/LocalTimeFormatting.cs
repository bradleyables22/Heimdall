using System.Globalization;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Defines the server-side contract shared with Heimdall's browser-local time formatter.
	/// </summary>
	internal static class LocalTimeFormatting
	{
		internal const string ValueAttribute = "heimdall-time";
		internal const string FormatAttribute = "heimdall-time-format";
		internal const string DefaultFormat = "G";
		internal const int MaximumFormatLength = 256;

		private const string StandardFormats = "dDtTgG";

		internal static string NormalizeAndValidateFormat(string? format)
		{
			var normalized = string.IsNullOrEmpty(format) ? DefaultFormat : format;

			if (normalized.Length > MaximumFormatLength)
			{
				throw new ArgumentException(
					$"Local time formats cannot exceed {MaximumFormatLength} characters.",
					nameof(format));
			}

			if (normalized.Length == 1)
			{
				if (StandardFormats.Contains(normalized[0], StringComparison.Ordinal))
					return normalized;

				throw UnsupportedFormat(normalized, normalized[0]);
			}

			for (var index = 0; index < normalized.Length;)
			{
				var current = normalized[index];

				if (current is '\'' or '"')
				{
					index = SkipQuotedLiteral(normalized, index, current);
					continue;
				}

				if (current == '\\')
				{
					if (index + 1 >= normalized.Length)
						throw new ArgumentException("A local time format cannot end with an escape character.", nameof(format));

					index += 2;
					continue;
				}

				if (current == '%')
				{
					if (index + 1 >= normalized.Length)
						throw new ArgumentException("A local time format cannot end with '%'.", nameof(format));

					var escapedToken = normalized[index + 1];
					if (!IsSupportedToken(escapedToken, 1))
						throw UnsupportedFormat(normalized, escapedToken);

					index += 2;
					continue;
				}

				if (IsTokenLetter(current))
				{
					var count = 1;
					while (index + count < normalized.Length && normalized[index + count] == current)
						count++;

					if (!IsSupportedToken(current, count))
						throw UnsupportedFormat(normalized, current);

					index += count;
					continue;
				}

				if (char.IsLetter(current))
					throw UnsupportedFormat(normalized, current);

				index++;
			}

			return normalized;
		}

		internal static string ToBrowserTimestamp(DateTimeOffset value)
			=> value.UtcDateTime.ToString(
				"yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
				CultureInfo.InvariantCulture);

		private static int SkipQuotedLiteral(string format, int start, char quote)
		{
			for (var index = start + 1; index < format.Length; index++)
			{
				if (format[index] == '\\')
				{
					if (index + 1 >= format.Length)
						break;

					index++;
					continue;
				}

				if (format[index] == quote)
					return index + 1;
			}

			throw new ArgumentException("A local time format contains an unterminated quoted literal.", nameof(format));
		}

		private static bool IsTokenLetter(char value)
			=> value is 'd' or 'M' or 'y' or 'h' or 'H' or 'm' or 's' or 't' or 'f' or 'z';

		private static bool IsSupportedToken(char value, int count)
			=> value switch
			{
				'd' or 'M' => count is >= 1 and <= 4,
				'y' => count is >= 1 and <= 4,
				'h' or 'H' or 'm' or 's' or 't' => count is >= 1 and <= 2,
				'f' => count is >= 1 and <= 3,
				'z' => count is >= 1 and <= 3,
				_ => false
			};

		private static ArgumentException UnsupportedFormat(string format, char token)
			=> new(
				$"Local time format '{format}' contains unsupported token '{token}'. " +
				"Use d, D, t, T, g, or G, or a supported custom date/time format.",
				nameof(format));
	}
}
