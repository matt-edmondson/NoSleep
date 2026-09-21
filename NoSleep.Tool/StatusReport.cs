// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool;

using System;
using System.Runtime.InteropServices;
using System.Text;
using ktsu.NoSleep.Contracts;
using ktsu.NoSleep.Platforms;
using ktsu.NoSleep.Tool.Cli;

/// <summary>
/// Builds the text behind <c>nosleep --status</c>.
/// </summary>
/// <remarks>
/// Sleep inhibition fails quietly by nature - the machine simply sleeps anyway, hours later, with nobody
/// watching - so the tool needs a way to say up front what it can and cannot do here.
/// </remarks>
public static class StatusReport
{
	/// <summary>
	/// Describes what NoSleep can do on this machine.
	/// </summary>
	/// <param name="blocker">The blocker for the current platform.</param>
	/// <returns>A multi-line report, without a trailing newline.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="blocker"/> is <see langword="null"/>.</exception>
	public static string Build(ISleepBlocker blocker)
	{
		Ensure.NotNull(blocker);

		StringBuilder report = new();
		report.AppendLine(HelpText.Version);
		report.AppendLine($"Platform:      {RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.RuntimeIdentifier})");
		report.AppendLine($"Mechanism:     {blocker.Mechanism}");
		report.AppendLine($"Keep awake:    {(blocker.IsSupported ? "supported" : "not supported on this machine")}");
		report.AppendLine($"Keep display:  {DescribeDisplaySupport(blocker)}");
		report.Append($"Tray icon:     {DesktopSession.Explanation}");

		return report.ToString();
	}

	private static string DescribeDisplaySupport(ISleepBlocker blocker)
	{
		if (!blocker.IsSupported)
		{
			return "not supported on this machine";
		}

		// Windows and macOS keep the screen lit through the same call that keeps the system awake. Linux
		// splits the two, and a box with only a logind lock can do one and not the other.
		return OperatingSystem.IsLinux() && blocker is LinuxSleepBlocker linux && !linux.SupportsDisplay
			? "not supported (needs gnome-session-inhibit)"
			: "supported";
	}
}
