param(
    [int]$Seconds = 60,
    [int]$WarmupSeconds = 10,
    [string]$OutputDirectory = "$PSScriptRoot/results",
    [switch]$SkipBuild,
    [switch]$SkipSettings,
    [switch]$Legacy,
    [string]$Executable
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$SkipBuild) {
    dotnet build "$repo/LightCrosshair/LightCrosshair.csproj" -c Release --nologo
    if ($LASTEXITCODE) { throw 'Build failed' }
}
if (!$Executable) {
    $Executable = (Get-ChildItem "$repo/LightCrosshair/bin/Release" -Recurse -Filter LightCrosshair.exe |
        Where-Object FullName -NotMatch 'publish' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
if (Get-Process LightCrosshair -ErrorAction SilentlyContinue) { throw 'Close the existing LightCrosshair process before measuring' }
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PerfNative {
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr p, uint f);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    public static long Query(IntPtr h, uint m) { IntPtr value; SendMessageTimeout(h,m,IntPtr.Zero,IntPtr.Zero,2,1000,out value); return value.ToInt64(); }
    public static IntPtr Find(int pid, bool legacy) {
        IntPtr found=IntPtr.Zero;
        EnumWindows((h,l)=>{ uint p; GetWindowThreadProcessId(h,out p); if(p==pid) { var title=new System.Text.StringBuilder(256); GetWindowText(h,title,256); if(title.ToString()=="LightCrosshair Core" || legacy) { found=h; return false; } } return true; },IntPtr.Zero);
        return found;
    }
}
'@
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$watch = [Diagnostics.Stopwatch]::StartNew()
$app = Start-Process -FilePath $Executable -PassThru -WindowStyle Hidden
try {
    $window = [IntPtr]::Zero
    while ($watch.Elapsed.TotalSeconds -lt 15 -and $window -eq [IntPtr]::Zero) {
        if ($app.HasExited) { throw 'App exited before creating its window' }
        $window = [PerfNative]::Find($app.Id, $Legacy.IsPresent)
        Start-Sleep -Milliseconds 25
    }
    $startupMs = $watch.Elapsed.TotalMilliseconds
    if ($window -eq [IntPtr]::Zero) { throw 'No Core window detected' }
    Start-Sleep -Seconds $WarmupSeconds
    $hasProbes = [PerfNative]::Query($window, 0x8069) -eq 0x4C433230
    $redrawsBefore = if ($hasProbes) { [PerfNative]::Query($window, 0x8064) } else { $null }
    $samples = @()
    $app.Refresh()
    $previousCpu = $app.TotalProcessorTime.TotalMilliseconds
    $previousTime = $watch.Elapsed.TotalMilliseconds
    for ($i = 0; $i -lt $Seconds; $i++) {
        Start-Sleep -Seconds 1
        $app.Refresh()
        $now = $watch.Elapsed.TotalMilliseconds
        $cpu = $app.TotalProcessorTime.TotalMilliseconds
        $samples += [pscustomobject]@{
            Second = $i + 1; PrivateBytes = $app.PrivateMemorySize64; WorkingSet = $app.WorkingSet64
            CpuOneCorePercent = 100 * ($cpu - $previousCpu) / ($now - $previousTime)
            GdiHandles = [PerfNative]::GetGuiResources($app.Handle, 0)
            UserHandles = [PerfNative]::GetGuiResources($app.Handle, 1)
        }
        $previousCpu = $cpu; $previousTime = $now
    }
    $before = $app.PrivateMemorySize64
    $redrawsAfter = if ($hasProbes) { [PerfNative]::Query($window, 0x8064) } else { $null }
    $activeTimers = if ($hasProbes) { [PerfNative]::Query($window, 0x8067) } else { $null }
    $settingsCycles = 0
    $verifiedCycles = 0
    $uiPath = Join-Path (Split-Path -Parent $Executable) 'LightCrosshair.UI.exe'
    if (!$SkipSettings -and (Test-Path -LiteralPath $uiPath)) {
        for ($cycle = 0; $cycle -lt 5; $cycle++) {
            [void][PerfNative]::PostMessage($window, 0x312, [IntPtr]9003, [IntPtr]::Zero)
            $deadline = [DateTime]::UtcNow.AddSeconds(8)
            $ui = $null
            do {
                Start-Sleep -Milliseconds 100
                $ui = Get-Process -Name 'LightCrosshair.UI' -ErrorAction SilentlyContinue | Where-Object Path -EQ $uiPath | Select-Object -First 1
                $opened = $ui -and $ui.MainWindowHandle -ne [IntPtr]::Zero -and (!$hasProbes -or [PerfNative]::Query($window, 0x8066) -eq 1)
            } until ($opened -or [DateTime]::UtcNow -gt $deadline)
            if (!$opened) { break }
            [void][PerfNative]::PostMessage($window, 0x312, [IntPtr]9003, [IntPtr]::Zero)
            $closed = $ui.WaitForExit(8000)
            $settingsCycles++
            if ($closed) { $verifiedCycles++ } else { break }
        }
    }
    $app.Refresh()
    $summary = [ordered]@{
        MeasuredAt = (Get-Date).ToUniversalTime().ToString('O'); Executable = $Executable
        OS = [Environment]::OSVersion.VersionString; Cpu = (Get-CimInstance Win32_Processor).Name
        LogicalProcessors = [Environment]::ProcessorCount; StartupWindowMs = [math]::Round($startupMs, 1)
        WarmupSeconds = $WarmupSeconds; IdleSeconds = $Seconds
        PrivateMB = [math]::Round(($samples | Measure-Object PrivateBytes -Average).Average / 1MB, 2)
        WorkingSetMB = [math]::Round(($samples | Measure-Object WorkingSet -Average).Average / 1MB, 2)
        CpuOneCorePercent = [math]::Round(($samples | Measure-Object CpuOneCorePercent -Average).Average, 4)
        GdiHandles = $samples[-1].GdiHandles; UserHandles = $samples[-1].UserHandles
        SettingsCyclesRequested = $settingsCycles
        SettingsCyclesVerified = $verifiedCycles -eq 5
        VerifiedSettingsCycles = $verifiedCycles
        SettingsPrivateDeltaMB = [math]::Round(($app.PrivateMemorySize64 - $before) / 1MB, 2)
        RedrawsPerSecond = if ($hasProbes) { ($redrawsAfter - $redrawsBefore) / $Seconds } else { $null }
        ActiveRepeatingTimers = $activeTimers
        WpfLoadedInCore = if ($hasProbes) { [PerfNative]::Query($window, 0x8065) -eq 1 } else { $null }
        Notes = 'Startup = first top-level HWND, not settings interactive. v2 probes read redraw count, repeating timers and loaded WPF assemblies. v1 settings cycles remain unverified.'
    }
    $samples | Export-Csv "$OutputDirectory/idle.csv" -NoTypeInformation
    $summary | ConvertTo-Json | Set-Content "$OutputDirectory/summary.json"
    $summary | ConvertTo-Json
}
finally {
    if (!$app.HasExited) {
        [void][PerfNative]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)
        if (!$app.WaitForExit(5000)) { $app.Kill() }
    }
    $app.Dispose()
}
