// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Tool.Tray;

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

/// <summary>
/// The tray icon: one icon, one menu, no windows.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia gives all three desktop platforms the same tray API - <c>Shell_NotifyIcon</c> on Windows,
/// <c>NSStatusItem</c> on macOS, and a StatusNotifierItem over D-Bus on Linux - which is the whole reason it
/// is here. Nothing else in NoSleep needs a UI framework.
/// </para>
/// <para>
/// There is no XAML: the app builds its menu in code and never calls <c>AvaloniaXamlLoader</c>, so
/// <see cref="Initialize"/> has nothing to do. A tray icon and a native menu need no styles or theme, and
/// skipping XAML keeps the tool's payload and its startup cost down.
/// </para>
/// </remarks>
public sealed class TrayApplication : Application, IDisposable
{
	private readonly KeepAwakeController controller;
	private readonly TrayPreferences preferences;
	private readonly TimeSpan? duration;

	// The menu items exist from construction so the refresh path has nothing to null-check; only the tray
	// icon itself has to wait, because its constructor reaches for a platform handle that does not exist
	// until Avalonia has finished starting.
	private readonly NativeMenuItem statusItem = new() { IsEnabled = false };
	private readonly NativeMenuItem keepAwakeItem = new("Keep awake") { ToggleType = MenuItemToggleType.CheckBox };
	private readonly NativeMenuItem displayItem = new("Keep display awake too") { ToggleType = MenuItemToggleType.CheckBox };
	private readonly NativeMenuItem quitItem = new("Quit NoSleep");

	private TrayIcon? trayIcon;
	private DispatcherTimer? expiryTimer;
	private string? lastError;
	private bool disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="TrayApplication"/> class.
	/// </summary>
	/// <param name="controller">The controller the menu drives.</param>
	/// <param name="preferences">Where the menu's state is remembered between runs.</param>
	/// <param name="duration">How long to stay awake before quitting, or <see langword="null"/> to stay until asked to stop.</param>
	/// <exception cref="ArgumentNullException"><paramref name="controller"/> or <paramref name="preferences"/> is <see langword="null"/>.</exception>
	public TrayApplication(KeepAwakeController controller, TrayPreferences preferences, TimeSpan? duration)
	{
		Ensure.NotNull(controller);
		Ensure.NotNull(preferences);

		this.controller = controller;
		this.preferences = preferences;
		this.duration = duration;
	}

	/// <summary>
	/// Gets a value indicating whether the tray icon came up.
	/// </summary>
	/// <remarks>
	/// Avalonia's Linux tray backend watches the D-Bus status-notifier host from an <c>async void</c> method
	/// whose exception filter stops applying once the icon is disposed, so a perfectly ordinary shutdown can
	/// throw a <see cref="System.Threading.Tasks.TaskCanceledException"/> out of the run loop after the tray
	/// has already done its job. This is how the caller tells that apart from a tray that never started.
	/// </remarks>
	public bool HasStarted { get; private set; }

	/// <inheritdoc/>
	public override void Initialize() => Name = "NoSleep";

	/// <inheritdoc/>
	public override void OnFrameworkInitializationCompleted()
	{
		keepAwakeItem.Click += OnKeepAwakeClicked;
		displayItem.Click += OnKeepDisplayClicked;
		quitItem.Click += OnQuitClicked;

		NativeMenu menu =
		[
			statusItem,
			new NativeMenuItemSeparator(),
			keepAwakeItem,
			displayItem,
			new NativeMenuItemSeparator(),
			quitItem,
		];

		trayIcon = new TrayIcon { Menu = menu, IsVisible = true };

		// A left click is the fastest way to flip the switch on Windows. Linux status-notifier hosts and
		// macOS mostly open the menu instead, which is why the menu carries the same toggle.
		trayIcon.Clicked += OnTrayIconClicked;

		// Registering the icon with the application is what disposes it on shutdown; a bare TrayIcon left
		// behind outlives the process on some Linux panels.
		TrayIcon.SetIcons(this, [trayIcon]);

		controller.StateChanged += OnControllerStateChanged;
		ApplyDuration();
		RefreshMenu();

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Exit += OnExit;
		}

		HasStarted = true;

