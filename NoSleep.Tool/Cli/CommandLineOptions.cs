// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool.Cli;

using System;

/// <summary>
/// What the user asked for on the command line.
/// </summary>
public sealed class CommandLineOptions
{
	/// <summary>
	/// Gets a value indicating whether the tray icon was explicitly asked for with <c>--tray</c>.
	/// </summary>
	/// <remarks>
	/// Distinct from "the tray is wanted": with neither switch given the tray is used when the machine has a
	/// desktop session to put it in, and this says the user overrode that.
	/// </remarks>
	public bool TrayRequested { get; init; }

	/// <summary>
	/// Gets a value indicating whether <c>--no-tray</c> was given.
	/// </summary>
	public bool TraySuppressed { get; init; }

	/// <summary>
	/// Gets a value indicating whether the display should be kept awake as well as the system.
	/// </summary>
	public bool KeepDisplayAwake { get; init; }

	/// <summary>
	/// Gets a value indicating whether the tray should start with keep-awake turned off.
	/// </summary>
	public bool StartDisabled { get; init; }

	/// <summary>
	/// Gets how long to stay awake before releasing and exiting, or <see langword="null"/> to stay awake until
	/// stopped.
	/// </summary>
	public TimeSpan? Duration { get; init; }

	/// <summary>
	/// Gets a value indicating whether to print what NoSleep can do on this machine and exit.
	/// </summary>
	public bool ShowStatus { get; init; }

	/// <summary>
	/// Gets a value indicating whether to print usage and exit.
	/// </summary>
	public bool ShowHelp { get; init; }

	/// <summary>
	/// Gets a value indicating whether to print the version and exit.
	/// </summary>
	public bool ShowVersion { get; init; }

	/// <summary>
	/// Gets the reason recorded with the inhibitor, shown by the platform's own tooling.
	/// </summary>
	public string Reason { get; init; } = SleepBlockRequest.DefaultReason;

	/// <summary>
	/// Builds the request these options describe.
	/// </summary>
	/// <returns>The request to hand to a <see cref="KeepAwakeController"/>.</returns>
	public SleepBlockRequest ToRequest() => new(KeepDisplayAwake, Reason);
}
