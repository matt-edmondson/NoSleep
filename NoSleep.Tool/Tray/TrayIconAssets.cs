// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool.Tray;

using System;
using System.IO;
using System.Reflection;
using Avalonia.Controls;

/// <summary>
/// The two tray images, loaded once from the assembly's embedded resources.
/// </summary>
/// <remarks>
/// The images are embedded rather than shipped beside the assembly because a dotnet tool is installed as a
/// package payload, and a loose PNG next to the dll is one more thing that can go missing on a machine
/// NoSleep will never be debugged on.
/// </remarks>
public static class TrayIconAssets
{
	private const string ResourcePrefix = "ktsu.NoSleep.Tool.Assets.";

	private static readonly Lazy<WindowIcon> ActiveIcon = new(() => Load("tray-active.png"));
	private static readonly Lazy<WindowIcon> IdleIcon = new(() => Load("tray-idle.png"));

	/// <summary>
	/// Gets the icon shown while NoSleep is holding an inhibitor: an open eye.
	/// </summary>
	public static WindowIcon Active => ActiveIcon.Value;

	/// <summary>
	/// Gets the icon shown while the machine is free to sleep: a closed eye.
	/// </summary>
	public static WindowIcon Idle => IdleIcon.Value;

	/// <summary>
	/// Gets the icon for a state.
	/// </summary>
	/// <param name="isActive">Whether NoSleep is currently holding an inhibitor.</param>
	/// <returns>The matching icon.</returns>
	public static WindowIcon For(bool isActive) => isActive ? Active : Idle;

	private static WindowIcon Load(string fileName)
	{
		string resourceName = ResourcePrefix + fileName;
		Assembly assembly = typeof(TrayIconAssets).Assembly;

		using Stream stream = assembly.GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException(
				$"The tray icon '{resourceName}' is missing from {assembly.GetName().Name}. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}.");

		return new WindowIcon(stream);
	}
}
