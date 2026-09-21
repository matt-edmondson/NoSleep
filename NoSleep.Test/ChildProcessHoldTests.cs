// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Test;

using System;
using System.Diagnostics;
using ktsu.NoSleep.Platforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="ChildProcessHold"/>, the mechanism behind the Linux inhibitor.
/// </summary>
/// <remarks>
/// These run against <c>/bin/cat</c> and <c>/bin/false</c> rather than <c>systemd-inhibit</c>: the shape being
/// tested is "a helper process that stays alive until told otherwise", and a build agent has no logind bus to
/// take a real lock on. They are skipped off Unix, where those paths do not exist.
/// </remarks>
[TestClass]
public class ChildProcessHoldTests
{
	private static bool IsUnix => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

	[TestMethod]
	public void Start_KeepsTheHelperRunningUntilDisposed()
	{
		if (!IsUnix)
		{
			Assert.Inconclusive("The helper paths used by this test only exist on Unix.");
			return;
		}

		int helperId;

		using (ChildProcessHold hold = ChildProcessHold.Start("/bin/cat", []))
		{
			Assert.AreEqual("/bin/cat", hold.Command);
			helperId = hold.ProcessId;
			Assert.IsTrue(IsRunning(helperId), "The helper should still be running while the hold is held");
		}

		Assert.IsFalse(IsRunning(helperId), "Disposing the hold should end the helper");
	}

	[TestMethod]
	public void Start_WhenTheHelperExitsImmediately_Throws()
	{
		if (!IsUnix)
		{
			Assert.Inconclusive("The helper paths used by this test only exist on Unix.");
			return;
		}

		SleepBlockException exception = Assert.ThrowsExactly<SleepBlockException>(() => ChildProcessHold.Start("/bin/false", []));
		StringAssert.Contains(exception.Message, "exited immediately", StringComparison.Ordinal);
	}

	[TestMethod]
	public void Start_WithAMissingHelper_Throws()
	{
		SleepBlockException exception = Assert.ThrowsExactly<SleepBlockException>(
			() => ChildProcessHold.Start("/definitely/not/a/real/inhibitor", []));

		StringAssert.Contains(exception.Message, "Could not start", StringComparison.Ordinal);
	}

	[TestMethod]
	public void Dispose_IsIdempotent()
	{
		if (!IsUnix)
		{
			Assert.Inconclusive("The helper paths used by this test only exist on Unix.");
			return;
		}

		ChildProcessHold hold = ChildProcessHold.Start("/bin/cat", []);

		hold.Dispose();
		hold.Dispose();
	}

	[TestMethod]
	public void Start_WithNullArguments_Throws() =>
		Assert.ThrowsExactly<ArgumentNullException>(() => ChildProcessHold.Start("/bin/cat", null!));

	private static bool IsRunning(int processId)
	{
		try
		{
			using Process process = Process.GetProcessById(processId);
			return !process.HasExited;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}
}
