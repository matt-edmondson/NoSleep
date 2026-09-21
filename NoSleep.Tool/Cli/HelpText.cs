// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool.Cli;

using System.Reflection;

/// <summary>
/// The usage and version text.
/// </summary>
public static class HelpText
{
	/// <summary>
	/// Gets the usage text printed for <c>--help</c> and for a bad command line.
	/// </summary>
	public static string Usage =>
		"""
		nosleep - keep this machine awake.

		Usage:
		  nosleep [options]

		With no options NoSleep starts keeping the machine awake and shows a tray icon when the
		session has somewhere to put one, falling back to staying in the terminal when it does not.

		Options:
		  -t, --tray             Show the tray icon even if no desktop session was detected.
		      --no-tray          Stay in the terminal; never show a tray icon.
		  -d, --display          Keep the display lit as well, not just the system awake.
		  -o, --off              Start with keep-awake switched off (tray only).
		  -f, --for <duration>   Release and exit after a duration: 45s, 90m, 2h, 1h30m.
		                         A bare number means minutes.
		  -r, --reason <text>    Reason recorded with the inhibitor, shown by the platform's
		                         own tooling (pmset -g assertions, systemd-inhibit --list).
		  -s, --status           Print what NoSleep can do on this machine, then exit.
		  -h, --help             Show this help, then exit.
		  -v, --version          Print the version, then exit.

		Examples:
		  nosleep                        Keep awake, with a tray icon if one is possible.
		  nosleep --no-tray --for 2h     Keep awake for two hours from a terminal or a script.
		  nosleep --display --tray       Keep the screen lit too, and show the tray icon.
		  nosleep --off                  Show the tray icon, but start switched off.

		Exit codes: 0 success, 1 the platform refused, 2 the command line was wrong.
		""";

	/// <summary>
	/// Gets the version line printed for <c>--version</c>.
	/// </summary>
	public static string Version
	{
		get
		{
			Assembly assembly = typeof(HelpText).Assembly;
			string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
				?? assembly.GetName().Version?.ToString()
				?? "unknown";

			// The SDK appends the source revision after a '+', which is noise in a version line.
			int plus = version.IndexOf('+', System.StringComparison.Ordinal);
			if (plus >= 0)
			{
				version = version[..plus];
			}

			return $"nosleep {version}";
		}
	}
}
