// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.NoSleep.Test;

using System;
using ktsu.NoSleep.Tool.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="CommandLineParser"/>.
/// </summary>
[TestClass]
public class CommandLineParserTests
{
	[TestMethod]
	public void TryParse_WithNoArguments_ReturnsDefaults()
	{
		Assert.IsTrue(CommandLineParser.TryParse([], out CommandLineOptions options, out string error));

		Assert.AreEqual(string.Empty, error);
		Assert.IsFalse(options.TrayRequested);
		Assert.IsFalse(options.TraySuppressed);
		Assert.IsFalse(options.KeepDisplayAwake);
		Assert.IsFalse(options.StartDisabled);
		Assert.IsNull(options.Duration);
		Assert.AreEqual(SleepBlockRequest.DefaultReason, options.Reason);
	}

	[TestMethod]
	[DataRow("-t")]
	[DataRow("--tray")]
	public void TryParse_WithTray_SetsTrayRequested(string argument)
	{
		Assert.IsTrue(CommandLineParser.TryParse([argument], out CommandLineOptions options, out _));
		Assert.IsTrue(options.TrayRequested);
	}

	[TestMethod]
	[DataRow("-d")]
	[DataRow("--display")]
	public void TryParse_WithDisplay_SetsKeepDisplayAwake(string argument)
	{
		Assert.IsTrue(CommandLineParser.TryParse([argument], out CommandLineOptions options, out _));
		Assert.IsTrue(options.KeepDisplayAwake);
		Assert.IsTrue(options.ToRequest().KeepDisplayAwake);
	}

	[TestMethod]
	public void TryParse_WithNoTray_SetsTraySuppressed()
	{
		Assert.IsTrue(CommandLineParser.TryParse(["--no-tray"], out CommandLineOptions options, out _));
		Assert.IsTrue(options.TraySuppressed);
	}

	[TestMethod]
	public void TryParse_WithDuration_ParsesIt()
	{
		Assert.IsTrue(CommandLineParser.TryParse(["--for", "90m"], out CommandLineOptions options, out _));
		Assert.AreEqual(TimeSpan.FromMinutes(90), options.Duration);
	}

	[TestMethod]
	public void TryParse_WithReason_CarriesItIntoTheRequest()
	{
		Assert.IsTrue(CommandLineParser.TryParse(["--reason", "long build"], out CommandLineOptions options, out _));
		Assert.AreEqual("long build", options.ToRequest().Reason);
	}

	[TestMethod]
	public void TryParse_WithCombinedSwitches_SetsAllOfThem()
	{
		Assert.IsTrue(CommandLineParser.TryParse(["--no-tray", "-d", "-f", "2h"], out CommandLineOptions options, out _));

		Assert.IsTrue(options.TraySuppressed);
		Assert.IsTrue(options.KeepDisplayAwake);
		Assert.AreEqual(TimeSpan.FromHours(2), options.Duration);
	}

	[TestMethod]
	public void TryParse_WithTrayAndNoTray_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--tray", "--no-tray"], out _, out string error));
		StringAssert.Contains(error, "contradict", StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithAnUnknownOption_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--sleep-more"], out _, out string error));
		StringAssert.Contains(error, "--sleep-more", StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithAStrayArgument_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["please"], out _, out string error));
		StringAssert.Contains(error, "please", StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithADurationThatIsNotADuration_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for", "soon"], out _, out string error));
		StringAssert.Contains(error, "soon", StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithAValuelessOptionAtTheEnd_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for"], out _, out string error));
		StringAssert.Contains(error, "needs a value", StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithNullArguments_Throws() =>
		Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineParser.TryParse(null!, out _, out _));
}
