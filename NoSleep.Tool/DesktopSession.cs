// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool;

using System;

/// <summary>
/// Works out whether this process has a desktop session to put a tray icon in.
/// </summary>
/// <remarks>
/// Starting the windowing subsystem on a machine with no display does not fail politely - it throws out of
/// Avalonia's platform initialisation, after NoSleep has already printed that it is keeping the machine awake.
/// Checking first lets a headless run fall back to the console instead.
/// </remarks>
public static class DesktopSession
{
	/// <summary>
	/// Gets a value indicating whether a tray icon can be shown.
	/// </summary>
	public static bool IsAvailable => !OperatingSystem.IsLinux() || HasLinuxDisplay;

	/// <summary>
	/// Gets a sentence explaining <see cref="IsAvailable"/>, for <c>--status</c> and for the fallback notice.
	/// </summary>
	public static string Explanation
	{
		get
		{
			if (!OperatingSystem.IsLinux())
			{
				return "available";
			}

			return HasLinuxDisplay
				? "available (DISPLAY or WAYLAND_DISPLAY is set)"
				: "unavailable (neither DISPLAY nor WAYLAND_DISPLAY is set)";
		}
	}

	private static bool HasLinuxDisplay =>
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
		|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
}
