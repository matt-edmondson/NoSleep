# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Restore, build, and test (standard workflow)
dotnet restore
dotnet build
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Build specific configuration
dotnet build -c Release

# Run the tool from the build output
dotnet run --project NoSleep.Tool -- --status

# Pack the tool and inspect the payload
dotnet pack NoSleep.Tool/NoSleep.Tool.csproj -c Release -o ./artifacts
```

If `dotnet test` reports "Zero tests ran", run the test host directly - some SDK builds do not drive
Microsoft Testing Platform correctly through `dotnet test`:

```bash
./NoSleep.Test/bin/Debug/net10.0/ktsu.NoSleep.Test
```

## Project Structure

This is a .NET tool (`ktsu.NoSleep.Tool`, installed as the `nosleep` command) with a reusable library
(`ktsu.NoSleep`) underneath it. The solution uses:

- **ktsu.Sdk** - Custom SDK providing shared build configuration
- **ktsu.Sdk.Tool** - Packs `NoSleep.Tool` as a `DotnetTool` package; derives the command name `nosleep`
  from the solution name
- **MSTest.Sdk** - Test project SDK with Microsoft Testing Platform
- Multi-targeting: `net10.0;net9.0` for the library, `net10.0` for the tool and tests

### Key Files

- `NoSleep/Contracts/ISleepBlocker.cs` - The platform layer's contract: acquire an inhibitor, release it,
  and say whether the machine can do either
- `NoSleep/KeepAwakeController.cs` - The on/off state machine over an `ISleepBlocker`, with a
  `StateChanged` event. Both the tray and the console mode drive this
- `NoSleep/SleepBlockRequest.cs` - What to keep awake (system, or system and display) and why
- `NoSleep/SleepBlockerFactory.cs` - Picks the blocker for the running OS
- `NoSleep/Platforms/WindowsSleepBlocker.cs` - `SetThreadExecutionState` on a dedicated parked thread
- `NoSleep/Platforms/MacOsSleepBlocker.cs` - IOKit power assertions through CoreFoundation strings
- `NoSleep/Platforms/LinuxSleepBlocker.cs` - `systemd-inhibit` and `gnome-session-inhibit` as child processes
- `NoSleep/Platforms/ChildProcessHold.cs` - The helper-process lifetime used by the Linux blocker
- `NoSleep/Platforms/ExecutableLocator.cs` - `PATH` lookup, so support can be reported without a process launch
- `NoSleep.Tool/Program.cs` - The whole tool: the menu, the three flags NoSleep adds, the `--status` rows,
  and the wiring between them and `KeepAwakeController`, described to `ktsu.TrayApp`'s `TrayAppBuilder`
- `scripts/generate-icons.py` - Draws `NoSleep.Tool/Assets/*.png` and the repository's `icon.png`

### Dependencies

- **ktsu.TrayApp** - The tray host and everything around it: the Avalonia tray icon, the tray-versus-console
  decision and its fallback, Ctrl+C and `SIGTERM`, the `--for` timer, the standard flags, the `--status`
  report, debounced preference persistence, and the MSBuild props that trim the tool payload. All of it was
  extracted from this repository; Avalonia arrives through it and is not referenced here
- **ktsu.Essentials + the ConfigHome / Json / Native providers** - The persistence stack behind the tray's
  remembered state. `ktsu.TrayApp` takes `IPersistenceProvider<string>` and nothing else, so where the
  settings live (`nosleep/tray.json` under the config home) is this tool's choice, assembled in `Program`
- **Polyfill** - Required by KTSU0001 for non-test projects; also supplies `Ensure.NotNull`

## Architecture

The split is deliberate: `ISleepBlocker` implementations know how to take and drop one platform inhibitor
and nothing else, `KeepAwakeController` owns whether one *should* be held, and the tool only describes a
menu over the controller to `ktsu.TrayApp`. Nothing in `NoSleep` references Avalonia, so the library is
usable from a service or a test without a windowing stack.

```csharp
using KeepAwakeController controller = new();
controller.SetRequest(new SleepBlockRequest(KeepDisplayAwake: true, "long build"));
controller.Enable();
```

Three platform details are load-bearing and easy to undo by accident:

- **Windows**: the execution state belongs to the thread that set it and is dropped when that thread exits,
  so `WindowsSleepBlocker` parks a dedicated thread for the duration. Setting the flags on a thread pool
  thread works only for as long as the pool happens to keep that thread.
- **Linux**: inhibitor locks belong to a process, so `ChildProcessHold` keeps the helper's standard input
  pipe open. When NoSleep dies the pipe closes, the helper reaches end of input, and the lock goes with it -
  without that, a `kill -9` would orphan an inhibitor with nothing left to release it.
- **Linux tray**: Avalonia's teardown throws after a perfectly ordinary shutdown, and a tray that never came
  up has to fall back to the console instead of killing the process. Both now belong to `ktsu.TrayApp`,
  whose CLAUDE.md lists them under "Load-bearing behaviour" - the tray decision (`--no-tray` wins, then
  `--tray`, then `DesktopSession.IsAvailable`) and the fallback are its rules now, not this repository's.

One thing here is load-bearing and easy to undo by accident. `Program` keeps *whether the user wants to be
kept awake* in a local `bool` rather than calling `Enable()` from the toggle's setter, because a run applies
the remembered preferences first, then the command line, and only then `OnStart`. A setter that enabled
directly would have `OnStart` override an explicit `--off`, or a remembered "off", a moment later.

## Testing

MSTest with Microsoft Testing Platform. `FakeSleepBlocker` stands in for the platform layer so
`KeepAwakeControllerTests` can assert on acquire/release sequences anywhere. `PlatformIntegrationTests` checks
the things that can only be asserted against the machine the tests are running on, including that the tray
PNGs are embedded under the resource names `TrayIconSet` resolves by string - the names are passed to
`ktsu.TrayApp` as strings, so a renamed asset is otherwise a run-time failure on a machine with a display.

`ChildProcessHoldTests` uses `/bin/cat` and `/bin/sh -c "exit 1"` rather than `systemd-inhibit`, because a
build agent has no logind bus to take a real lock on; those cases report inconclusive off Unix. The exiting
helper is spelled as a shell invocation on purpose: POSIX puts `sh` at `/bin/sh` on every Unix, while `false`
is `/bin/false` on Linux and `/usr/bin/false` on macOS, so hardcoding the Linux path failed on macOS for the
wrong reason - the helper could not be started at all, rather than starting and exiting.

Verifying the Linux tray end to end needs a display and a status-notifier host on the session bus.
`ktsu.TrayApp` ships both halves of that rig in its `scripts/` directory:

```bash
pip install dbus-next
eval "$(dbus-launch --sh-syntax)"
python3 scripts/status-notifier-watcher.py > watcher.log 2>&1 &
xvfb-run -a env DBUS_SESSION_BUS_ADDRESS="$DBUS_SESSION_BUS_ADDRESS" \
  dotnet run --project NoSleep.Tool -- --tray --for 5s
python3 scripts/click-tray-menu.py "$(grep -o 'org.kde.StatusNotifierItem-[0-9-]*' watcher.log | head -1)" "Keep awake"
```

Success is the watcher logging a registered item, the run lasting the full window and exiting 0, and the
click flipping the entry's checked state with the status line re-reading itself.

## Packaging

The tool payload is trimmed by `build/ktsu.TrayApp.props` and `.targets`, which arrive with the
`ktsu.TrayApp` package and are imported into any project referencing it. A `DotnetTool` package is
RID-agnostic, so without them SkiaSharp's native assets ship for every RID it supports, plus their debug
symbols: 190 MB against the current 50 MB, measured here with
`dotnet pack NoSleep.Tool/NoSleep.Tool.csproj -c Release -p:TrayAppTrimToolRuntimeAssets=false`.
`TrayAppToolRuntimeIdentifiers` lists the runtimes that keep their native assets; anything else falls back
to the console mode, which needs none. Adding a RID back is a one-line property in this project.

## CI/CD

Uses the KtsuBuild tool (`ktsu.KtsuBuild.Tool`) for the CI pipeline. Version increments are controlled by
commit message tags: `[major]`, `[minor]`, `[patch]`, `[pre]`.

## Code Quality

Do not add global suppressions for warnings. Use explicit suppression attributes with justifications when
needed, with preprocessor defines only as fallback. Make the smallest, most targeted suppressions possible.

Auto-generated files (`VERSION.md`, `CHANGELOG.md`, `LICENSE.md`) should never be edited manually.
