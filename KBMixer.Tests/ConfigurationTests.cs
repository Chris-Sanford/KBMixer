using KBMixer;
using Xunit;

namespace KBMixer.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void EmptyProfile_IsUnconfigured()
    {
        var config = new Config
        {
            ConfigId = Guid.NewGuid(),
            DeviceId = "device-1",
            AppFileName = "",
            AppFriendlyName = "",
            Hotkeys = [],
            ControlSingleSession = false,
            ProcessIndex = 0
        };

        Assert.False(config.HasTarget);
        Assert.Equal("New profile", config.GetAutoDisplayName("Speakers"));
    }

    [Fact]
    public void DeviceReconciliation_PrefersFriendlyNameAndDescription()
    {
        var config = new Config
        {
            ConfigId = Guid.NewGuid(),
            DeviceId = "old-device-id",
            DeviceFriendlyName = "Speakers",
            DeviceDescription = "USB DAC",
            AppFileName = "chrome.exe",
            AppFriendlyName = "Google Chrome",
            Hotkeys = [16],
            ControlSingleSession = false,
            ProcessIndex = 0
        };

        var devices = new[]
        {
            new DeviceIdentity("wrong-device", "Speakers", "Onboard Audio"),
            new DeviceIdentity("matching-device", "Speakers", "USB DAC")
        };

        var result = config.TryReconcileDevice(devices);

        Assert.Equal(DeviceReconcileResult.Rematched, result);
        Assert.Equal("matching-device", config.DeviceId);
    }

    [Fact]
    public void DeviceReconciliation_BackfillsIdentityForExistingDevice()
    {
        var config = new Config
        {
            ConfigId = Guid.NewGuid(),
            DeviceId = "live-device",
            AppFileName = "chrome.exe",
            AppFriendlyName = "Google Chrome",
            Hotkeys = [],
            ControlSingleSession = false,
            ProcessIndex = 0
        };

        var result = config.TryReconcileDevice(
        [
            new DeviceIdentity("live-device", "Speakers", "USB DAC")
        ]);

        Assert.Equal(DeviceReconcileResult.Backfilled, result);
        Assert.Equal("Speakers", config.DeviceFriendlyName);
        Assert.Equal("USB DAC", config.DeviceDescription);
    }
}
