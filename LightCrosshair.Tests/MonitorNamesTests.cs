using System.Collections.Generic;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class MonitorNamesTests
{
    [Fact]
    public void TargetModelWinsAndMissingOrEmptyEdidFallsBackToAdapter()
    {
        var names = MonitorNames.Populate(new[] {
            new MonitorInfo { DeviceName = "DISPLAY1", Width = 2560, Height = 1440, IsPrimary = true },
            new MonitorInfo { DeviceName = "DISPLAY2" }, new MonitorInfo { DeviceName = "DISPLAY3" }, new MonitorInfo { DeviceName = "DISPLAY4" }
        }, new FakeNames());
        Assert.Equal("DELL S2721DGF", names[0].FriendlyName);
        Assert.Equal("AMD Radeon RX 6950 XT", names[1].FriendlyName);
        Assert.Equal("Virtual display adapter", names[2].FriendlyName);
        Assert.Equal("DISPLAY4", names[3].FriendlyName);
        Assert.True(names[0].IsPrimary); Assert.Equal(2560, names[0].Width); Assert.Equal(1440, names[0].Height);
        Assert.Equal("DISPLAY1", names[0].DeviceName);
    }
    private sealed class FakeNames : IMonitorNameProvider
    {
        public IReadOnlyDictionary<string, string> TargetNames() => new Dictionary<string, string> { ["DISPLAY1"] = " DELL S2721DGF ", ["DISPLAY2"] = "  " };
        public IReadOnlyDictionary<string, string> AdapterNames() => new Dictionary<string, string> { ["DISPLAY1"] = "Generic adapter", ["DISPLAY2"] = "AMD Radeon RX 6950 XT", ["DISPLAY3"] = "Virtual display adapter" };
    }
}
