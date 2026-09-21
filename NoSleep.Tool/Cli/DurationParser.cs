// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool.Cli;

using System;
using System.Globalization;

/// <summary>
/// Reads the duration accepted by <c>--for</c>.
/// </summary>
/// <remarks>
/// <see cref="TimeSpan.TryParse(string, out TimeSpan)"/> reads <c>2</c> as two days and rejects <c>90m</c>
/// outright, neither of which is what someone types when they want to stay awake through a build. This reads
/// the compound form instead: a run of number/unit pairs such as <c>1h30m</c>, with a bare number meaning
/// minutes.
/// </remarks>
public static class DurationParser
{
	/// <summary>
	/// Parses a duration such as <c>45s</c>, <c>90m</c>, <c>2h</c>, <c>1h30m</c>, or a bare <c>90</c> for minutes.
	/// </summary>
	/// <param name="text">The text to read.</param>
	/// <param name="duration">Receives the duration when parsing succeeds.</param>
	/// <returns><see langword="true"/> when <paramref name="text"/> is a positive duration.</returns>
	public static bool TryParse(string? text, out TimeSpan duration)
	{
		duration = TimeSpan.Zero;

		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		ReadOnlySpan<char> span = text.AsSpan().Trim();
		double totalSeconds = 0;
		int index = 0;
		bool sawAnyPart = false;

		while (index < span.Length)
		{
			int digitsStart = index;
			while (index < span.Length && (char.IsAsciiDigit(span[index]) || span[index] == '.'))
			{
				index++;
			}

			if (index == digitsStart)
			{
				return false;
			}

			if (!double.TryParse(span[digitsStart..index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
			{
				return false;
			}

			int unitStart = index;
			while (index < span.Length && char.IsAsciiLetter(span[index]))
			{
				index++;
			}

			// A bare number is only allowed when it is the whole value: "90" means ninety minutes, but "5m5"
			// is a typo, not five minutes and five more.
			bool isWholeValue = digitsStart == 0 && index == span.Length;

			if (!TryGetUnitSeconds(span[unitStart..index], isWholeValue, out double unitSeconds))
			{
				return false;
			}

			totalSeconds += value * unitSeconds;
			sawAnyPart = true;
		}

		if (!sawAnyPart || totalSeconds <= 0 || totalSeconds > TimeSpan.MaxValue.TotalSeconds)
		{
			return false;
		}

		duration = TimeSpan.FromSeconds(totalSeconds);
		return true;
	}

	private static bool TryGetUnitSeconds(ReadOnlySpan<char> unit, bool isWholeValue, out double seconds)
	{
		// A missing unit means minutes: "--for 90" is the shape people reach for, and minutes is the only
		// reading of it that is ever useful.
		seconds = unit.Length switch
		{
			0 => isWholeValue ? 60 : 0,
			_ when unit.Equals("s", StringComparison.OrdinalIgnoreCase) => 1,
			_ when unit.Equals("sec", StringComparison.OrdinalIgnoreCase) => 1,
			_ when unit.Equals("m", StringComparison.OrdinalIgnoreCase) => 60,
			_ when unit.Equals("min", StringComparison.OrdinalIgnoreCase) => 60,
			_ when unit.Equals("h", StringComparison.OrdinalIgnoreCase) => 3600,
			_ when unit.Equals("hr", StringComparison.OrdinalIgnoreCase) => 3600,
			_ when unit.Equals("d", StringComparison.OrdinalIgnoreCase) => 86400,
			_ => 0,
		};

		return seconds > 0;
	}
}
