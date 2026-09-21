// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool;

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using ktsu.NoSleep.Tool.Cli;

/// <summary>
/// The headless mode: hold the inhibitor and block until interrupted or until the duration runs out.
/// </summary>
/// <remarks>
/// This is the mode a CI agent, an SSH session, or a script gets. It has no dependency on the windowing
/// stack, which is what makes <c>nosleep --no-tray</c> usable on a machine with no desktop at all.
/// </remarks>
public static class ConsoleRunner
{
	/// <summary>
	/// Keeps the machine awake until the process is asked to stop.
	/// </summary>
	/// <param name="controller">The controller to drive.</param>
	/// <param name="options">The options the run was started with.</param>
	/// <returns>The process exit code.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="controller"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
	public static int Run(KeepAwakeController controller, CommandLineOptions options)
	{
		Ensure.NotNull(controller);
		Ensure.NotNull(options);

		try
		{
			controller.Enable();
		}
		catch (Exception ex) when (ex is SleepBlockException or PlatformNotSupportedException)
		{
			Console.Error.WriteLine($"nosleep: {ex.Message}");
			return 1;
		}

		using ManualResetEventSlim stopping = new(initialState: false);

		void RequestStop() => stopping.Set();

		void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
		{
			// Take the interrupt rather than letting the runtime tear the process down, so the inhibitor is
			// released through the normal path instead of being left to the operating system to clean up.
			eventArgs.Cancel = true;
			RequestStop();
		}

		Console.CancelKeyPress += OnCancelKeyPress;

		// SIGTERM is how a service manager or `docker stop` ends this, and it does not go through
		// CancelKeyPress.
		using PosixSignalRegistration termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
		{
			context.Cancel = true;
			RequestStop();
		});

		try
		{
			Console.WriteLine(DescribeRun(controller, options));

			if (options.Duration is TimeSpan window)
			{
				stopping.Wait(window);
			}
			else
			{
				stopping.Wait();
			}
		}
		finally
		{
			Console.CancelKeyPress -= OnCancelKeyPress;
			controller.Disable();
		}

		Console.WriteLine("nosleep: released. This machine may sleep again.");
		return 0;
	}

	private static string DescribeRun(KeepAwakeController controller, CommandLineOptions options)
	{
		string scope = controller.KeepDisplayAwake ? "system and display" : "system";
		string until = options.Duration is TimeSpan window
			? string.Create(CultureInfo.InvariantCulture, $"for {window:g}")
			: "until interrupted";

		return string.Create(
			CultureInfo.InvariantCulture,
			$"nosleep: keeping {scope} awake {until} via {controller.Mechanism}. Press Ctrl+C to stop.");
	}
}
