using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LightCrosshair
{
    internal static class DisplayColorEnvironment
    {
        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const int ERROR_SUCCESS = 0;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTL
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECTL
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public DISPLAYCONFIG_2DREGION activeSize;
            public DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
        {
            public POINTL PathSourceSize;
            public RECTL DesktopImageRegion;
            public RECTL DesktopImageClip;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;

            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;

            [FieldOffset(0)]
            public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
            [MarshalAs(UnmanagedType.Bool)]
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
            public uint colorEncoding;
            public uint bitsPerColorChannel;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        public static bool IsHdrActive(out string details)
        {
            details = "HDR state unknown.";
            try
            {
                int result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
                if (result != ERROR_SUCCESS || pathCount == 0)
                {
                    details = $"HDR state unavailable (GetDisplayConfigBufferSizes={result}, paths={pathCount}).";
                    return false;
                }

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (result != ERROR_SUCCESS)
                {
                    details = $"HDR state unavailable (QueryDisplayConfig={result}).";
                    return false;
                }

                bool anySupported = false;
                int enabledCount = 0;
                int inspected = 0;

                for (int i = 0; i < pathCount; i++)
                {
                    var request = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                    {
                        header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                        {
                            type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                            size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                            adapterId = paths[i].targetInfo.adapterId,
                            id = paths[i].targetInfo.id,
                        }
                    };

                    result = DisplayConfigGetDeviceInfo(ref request);
                    if (result != ERROR_SUCCESS)
                    {
                        continue;
                    }

                    inspected++;
                    bool supported = (request.value & 0x1u) != 0;
                    bool enabled = (request.value & 0x2u) != 0;

                    if (supported)
                    {
                        anySupported = true;
                    }

                    if (supported && enabled)
                    {
                        enabledCount++;
                    }
                }

                if (enabledCount > 0)
                {
                    details = $"HDR active on {enabledCount} output(s) (inspected={inspected}, supported={anySupported}).";
                    return true;
                }

                details = anySupported
                    ? $"HDR capable outputs found but currently disabled (inspected={inspected})."
                    : $"No HDR-capable active output reported (inspected={inspected}).";
                return false;
            }
            catch (Exception ex)
            {
                details = $"HDR check failed: {ex.Message}";
                return false;
            }
        }
    }

    internal sealed class AmdAdlColorManager
    {
        private const int ADL_OK = 0;
        private const int ADL_ERR_NOT_SUPPORTED = -3;
        private const int ADL_DISPLAY_COLOR_BRIGHTNESS = 1 << 0;
        private const int ADL_DISPLAY_COLOR_CONTRAST = 1 << 1;
        private const int ADL_DISPLAY_COLOR_SATURATION = 1 << 2;

        private static readonly int[] BrightnessTypeCandidates = { ADL_DISPLAY_COLOR_BRIGHTNESS };
        private static readonly int[] ContrastTypeCandidates = { ADL_DISPLAY_COLOR_CONTRAST };
        private static readonly int[] SaturationTypeCandidates = { ADL_DISPLAY_COLOR_SATURATION };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr AdlMallocCallback(int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Adl2MainControlCreateDelegate(AdlMallocCallback callback, int enumConnectedAdapters, out IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Adl2MainControlDestroyDelegate(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Adl2AdapterCountGetDelegate(IntPtr context, out int numberOfAdapters);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Adl2AdapterActiveGetDelegate(IntPtr context, int adapterIndex, out int status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Adl2DisplayColorGetDelegate(
            IntPtr context,
            int adapterIndex,
            int displayIndex,
            int colorType,
            out int current,
            out int defaultValue,
            out int min,
            out int max,
            out int step);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Adl2DisplayColorSetDelegate(IntPtr context, int adapterIndex, int displayIndex, int colorType, int current);

        private readonly object _sync = new();
        private readonly IntPtr _libraryHandle;
        private readonly string _libraryName;
        private readonly Adl2MainControlCreateDelegate? _mainControlCreate;
        private readonly Adl2MainControlDestroyDelegate? _mainControlDestroy;
        private readonly Adl2AdapterCountGetDelegate? _adapterCountGet;
        private readonly Adl2AdapterActiveGetDelegate? _adapterActiveGet;
        private readonly Adl2DisplayColorGetDelegate? _displayColorGet;
        private readonly Adl2DisplayColorSetDelegate? _displayColorSet;
        private readonly Dictionary<ChannelKey, int> _originalValues = new();
        private readonly Dictionary<DisplayChannel, int> _resolvedColorTypes = new();
        private readonly HashSet<DisplayChannel> _loggedResolvedChannels = new();
        private readonly Dictionary<DisplayChannel, HashSet<int>> _writeRejectedTypes = new();
        private readonly HashSet<ChannelKey> _loggedUnsupportedWrites = new();

        private readonly AdlMallocCallback _mallocCallback = size => Marshal.AllocHGlobal(size);

        private readonly struct ChannelKey : IEquatable<ChannelKey>
        {
            public readonly int AdapterIndex;
            public readonly int DisplayIndex;
            public readonly int ColorType;

            public ChannelKey(int adapterIndex, int displayIndex, int colorType)
            {
                AdapterIndex = adapterIndex;
                DisplayIndex = displayIndex;
                ColorType = colorType;
            }

            public bool Equals(ChannelKey other)
                => AdapterIndex == other.AdapterIndex && DisplayIndex == other.DisplayIndex && ColorType == other.ColorType;

            public override bool Equals(object? obj) => obj is ChannelKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(AdapterIndex, DisplayIndex, ColorType);
        }

        private readonly struct DisplayChannel : IEquatable<DisplayChannel>
        {
            public readonly int AdapterIndex;
            public readonly int DisplayIndex;
            public readonly string Channel;

            public DisplayChannel(int adapterIndex, int displayIndex, string channel)
            {
                AdapterIndex = adapterIndex;
                DisplayIndex = displayIndex;
                Channel = channel;
            }

            public bool Equals(DisplayChannel other)
                => AdapterIndex == other.AdapterIndex && DisplayIndex == other.DisplayIndex && string.Equals(Channel, other.Channel, StringComparison.Ordinal);

            public override bool Equals(object? obj) => obj is DisplayChannel other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(AdapterIndex, DisplayIndex, Channel);
        }

        private sealed class ColorChannelSnapshot
        {
            public int ColorType { get; }
            public int Current { get; }
            public int DefaultValue { get; }
            public int Min { get; }
            public int Max { get; }
            public int Step { get; }

            public ColorChannelSnapshot(int colorType, int current, int defaultValue, int min, int max, int step)
            {
                ColorType = colorType;
                Current = current;
                DefaultValue = defaultValue;
                Min = min;
                Max = max;
                Step = step;
            }
        }

        public AmdAdlColorManager()
        {
            if (!TryLoadLibrary("atiadlxx.dll", out _libraryHandle))
            {
                _ = TryLoadLibrary("atiadlxy.dll", out _libraryHandle);
                _libraryName = _libraryHandle != IntPtr.Zero ? "atiadlxy.dll" : "none";
            }
            else
            {
                _libraryName = "atiadlxx.dll";
            }

            if (_libraryHandle == IntPtr.Zero)
            {
                IsAvailable = false;
                AvailabilityMessage = "ADL runtime not present.";
                return;
            }

            _mainControlCreate = GetExport<Adl2MainControlCreateDelegate>(_libraryHandle, "ADL2_Main_Control_Create");
            _mainControlDestroy = GetExport<Adl2MainControlDestroyDelegate>(_libraryHandle, "ADL2_Main_Control_Destroy");
            _adapterCountGet = GetExport<Adl2AdapterCountGetDelegate>(_libraryHandle, "ADL2_Adapter_NumberOfAdapters_Get");
            _adapterActiveGet = GetExport<Adl2AdapterActiveGetDelegate>(_libraryHandle, "ADL2_Adapter_Active_Get");
            _displayColorGet = GetExport<Adl2DisplayColorGetDelegate>(_libraryHandle, "ADL2_Display_Color_Get");
            _displayColorSet = GetExport<Adl2DisplayColorSetDelegate>(_libraryHandle, "ADL2_Display_Color_Set");

            IsAvailable = _mainControlCreate != null
                && _mainControlDestroy != null
                && _adapterCountGet != null
                && _adapterActiveGet != null
                && _displayColorGet != null
                && _displayColorSet != null;

            AvailabilityMessage = IsAvailable
                ? $"ADL color API available via {_libraryName}."
                : $"ADL runtime found ({_libraryName}) but required color exports are missing.";
        }

        public bool IsAvailable { get; }

        public string AvailabilityMessage { get; }

        public bool HasCapturedOriginalState => _originalValues.Count > 0;

        public bool TryCaptureOriginalState(out string? error)
        {
            lock (_sync)
            {
                if (!IsAvailable)
                {
                    error = AvailabilityMessage;
                    return false;
                }

                if (DisplayColorEnvironment.IsHdrActive(out string hdrDetails))
                {
                    error = $"ADL baseline capture aborted: HDR is active. {hdrDetails}";
                    return false;
                }

                if (_originalValues.Count > 0)
                {
                    error = null;
                    return true;
                }

                if (!TryCreateContext(out IntPtr context, out error))
                {
                    return false;
                }

                int capturedChannels = 0;
                try
                {
                    if (_adapterCountGet!(context, out int adapterCount) != ADL_OK || adapterCount <= 0)
                    {
                        error = "ADL did not report active adapters while capturing baseline.";
                        return false;
                    }

                    for (int adapter = 0; adapter < adapterCount; adapter++)
                    {
                        if (!IsAdapterActive(context, adapter))
                        {
                            continue;
                        }

                        for (int display = 0; display < 16; display++)
                        {
                            int before = _originalValues.Count;
                            _ = TryCaptureChannel(context, adapter, display, "brightness", BrightnessTypeCandidates);
                            _ = TryCaptureChannel(context, adapter, display, "contrast", ContrastTypeCandidates);
                            _ = TryCaptureChannel(context, adapter, display, "saturation", SaturationTypeCandidates);

                            capturedChannels += _originalValues.Count - before;
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = $"ADL baseline capture failed: {ex.Message}";
                    return false;
                }
                finally
                {
                    _ = _mainControlDestroy!(context);
                }

                if (capturedChannels == 0)
                {
                    error = "ADL did not expose readable display color controls for baseline capture.";
                    return false;
                }

                Program.LogDebug($"ADL baseline captured for {capturedChannels} channel entries.", nameof(AmdAdlColorManager));
                error = null;
                return true;
            }
        }

        public bool TryApply(ColorAdjustment adjustment, out string? error)
        {
            lock (_sync)
            {
                if (!IsAvailable)
                {
                    error = AvailabilityMessage;
                    return false;
                }

                if (DisplayColorEnvironment.IsHdrActive(out string hdrDetails))
                {
                    error = $"ADL apply aborted: HDR is active. {hdrDetails}";
                    return false;
                }

                if (_originalValues.Count == 0 && !TryCaptureOriginalState(out error))
                {
                    return false;
                }

                if (!TryCreateContext(out IntPtr context, out error))
                {
                    return false;
                }

                var writableDisplays = new HashSet<(int AdapterIndex, int DisplayIndex)>();
                foreach (var key in _originalValues.Keys)
                {
                    writableDisplays.Add((key.AdapterIndex, key.DisplayIndex));
                }

                int updatedDisplays = 0;
                try
                {
                    if (writableDisplays.Count == 0)
                    {
                        error = "ADL baseline does not include writable display color controls.";
                        return false;
                    }

                    foreach (var displayKey in writableDisplays)
                    {
                        bool anyUpdated = false;
                        anyUpdated |= TryApplyChannel(context, displayKey.AdapterIndex, displayKey.DisplayIndex, "brightness", BrightnessTypeCandidates, adjustment.Brightness, 100, 50, 150);
                        anyUpdated |= TryApplyChannel(context, displayKey.AdapterIndex, displayKey.DisplayIndex, "contrast", ContrastTypeCandidates, adjustment.Contrast, 100, 50, 150);
                        anyUpdated |= TryApplyChannel(context, displayKey.AdapterIndex, displayKey.DisplayIndex, "saturation", SaturationTypeCandidates, adjustment.Vibrance, 50, 0, 100);

                        if (anyUpdated)
                        {
                            updatedDisplays++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = $"ADL apply failed: {ex.Message}";
                    return false;
                }
                finally
                {
                    _ = _mainControlDestroy!(context);
                }

                if (updatedDisplays == 0)
                {
                    error = "ADL did not expose writable display color controls for active outputs.";
                    return false;
                }

                error = null;
                return true;
            }
        }

        public bool TryRestore(out string? error)
        {
            lock (_sync)
            {
                if (!IsAvailable)
                {
                    error = AvailabilityMessage;
                    return false;
                }

                if (DisplayColorEnvironment.IsHdrActive(out string hdrDetails))
                {
                    error = $"ADL restore aborted: HDR is active. {hdrDetails}";
                    return false;
                }

                if (_originalValues.Count == 0)
                {
                    error = null;
                    return true;
                }

                if (!TryCreateContext(out IntPtr context, out error))
                {
                    return false;
                }

                try
                {
                    var failedEntries = new List<string>();
                    var unsupportedKeys = new List<ChannelKey>();
                    foreach (var kvp in _originalValues)
                    {
                        int setResult = _displayColorSet!(context, kvp.Key.AdapterIndex, kvp.Key.DisplayIndex, kvp.Key.ColorType, kvp.Value);
                        if (setResult == ADL_ERR_NOT_SUPPORTED)
                        {
                            unsupportedKeys.Add(kvp.Key);
                            continue;
                        }

                        if (setResult != ADL_OK)
                        {
                            if (failedEntries.Count < 8)
                            {
                                failedEntries.Add($"adapter={kvp.Key.AdapterIndex},display={kvp.Key.DisplayIndex},type={kvp.Key.ColorType},result={setResult}");
                            }
                        }
                    }

                    foreach (var key in unsupportedKeys)
                    {
                        _originalValues.Remove(key);
                        var displayChannel = new DisplayChannel(key.AdapterIndex, key.DisplayIndex, ResolveChannelNameByType(key.ColorType));
                        MarkWriteRejected(displayChannel, key.ColorType);
                    }

                    if (failedEntries.Count > 0)
                    {
                        error = $"ADL restore failed for {failedEntries.Count} channel(s): {string.Join("; ", failedEntries)}";
                        return false;
                    }

                    error = null;
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"ADL restore failed: {ex.Message}";
                    return false;
                }
                finally
                {
                    _ = _mainControlDestroy!(context);
                }
            }
        }

        private static bool TryLoadLibrary(string library, out IntPtr handle)
        {
            try
            {
                return NativeLibrary.TryLoad(library, out handle);
            }
            catch
            {
                handle = IntPtr.Zero;
                return false;
            }
        }

        private static T? GetExport<T>(IntPtr libraryHandle, string exportName)
            where T : Delegate
        {
            try
            {
                if (!NativeLibrary.TryGetExport(libraryHandle, exportName, out IntPtr proc) || proc == IntPtr.Zero)
                {
                    return null;
                }

                return Marshal.GetDelegateForFunctionPointer<T>(proc);
            }
            catch
            {
                return null;
            }
        }

        private bool TryCreateContext(out IntPtr context, out string? error)
        {
            context = IntPtr.Zero;
            int result = _mainControlCreate!(_mallocCallback, 1, out context);
            if (result != ADL_OK || context == IntPtr.Zero)
            {
                error = $"ADL2_Main_Control_Create failed ({result}).";
                return false;
            }

            error = null;
            return true;
        }

        private bool IsAdapterActive(IntPtr context, int adapterIndex)
        {
            try
            {
                return _adapterActiveGet!(context, adapterIndex, out int active) == ADL_OK && active != 0;
            }
            catch
            {
                return false;
            }
        }

        private bool TryApplyChannel(IntPtr context, int adapterIndex, int displayIndex, string channelName, int[] candidates, int sliderValue, int sliderCenter, int sliderMin, int sliderMax)
        {
            var channelKey = new DisplayChannel(adapterIndex, displayIndex, channelName);
            for (int attempt = 0; attempt < candidates.Length; attempt++)
            {
                if (!TryResolveColorChannel(context, adapterIndex, displayIndex, channelName, candidates, out var snapshot))
                {
                    return false;
                }

                int mapped = MapCenteredSlider(sliderValue, sliderCenter, sliderMin, sliderMax, snapshot.Min, snapshot.Max, snapshot.DefaultValue);
                if (snapshot.Step > 1)
                {
                    int offset = mapped - snapshot.Min;
                    int snapped = (int)Math.Round(offset / (double)snapshot.Step) * snapshot.Step;
                    mapped = Math.Clamp(snapshot.Min + snapped, snapshot.Min, snapshot.Max);
                }

                if (mapped == snapshot.Current)
                {
                    return true;
                }

                var originalKey = new ChannelKey(adapterIndex, displayIndex, snapshot.ColorType);
                if (!_originalValues.ContainsKey(originalKey))
                {
                    _originalValues[originalKey] = snapshot.Current;
                }

                int setResult = _displayColorSet!(context, adapterIndex, displayIndex, snapshot.ColorType, mapped);
                if (setResult == ADL_OK)
                {
                    return true;
                }

                if (setResult == ADL_ERR_NOT_SUPPORTED)
                {
                    MarkWriteRejected(channelKey, snapshot.ColorType);
                    _resolvedColorTypes.Remove(channelKey);

                    if (_loggedUnsupportedWrites.Add(originalKey))
                    {
                        Program.LogDebug(
                            $"ADL write not supported -> adapter={adapterIndex} display={displayIndex} channel={channelName} type={snapshot.ColorType}. Retrying alternate candidates.",
                            nameof(AmdAdlColorManager));
                    }

                    continue;
                }

                Program.LogDebug(
                    $"ADL write failed with code {setResult} -> adapter={adapterIndex} display={displayIndex} channel={channelName} type={snapshot.ColorType}.",
                    nameof(AmdAdlColorManager));
                return false;
            }

            return false;
        }

        private bool TryCaptureChannel(IntPtr context, int adapterIndex, int displayIndex, string channelName, int[] candidates)
        {
            if (!TryResolveColorChannel(context, adapterIndex, displayIndex, channelName, candidates, out var snapshot))
            {
                return false;
            }

            var displayChannel = new DisplayChannel(adapterIndex, displayIndex, channelName);
            int probeResult = _displayColorSet!(context, adapterIndex, displayIndex, snapshot.ColorType, snapshot.Current);
            if (probeResult != ADL_OK)
            {
                if (probeResult == ADL_ERR_NOT_SUPPORTED)
                {
                    MarkWriteRejected(displayChannel, snapshot.ColorType);
                    _resolvedColorTypes.Remove(displayChannel);

                    var unsupportedKey = new ChannelKey(adapterIndex, displayIndex, snapshot.ColorType);
                    if (_loggedUnsupportedWrites.Add(unsupportedKey))
                    {
                        Program.LogDebug(
                            $"ADL baseline skip (not writable) -> adapter={adapterIndex} display={displayIndex} channel={channelName} type={snapshot.ColorType}.",
                            nameof(AmdAdlColorManager));
                    }
                }
                else
                {
                    Program.LogDebug(
                        $"ADL baseline probe failed with code {probeResult} -> adapter={adapterIndex} display={displayIndex} channel={channelName} type={snapshot.ColorType}.",
                        nameof(AmdAdlColorManager));
                }

                return false;
            }

            var originalKey = new ChannelKey(adapterIndex, displayIndex, snapshot.ColorType);
            if (!_originalValues.ContainsKey(originalKey))
            {
                _originalValues[originalKey] = snapshot.Current;
            }

            return true;
        }

        private bool TryResolveColorChannel(IntPtr context, int adapterIndex, int displayIndex, string channelName, int[] candidates, out ColorChannelSnapshot snapshot)
        {
            var key = new DisplayChannel(adapterIndex, displayIndex, channelName);
            if (_resolvedColorTypes.TryGetValue(key, out int resolvedType))
            {
                if (TryReadChannel(context, adapterIndex, displayIndex, resolvedType, out snapshot))
                {
                    return true;
                }

                _resolvedColorTypes.Remove(key);
            }

            foreach (int candidateType in candidates)
            {
                if (IsWriteRejected(key, candidateType))
                {
                    continue;
                }

                if (!TryReadChannel(context, adapterIndex, displayIndex, candidateType, out var candidateSnapshot))
                {
                    continue;
                }

                if (candidateSnapshot.Max <= candidateSnapshot.Min)
                {
                    continue;
                }

                _resolvedColorTypes[key] = candidateType;
                snapshot = candidateSnapshot;

                if (_loggedResolvedChannels.Add(key))
                {
                    Program.LogDebug(
                        $"ADL channel resolved -> adapter={adapterIndex} display={displayIndex} channel={channelName} type={candidateType} range={candidateSnapshot.Min}-{candidateSnapshot.Max} default={candidateSnapshot.DefaultValue} step={candidateSnapshot.Step}",
                        nameof(AmdAdlColorManager));
                }

                return true;
            }

            snapshot = new ColorChannelSnapshot(0, 0, 0, 0, 0, 1);
            return false;
        }

        private bool IsWriteRejected(DisplayChannel channel, int colorType)
        {
            return _writeRejectedTypes.TryGetValue(channel, out var rejected) && rejected.Contains(colorType);
        }

        private void MarkWriteRejected(DisplayChannel channel, int colorType)
        {
            if (!_writeRejectedTypes.TryGetValue(channel, out var rejected))
            {
                rejected = new HashSet<int>();
                _writeRejectedTypes[channel] = rejected;
            }

            _ = rejected.Add(colorType);
        }

        private static string ResolveChannelNameByType(int colorType)
        {
            return colorType switch
            {
                ADL_DISPLAY_COLOR_BRIGHTNESS => "brightness",
                ADL_DISPLAY_COLOR_CONTRAST => "contrast",
                ADL_DISPLAY_COLOR_SATURATION => "saturation",
                _ => "unknown",
            };
        }

        private bool TryReadChannel(IntPtr context, int adapterIndex, int displayIndex, int colorType, out ColorChannelSnapshot snapshot)
        {
            int result = _displayColorGet!(
                context,
                adapterIndex,
                displayIndex,
                colorType,
                out int current,
                out int defaultValue,
                out int min,
                out int max,
                out int step);

            if (result != ADL_OK)
            {
                snapshot = new ColorChannelSnapshot(colorType, 0, 0, 0, 0, 1);
                return false;
            }

            snapshot = new ColorChannelSnapshot(colorType, current, defaultValue, min, max, Math.Max(step, 1));
            return true;
        }

        private static int MapCenteredSlider(int sliderValue, int sliderCenter, int sliderMin, int sliderMax, int min, int max, int defaultValue)
        {
            if (max <= min)
            {
                return defaultValue;
            }

            if (sliderValue == sliderCenter)
            {
                return Math.Clamp(defaultValue, min, max);
            }

            if (sliderValue > sliderCenter)
            {
                int span = Math.Max(1, sliderMax - sliderCenter);
                double t = (sliderValue - sliderCenter) / (double)span;
                return Math.Clamp(defaultValue + (int)Math.Round((max - defaultValue) * t), min, max);
            }

            int lowerSpan = Math.Max(1, sliderCenter - sliderMin);
            double down = (sliderCenter - sliderValue) / (double)lowerSpan;
            return Math.Clamp(defaultValue - (int)Math.Round((defaultValue - min) * down), min, max);
        }
    }
}

