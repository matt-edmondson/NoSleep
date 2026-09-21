// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool.Cli;

using System;
using System.Collections.Generic;

/// <summary>
/// Turns <c>nosleep</c>'s arguments into <see cref="CommandLineOptions"/>.
/// </summary>
/// <remarks>
/// The whole surface is eight switches with no subcommands, so it is parsed here rather than pulled in from a
/// parser library: a hand-written loop is smaller than the configuration one would take, and it keeps the
/// error messages in the same voice as the rest of the tool.
/// </remarks>
public static class CommandLineParser
{
	/// <summary>
	/// Parses the arguments.
	/// </summary>
	/// <param name="args">The raw arguments, as handed to <c>Main</c>.</param>
	/// <param name="options">Receives the parsed options when parsing succeeds.</param>
	/// <param name="error">Receives a message describing the problem when parsing fails.</param>
	/// <returns><see langword="true"/> when the arguments were understood.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
	public static bool TryParse(IReadOnlyList<string> args, out CommandLineOptions options, out string error)
	{
		Ensure.NotNull(args);

		bool tray = false;
		bool noTray = false;
		bool display = false;
		bool startDisabled = false;
		bool status = false;
		bool help = false;
		bool version = false;
		TimeSpan? duration = null;
		string reason = SleepBlockRequest.DefaultReason;

		for (int index = 0; index < args.Count; index++)
		{
			string argument = args[index];

			switch (argument)
			{
				case "-t" or "--tray":
					tray = true;
					break;

				case "--no-tray":
					noTray = true;
					break;

				case "-d" or "--display":
					display = true;
					break;

				case "-o" or "--off":
					startDisabled = true;
					break;

				case "-s" or "--status":
					status = true;
					break;

				case "-h" or "--help" or "-?":
					help = true;
					break;

				case "-v" or "--version":
					version = true;
					break;

				case "-f" or "--for":
					if (!TryTakeValue(args, ref index, argument, out string durationText, out error))
					{
						options = new CommandLineOptions();
						return false;
					}

					if (!DurationParser.TryParse(durationText, out TimeSpan parsedDuration))
					{
						options = new CommandLineOptions();
						error = $"'{durationText}' is not a duration. Try 45s, 90m, 2h, or 1h30m.";
						return false;
					}

					duration = parsedDuration;
					break;

				case "-r" or "--reason":
					if (!TryTakeValue(args, ref index, argument, out reason, out error))
					{
						options = new CommandLineOptions();
						return false;
					}

					break;

				default:
					options = new CommandLineOptions();
					error = argument.StartsWith('-')
						? $"Unknown option '{argument}'. Run 'nosleep --help' for the list."
						: $"Unexpected argument '{argument}'. NoSleep takes options only.";
					return false;
			}
		}

		if (tray && noTray)
		{
			options = new CommandLineOptions();
			error = "--tray and --no-tray contradict each other.";
			return false;
		}

		options = new CommandLineOptions
		{
			TrayRequested = tray,
			TraySuppressed = noTray,
			KeepDisplayAwake = display,
			StartDisabled = startDisabled,
			Duration = duration,
			ShowStatus = status,
			ShowHelp = help,
			ShowVersion = version,
			Reason = reason,
		};

		error = string.Empty;
		return true;
	}

	private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, string option, out string value, out string error)
	{
		if (index + 1 >= args.Count)
		{
			value = string.Empty;
			error = $"'{option}' needs a value.";
			return false;
		}

		index++;
		value = args[index];
		error = string.Empty;
		return true;
	}
}
