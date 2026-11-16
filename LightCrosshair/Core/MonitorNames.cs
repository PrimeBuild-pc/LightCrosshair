using System.Runtime.InteropServices;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal interface IMonitorNameProvider
{
    IReadOnlyDictionary<string, string> TargetNames();
    IReadOnlyDictionary<string, string> AdapterNames();
}

internal static class MonitorNames
{
    internal static List<MonitorInfo> Populate(IEnumerable<MonitorInfo> monitors, IMonitorNameProvider? provider = null)
    {
        provider ??= new DisplayConfigMonitorNames();
        var targets = provider.TargetNames(); var adapters = provider.AdapterNames();
        var result = monitors.ToList();
        foreach (var monitor in result)
            monitor.FriendlyName = targets.TryGetValue(monitor.DeviceName, out string? model) && !string.IsNullOrWhiteSpace(model) ? model.Trim()
                : adapters.TryGetValue(monitor.DeviceName, out string? adapter) && !string.IsNullOrWhiteSpace(adapter) ? adapter.Trim() : monitor.DeviceName;
        return result;
    }
}

internal sealed class DisplayConfigMonitorNames : IMonitorNameProvider
{
    public IReadOnlyDictionary<string, string> TargetNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // A hotplug between size/query can change the array sizes. Retry that race.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(2, out uint pathCount, out uint modeCount) != 0) break; // QDC_ONLY_ACTIVE_PATHS
            var paths = new PathInfo[pathCount]; var modes = new ModeInfo[modeCount];
            int error = QueryDisplayConfig(2, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (error == 122) continue;
            if (error != 0) break;
            for (int i = 0; i < pathCount; i++)
            {
                var source = new SourceName { Header = Header.For<SourceName>(1, paths[i].Source.Adapter, paths[i].Source.Id) };
                var target = new TargetName { Header = Header.For<TargetName>(2, paths[i].Target.Adapter, paths[i].Target.Id) };
                if (GetSourceName(ref source) == 0 && GetTargetName(ref target) == 0 && !string.IsNullOrWhiteSpace(target.FriendlyName))
                    names.TryAdd(source.GdiName, target.FriendlyName);
            }
            break;
        }
        return names;
    }
    public IReadOnlyDictionary<string, string> AdapterNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (uint i = 0; ; i++)
        {
            var device = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, i, ref device, 0)) break;
            names[device.Name] = device.Description;
        }
        return names;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] private struct SourceInfo { public Luid Adapter; public uint Id, ModeIndex, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct TargetInfo
    { public Luid Adapter; public uint Id, ModeIndex, Technology, Rotation, Scaling, RefreshNumerator, RefreshDenominator, ScanOrder, Available, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct PathInfo { public SourceInfo Source; public TargetInfo Target; public uint Flags; }
    [StructLayout(LayoutKind.Explicit, Size = 64)] private struct ModeInfo { }
    [StructLayout(LayoutKind.Sequential)] private struct Header
    {
        public uint Type, Size; public Luid Adapter; public uint Id;
        internal static Header For<T>(uint type, Luid adapter, uint id) => new() { Type = type, Size = (uint)Marshal.SizeOf<T>(), Adapter = adapter, Id = id };
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SourceName
    { public Header Header; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string GdiName; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TargetName
    {
        public Header Header; public uint Flags, Technology; public ushort EdidManufacturer, EdidProduct; public uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DisplayDevice
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] PathInfo[] pathInfo, ref uint modes, [Out] ModeInfo[] modeInfo, IntPtr topology);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetSourceName(ref SourceName name);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetTargetName(ref TargetName name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice info, uint flags);
}
