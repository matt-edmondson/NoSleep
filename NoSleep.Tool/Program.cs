// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool;

using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using ktsu.NoSleep.Contracts;
using ktsu.NoSleep.Tool.Cli;
using ktsu.NoSleep.Tool.Tray;

/// <summary>
/// Entry point for the <c>nosleep</c> .NET tool.
/// </summary>
internal static class Program
{
	private const int ExitSuccess = 0;
	private const int ExitFailure = 1;
	private const int ExitUsage = 2;

	/// <summary>Set to <c>1</c> to print the full exception behind a tray failure.</summary>
	private const string DebugEnvironmentVariable = "NOSLEEP_DEBUG";

	/// <summary>
	/// Runs the tool.
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	/// <returns>0 on success, 1 when the platform refused, 2 when the command line was wrong.</returns>
	internal static int Main(string[] args)
	{
		if (!CommandLineParser.TryParse(args, out CommandLineOptions options, out string error))
		{
			Console.Error.WriteLine($"nosleep: {error}");
			Console.Error.WriteLine();
			Console.Error.WriteLine(HelpText.Usage);
			return ExitUsage;
		}

		if (options.ShowHelp)
		{
			Console.WriteLine(HelpText.Usage);
			return ExitSuccess;
		}

		if (options.ShowVersion)
		{
			Console.WriteLine(HelpText.Version);
			return ExitSuccess;
		}

		if (options.ShowStatus)
		{
			using ISleepBlocker blocker = SleepBlockerFactory.Create();
			Console.WriteLine(StatusReport.Build(blocker));
			return blocker.IsSupported ? ExitSuccess : ExitFailure;
		}

		return UseTray(options) ? RunTray(options) : RunConsole(options);
	}

	/// <summary>
	/// Decides between the tray and the console.
	/// </summary>
	/// <remarks>
	/// <c>--no-tray</c> always wins, and <c>--tray</c> overrides the detection for the machines where it
	/// guesses wrong - a working D-Bus status-notifier host with no <c>DISPLAY</c> set, say. Otherwise the
	/// tray is the default wherever there is a session to put it in.
	/// </remarks>
	private static bool UseTray(CommandLineOptions options)
	{
		if (options.TraySuppressed)
		{
			return false;
		}

		return options.TrayRequested || DesktopSession.IsAvailable;
	}

	private static int RunConsole(CommandLineOptions options)
	{
		using KeepAwakeController controller = new();
		controller.SetRequest(options.ToRequest());
		return ConsoleRunner.Run(controller, options);
	}

	[SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Any failure to bring up a tray icon is recoverable by falling back to the console, and the windowing backends do not report those failures through a common exception type.")]
	private static int RunTray(CommandLineOptions options)
	{
		// Get() is the singleton the static QueueSave/SaveIfRequired helpers write back, so the tray has to
		// mutate that instance rather than a private LoadOrCreate() copy.
		TrayPreferences preferences = TrayPreferences.Get();

		// An explicit switch beats what the tray remembered; without one, the tray comes back the way it was
		// left.
		bool keepDisplayAwake = options.KeepDisplayAwake || preferences.KeepDisplayAwake;
		bool startEnabled = !options.StartDisabled && preferences.KeepAwake;

		using KeepAwakeController controller = new();
		controller.SetRequest(options.ToRequest() with { KeepDisplayAwake = keepDisplayAwake });

		if (startEnabled)
		{
			try
			{
				controller.Enable();
			}
			catch (Exception ex) when (ex is SleepBlockException or PlatformNotSupportedException)
			{
				// Not fatal: the tray still comes up so the user can see why, and try again once whatever was
				// missing is installed.
				Console.Error.WriteLine($"nosleep: {ex.Message}");
			}
		}

		TrayApplication? app = null;

		try
		{
			// StartWithClassicDesktopLifetime blocks until the tray quits, so the using above covers the
			// controller for the whole run.
			return AppBuilder.Configure(() => app = new TrayApplication(controller, preferences, options.Duration))
				.UsePlatformDetect()
				.With(new MacOSPlatformOptions { ShowInDock = false })
				.StartWithClassicDesktopLifetime([], Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
		}
		catch (Exception ex)
		{
			if (Environment.GetEnvironmentVariable(DebugEnvironmentVariable) == "1")
			{
				Console.Error.WriteLine(ex.ToString());
			}

			// The tray already ran and quit; this is Avalonia's teardown throwing after the fact (see
			// TrayApplication.HasStarted). Restarting in the terminal here would keep the machine awake after
			// the user asked NoSleep to stop.
			if (app?.HasStarted == true)
			{
				return ExitSuccess;
			}

			// Otherwise the windowing stack refused after the session check passed - a DISPLAY pointing at
			// nothing, a missing libX11, no status-notifier host on the bus - and it reports those as plain
			// exceptions of whatever type the backend felt like (X11 throws Exception("XOpenDisplay failed")).
			// There is no useful list to filter on, and every one of them means the same thing here: no tray,
			// so use the terminal. Letting it escape instead would kill a process the user asked to keep
			// their machine awake.
			Console.Error.WriteLine($"nosleep: could not show a tray icon ({ex.Message}). Staying in the terminal instead.");
			controller.Dispose();
			return RunConsole(options);
		}
	}
}
