// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool;

using ktsu.AppDataStorage;

/// <summary>
/// The tray's remembered state, persisted as JSON under the user's application data directory.
/// </summary>
/// <remarks>
/// Only the tray reads this. A command line run is explicit about what it wants, so letting a remembered
/// setting override an argument would be surprising; the console path never touches it.
/// </remarks>
public sealed class TrayPreferences : AppData<TrayPreferences>
{
	/// <summary>
	/// Gets or sets a value indicating whether the display was being kept awake when NoSleep last exited.
	/// </summary>
	public bool KeepDisplayAwake { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether keep-awake was switched on when NoSleep last exited.
	/// </summary>
	public bool KeepAwake { get; set; } = true;
}
