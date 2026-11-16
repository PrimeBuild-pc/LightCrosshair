using System.Collections.Generic;
using System.Linq;
using LightCrosshair.Core;
using LightCrosshair.Contracts;
using Xunit;
namespace LightCrosshair.Tests;
public class FinalColorsTests
{
    [Theory]
    [InlineData(false,false,0)] [InlineData(true,false,115)] [InlineData(true,true,125)] [InlineData(false,true,125)]
    public void GameEnabledColorsOverrideGlobalOtherwiseGlobalOrOriginal(bool global, bool game, int gamma)
    {
        var store=LegacyMigration.Defaults();
        store.Settings.DisplayColorsEnabled=global; store.Settings.DisplayColors=new(){Gamma=115};
        store.GameProfiles.Add(new(){ProcessName="game.exe",CrosshairId=store.Settings.DefaultCrosshairId,DisplayColorsEnabled=game,DisplayColors=new(){Gamma=125}});
        Assert.Equal(gamma,GameRules.Evaluate(store,"game.exe").DisplayColors?.Gamma ?? 0);
        Assert.Equal(global?115:0,GameRules.Evaluate(store,"explorer.exe").DisplayColors?.Gamma ?? 0);
    }
    [Fact]
    public void EveryNamedRampIsCapturedBeforeWritingAndRestoredFromJournal()
    {
        var devices=new Devices(); var manager=new Win32GammaColorManager(new(GpuVendor.Amd,"fake"),devices);
        Assert.True(manager.TryApply(new(110,100,100,50),out _));
        Assert.Equal(new[]{"read A","read B","write A","write B"},devices.Calls);
        var original=manager.CaptureRecovery(); Assert.Equal(2,original.Displays.Count); Assert.Null(original.Global);
        var recovered=new Win32GammaColorManager(new(GpuVendor.Amd,"fake"),devices); recovered.LoadRecovery(original);
        Assert.True(recovered.TryRestore(out _)); Assert.Equal((ushort)123,devices.Current["A"].Red[128]);
        Assert.Equal((ushort)456,devices.Current["B"].Red[128]);
    }
    [Fact]
    public void IncompleteBaselineNeverWritesAnyDisplay()
    {
        var devices=new Devices(){FailRead=true}; var manager=new Win32GammaColorManager(new(GpuVendor.Amd,"fake"),devices);
        Assert.False(manager.TryApply(new(110,100,100,50),out _)); Assert.DoesNotContain(devices.Calls,c=>c.StartsWith("write"));
        Assert.Empty(manager.CaptureRecovery().Displays);
    }
    private sealed class Devices:Win32GammaColorManager.IDevices
    {
        public bool FailRead;
        public List<string> Calls=new();
        public IEnumerable<string> Names=>new[]{"A","B"};
        public Dictionary<string,Win32GammaColorManager.GammaRamp> Current=new(){["A"]=Ramp(123),["B"]=Ramp(456)};
        static Win32GammaColorManager.GammaRamp Ramp(ushort v)=>new(){Red=Enumerable.Repeat(v,256).ToArray(),Green=new ushort[256],Blue=new ushort[256]};
        public bool Read(string name,out Win32GammaColorManager.GammaRamp ramp){Calls.Add("read "+name);ramp=Current[name];return !(FailRead&&name=="B");}
        public bool Write(string name,Win32GammaColorManager.GammaRamp ramp){Calls.Add("write "+name);Current[name]=ramp;return true;}
    }
}
