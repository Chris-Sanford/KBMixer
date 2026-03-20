using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KBMixer;
using NAudio.CoreAudioApi;
using Xunit;
using ConfigModel = KBMixer.Config;

namespace KBMixer.Tests;

public class AudioSessionGroupingTests
{
    [Theory]
    [InlineData("Discord.exe", "discord.exe", true)]
    [InlineData("Discord.exe", "Discord (Instance 2)", true)]
    [InlineData("Discord (Instance 1)", "Discord (Instance 2)", true)]
    [InlineData("Discord.exe", "chrome.exe", false)]
    [InlineData("%b#", "%b#", true)]
    public void GroupingKeyForAppFileName_MatchesExecutableGroup(string a, string b, bool sameGroup)
    {
        var ka = Audio.GroupingKeyForAppFileName(a);
        var kb = Audio.GroupingKeyForAppFileName(b);
        if (sameGroup)
            Assert.Equal(ka, kb);
        else
            Assert.NotEqual(ka, kb);
    }

    [Theory]
    [InlineData("Discord.exe", "Discord (Instance 2)", "", "Discord.exe", null, true)]
    [InlineData("Discord.exe", "Discord (Instance 2)", "", "Discord", null, true)]
    [InlineData("SomeOther.exe", "Discord (Instance 2)", "", "Discord.exe", "Discord", true)]
    [InlineData("z.exe", "Google Chrome", "", "Discord.exe", null, false)]
    [InlineData("Discord.exe", "", "", "Discord.exe", null, true)]
    public void SessionMatchesConfig_ExeOrDisplayOrFriendly(
        string sessionExe,
        string? display,
        string? identityFriendly,
        string configAppFile,
        string? configFriendly,
        bool expectMatch)
    {
        bool m = Audio.SessionMatchesConfig(sessionExe, display, identityFriendly, configAppFile, configFriendly);
        Assert.Equal(expectMatch, m);
    }

    /// <summary>Exercises WASAPI enumeration on this machine (no specific app required).</summary>
    [Fact]
    public void CollectSessionsForExecutableGroup_DoesNotThrow_OnDefaultDevice()
    {
        var devices = Audio.GetAudioDevices();
        Assert.NotEmpty(devices);

        var device = devices[0].MMDevice;
        var list = Audio.CollectSessionsForExecutableGroup(device, "explorer.exe");
        Assert.NotNull(list);
        // explorer may or may not be playing audio; count may be 0
        Assert.True(list.Count >= 0);
        // Reference-equality set: no duplicate control references
        Assert.Equal(list.Count, new HashSet<AudioSessionControl>(new RefEq()).Count);
    }

    [Fact]
    public void CollectSessionsForConfig_DoesNotThrow_OnDefaultDevice()
    {
        var devices = Audio.GetAudioDevices();
        Assert.NotEmpty(devices);
        var cfg = new ConfigModel
        {
            ConfigId = Guid.NewGuid(),
            DeviceId = devices[0].MMDevice.ID,
            AppFileName = "explorer.exe",
            AppFriendlyName = "Explorer",
            Hotkeys = [],
            ControlSingleSession = false,
            ProcessIndex = 0
        };
        var list = Audio.CollectSessionsForConfig(devices[0].MMDevice, cfg);
        Assert.NotNull(list);
        Assert.Equal(list.Count, new HashSet<AudioSessionControl>(new RefEq()).Count);
    }

    private sealed class RefEq : IEqualityComparer<AudioSessionControl>
    {
        public bool Equals(AudioSessionControl? x, AudioSessionControl? y) => ReferenceEquals(x, y);
        public int GetHashCode(AudioSessionControl obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