		base.OnFrameworkInitializationCompleted();
	}

	private void OnTrayIconClicked(object? sender, EventArgs e) => ToggleKeepAwake();

	private void OnKeepAwakeClicked(object? sender, EventArgs e) => ToggleKeepAwake();

	private void OnKeepDisplayClicked(object? sender, EventArgs e)
	{
		bool keepDisplayAwake = !controller.KeepDisplayAwake;

		if (TryRun(() => controller.SetKeepDisplayAwake(keepDisplayAwake)))
		{
			preferences.KeepDisplayAwake = keepDisplayAwake;
			TrayPreferences.QueueSave();
		}

		RefreshMenu();
	}

	private void OnQuitClicked(object? sender, EventArgs e) => Shutdown();

	private void OnControllerStateChanged(object? sender, KeepAwakeStateChangedEventArgs e) =>
		Dispatcher.UIThread.Post(RefreshMenu);

	private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) => Dispose();

	/// <summary>
	/// Stops the tray icon and flushes any pending preference write.
	/// </summary>
	/// <remarks>
	/// The controller is not disposed here: it belongs to the caller that started this run, and disposing it
	/// from the exit handler would release the inhibitor twice on the fallback path.
	/// </remarks>
	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;

		expiryTimer?.Stop();
		controller.StateChanged -= OnControllerStateChanged;
		TrayPreferences.SaveIfRequired();
		trayIcon?.Dispose();
		trayIcon = null;
	}

	private void ToggleKeepAwake()
	{
		if (TryRun(() => controller.Toggle()))
		{
			preferences.KeepAwake = controller.IsActive;
			TrayPreferences.QueueSave();
		}

		RefreshMenu();
	}

	private void ApplyDuration()
	{
		if (duration is not TimeSpan window)
		{
			return;
		}

		expiryTimer = new DispatcherTimer { Interval = window };
		expiryTimer.Tick += (_, _) =>
		{
			expiryTimer.Stop();
			controller.Disable();
			Shutdown();
		};

		expiryTimer.Start();
	}

	private void Shutdown()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
			return;
		}

		// Nothing else can end a tray-only run, so fall back to ending the dispatcher loop directly rather
		// than leaving a process with no way out.
		Dispatcher.UIThread.InvokeShutdown();
	}

	private void RefreshMenu()
	{
		bool active = controller.IsActive;
		string state = DescribeState(active);

		statusItem.Header = state;

		keepAwakeItem.IsChecked = active;
		keepAwakeItem.IsEnabled = controller.IsSupported;

		displayItem.IsChecked = controller.KeepDisplayAwake;
		displayItem.IsEnabled = controller.IsSupported;

		if (trayIcon is not null)
		{
			trayIcon.Icon = TrayIconAssets.For(active);
			trayIcon.ToolTipText = state;
		}
	}

	private string DescribeState(bool active)
	{
		if (lastError is not null)
		{
			return $"NoSleep failed: {lastError}";
		}

		if (!controller.IsSupported)
		{
			return $"NoSleep cannot keep this machine awake ({controller.Mechanism})";
		}

		if (!active)
		{
			return "NoSleep is off - this machine may sleep";
		}

		string scope = controller.KeepDisplayAwake ? "system and display" : "system";
		return duration is TimeSpan window
			? string.Create(CultureInfo.InvariantCulture, $"Keeping {scope} awake for {DescribeDuration(window)}")
			: $"Keeping {scope} awake";
	}

	private static string DescribeDuration(TimeSpan window) =>
		window.TotalHours >= 1
			? string.Create(CultureInfo.InvariantCulture, $"{window.TotalHours:0.#}h")
			: string.Create(CultureInfo.InvariantCulture, $"{window.TotalMinutes:0.#}m");

	/// <summary>
	/// Runs a menu action, turning a platform refusal into a message in the menu.
	/// </summary>
	/// <remarks>
	/// A tray app has no console to fail into and no window to raise a dialog over, so a refusal has to land
	/// somewhere the user is already looking. It is also written to standard error for anyone who started
	/// NoSleep from a terminal.
	/// </remarks>
	private bool TryRun(Action action)
	{
		try
		{
			action();
			lastError = null;
			return true;
		}
		catch (Exception ex) when (ex is SleepBlockException or PlatformNotSupportedException or ObjectDisposedException)
		{
			// RefreshMenu runs right after every action, so the message is kept in a field rather than
			// written straight into the menu item, where the refresh would immediately overwrite it.
			lastError = ex.Message;
			Console.Error.WriteLine($"nosleep: {ex.Message}");
			return false;
		}
	}
}
