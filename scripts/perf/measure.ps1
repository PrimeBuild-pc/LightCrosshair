param(
    [int]$Seconds = 60,
    [ValidateRange(1, 100)][int]$SettingsCycles = 20,
    [string]$DiagnosticsTools,
    [switch]$ProfileHeap,
    [int]$WarmupSeconds = 10,
    [string]$OutputDirectory = "$PSScriptRoot/results",
    [switch]$SkipBuild,
    [switch]$SkipSettings,
    [switch]$Legacy,
    [switch]$Isolated,
    [string]$Executable
)
$ErrorActionPreference = 'Stop'
if ($ProfileHeap -and !$DiagnosticsTools) { throw '-ProfileHeap requires -DiagnosticsTools' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Executable -and !$SkipBuild -and !$Legacy) {
    & "$repo/scripts/publish.ps1" -Runtime win-x64 -OutputRoot dist
    $Executable = Join-Path $repo 'dist/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe'
}
elseif (!$SkipBuild) {
    dotnet build "$repo/LightCrosshair/LightCrosshair.csproj" -c Release --nologo
    if ($LASTEXITCODE) { throw 'Build failed' }
}
if (!$Executable) {
    $Executable = (Get-ChildItem "$repo/LightCrosshair/bin/Release" -Recurse -Filter LightCrosshair.exe |
        Where-Object FullName -NotMatch 'publish' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
$Executable = [IO.Path]::GetFullPath($Executable)
if (!$Isolated) {
    $existingMutex = $null
    if ($Legacy -and (Get-Process LightCrosshair -ErrorAction SilentlyContinue)) { throw 'Close the existing v1 process before measuring' }
    if ([Threading.Mutex]::TryOpenExisting('Local\LightCrosshair.Core', [ref]$existingMutex)) {
        $existingMutex.Dispose(); throw 'Close the existing production Core before measuring, or use -Isolated -SkipSettings'
    }
}
if (!$Isolated -and !$SkipSettings -and (Get-Process 'LightCrosshair.UI' -ErrorAction SilentlyContinue)) { throw 'An existing UI process would interfere with lifecycle measurements' }
if (!$Isolated -and !$SkipSettings -and !$Legacy) {
    $placementSource = Join-Path $repo 'LightCrosshair.UI/Services/WindowPlacementStore.cs'
    if (Test-Path -LiteralPath $placementSource) {
        $placement = Get-Content -LiteralPath $placementSource -Raw
        if ($placement.Contains('SpecialFolder.ApplicationData') -and !$placement.Contains('LIGHTCROSSHAIR_DATA_ROOT')) {
            throw 'UI placement must honor LIGHTCROSSHAIR_DATA_ROOT. Use an isolated UI build or -SkipSettings.'
        }
    }
}
if ($Isolated) {
    $SkipSettings = $true
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = [Guid]::NewGuid().ToString('N')
    if (!$env:LIGHTCROSSHAIR_DATA_ROOT) { $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path ([IO.Path]::GetTempPath()) ("lc2-perf-$env:LIGHTCROSSHAIR_TEST_INSTANCE/v2") }
}
if (!$Legacy -and !$env:LIGHTCROSSHAIR_DATA_ROOT) {
    $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path ([IO.Path]::GetTempPath()) ("lc2-perf-" + [Guid]::NewGuid().ToString('N') + '/v2')
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PerfNative {
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr p, uint f);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder text, int count);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool WaitNamedPipe(string name, uint timeout);
    public static bool PipeBusy(string name) {
        if (WaitNamedPipe(@"\\.\pipe\" + name, 1)) return false;
        int error = Marshal.GetLastWin32Error(); return error == 121 || error == 231;
    }
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
$app = $null; $counterProcess = $null
try {
    $app = Start-Process -FilePath $Executable -PassThru -WindowStyle Hidden
    if ($DiagnosticsTools) {
        $counterProcess = Start-Process -FilePath (Join-Path $DiagnosticsTools 'dotnet-counters.exe') -ArgumentList @('collect', '--process-id', $app.Id, '--counters', 'System.Runtime', '--refresh-interval', '1', '--format', 'csv', '--output', ([IO.Path]::GetFullPath("$OutputDirectory/runtime.csv")), '--duration', '00:05:00') -WindowStyle Hidden -PassThru
    }
    $window = [IntPtr]::Zero
    while ($watch.Elapsed.TotalSeconds -lt 15 -and $window -eq [IntPtr]::Zero) {
        if ($app.HasExited) { throw 'App exited before creating its window' }
        $window = [PerfNative]::Find($app.Id, $Legacy.IsPresent)
        Start-Sleep -Milliseconds 25
    }
    $startupMs = $watch.Elapsed.TotalMilliseconds
    if ($window -eq [IntPtr]::Zero) { throw 'No Core window detected' }
    if ($Isolated) {
        # Release live system shortcuts so another worktree can be tested concurrently.
        $fixturePipe = [IO.Pipes.NamedPipeClientStream]::new('.', "LightCrosshair.v2.$($app.SessionId).test.$env:LIGHTCROSSHAIR_TEST_INSTANCE", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
        $fixturePipe.Connect(5000)
        $fixtureReader = [IO.StreamReader]::new($fixturePipe, [Text.Encoding]::UTF8, $false, 4096, $true)
        $script:fixtureId = 0
        function Invoke-PerfFixture([string]$method, $payload) {
            $script:fixtureId++
            $json = @{kind='req';id=$script:fixtureId;method=$method;payload=$payload} | ConvertTo-Json -Depth 32 -Compress
            $bytes = [Text.Encoding]::UTF8.GetBytes($json + "`n")
            $fixturePipe.Write($bytes, 0, $bytes.Length); $fixturePipe.Flush()
            do {
                $response = $fixtureReader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult() | ConvertFrom-Json -Depth 32
            } until ($response.kind -eq 'res' -and $response.id -eq $script:fixtureId)
            if (!$response.ok) { throw "Fixture setup failed: $($response.error.code)" }
            return $response.payload
        }
        try {
            Invoke-PerfFixture hello @{protocolVersion=1;client='perf-fixture'} | Out-Null
            $fixture = Invoke-PerfFixture getSnapshot $null
            foreach ($action in @('toggleVisibility','nextCrosshair','previousCrosshair','openSettings')) { $fixture.store.settings.hotkeys.$action.key='' }
            Invoke-PerfFixture saveSettings $fixture.store.settings | Out-Null
        }
        finally { $fixtureReader.Dispose(); $fixturePipe.Dispose() }
    }
    Start-Sleep -Seconds $WarmupSeconds
    $hasProbes = [PerfNative]::Query($window, 0x8069) -eq 0x4C433230
    $redrawsBefore = if ($hasProbes) { [PerfNative]::Query($window, 0x8064) } else { $null }
    $samples = @()
    $app.Refresh()
    $previousCpu = $app.TotalProcessorTime.TotalMilliseconds
    $previousTime = $watch.Elapsed.TotalMilliseconds
    for ($i = 0; $i -lt $Seconds; $i++) {
        Start-Sleep -Seconds 1
        if ($app.HasExited) { throw 'Core exited during the idle interval; this run is invalid' }
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
    if ($app.HasExited -or ($hasProbes -and [PerfNative]::Query($window, 0x8069) -ne 0x4C433230)) { throw 'Core window changed or exited; this run is invalid' }
    $redrawsAfter = if ($hasProbes) { [PerfNative]::Query($window, 0x8064) } else { $null }
    $activeTimers = if ($hasProbes) { [PerfNative]::Query($window, 0x8067) } else { $null }
    $completedCycles = 0
    $verifiedCycles = 0
    $cyclePrivateMB = @()
    $cycleHandles = @()
    $coldUiWindowMs = $null
    $uiPath = Join-Path (Split-Path -Parent $Executable) 'LightCrosshair.UI.exe'
    if (!$SkipSettings -and !$Legacy -and !(Test-Path -LiteralPath $uiPath)) { throw 'Publish both Core and UI together, then pass -Executable to the published Core' }
    if (!$SkipSettings -and (Test-Path -LiteralPath $uiPath)) {
        for ($cycle = 0; $cycle -lt $SettingsCycles; $cycle++) {
            $uiWatch = [Diagnostics.Stopwatch]::StartNew()
            [void][PerfNative]::PostMessage($window, 0x312, [IntPtr]9003, [IntPtr]::Zero)
            $deadline = [DateTime]::UtcNow.AddSeconds(8)
            $ui = $null
            do {
                Start-Sleep -Milliseconds 100
                $ui = Get-Process -Name 'LightCrosshair.UI' -ErrorAction SilentlyContinue | Where-Object Path -EQ $uiPath | Select-Object -First 1
                $opened = $ui -and $ui.MainWindowHandle -ne [IntPtr]::Zero -and ($(if ($hasProbes) { [PerfNative]::Query($window, 0x8066) -eq 1 } else { [PerfNative]::PipeBusy("LightCrosshair.v2.$($app.SessionId)") }))
            } until ($opened -or [DateTime]::UtcNow -gt $deadline)
            if (!$opened) { throw "UI cycle $($cycle + 1) never connected" }
            # A production pipe can become busy just before hello is dispatched.
            Start-Sleep -Milliseconds 200
            if ($cycle -eq 0) { $coldUiWindowMs = [math]::Round($uiWatch.Elapsed.TotalMilliseconds, 1) }
            $uiWindow = $ui.MainWindowHandle
            [void]$ui.CloseMainWindow() # exercise the window-close/parking path, as the warm observer does
            $deadline = [DateTime]::UtcNow.AddSeconds(8)
            do { Start-Sleep -Milliseconds 25; $closed = $ui.HasExited -or ![PerfNative]::IsWindowVisible($uiWindow) } until($closed -or [DateTime]::UtcNow -gt $deadline)
            $completedCycles++
            $app.Refresh(); $cyclePrivateMB += [math]::Round($app.PrivateMemorySize64 / 1MB, 2); $cycleHandles += $app.HandleCount
            if ($closed) { $verifiedCycles++ } else { throw "UI cycle $($cycle + 1) did not close or park" }
            if ($ProfileHeap -and $cycle + 1 -in @(5, $SettingsCycles)) {
                & (Join-Path $DiagnosticsTools 'dotnet-gcdump.exe') collect --process-id $app.Id --output "$OutputDirectory/cycle-$($cycle + 1).gcdump"
                if ($LASTEXITCODE) { throw 'Heap collection failed' }
            }
        }
    }
    $app.Refresh()
    $summary = [ordered]@{
        MeasuredAt = (Get-Date).ToUniversalTime().ToString('O'); Executable = $Executable
        OS = [Environment]::OSVersion.VersionString; Cpu = (Get-CimInstance Win32_Processor).Name
        LogicalProcessors = [Environment]::ProcessorCount; StartupWindowMs = [math]::Round($startupMs, 1)
        Isolated = $Isolated.IsPresent
        WarmupSeconds = $WarmupSeconds; IdleSeconds = $Seconds
        PrivateMB = [math]::Round(($samples | Measure-Object PrivateBytes -Average).Average / 1MB, 2)
        WorkingSetMB = [math]::Round(($samples | Measure-Object WorkingSet -Average).Average / 1MB, 2)
        CpuOneCorePercent = [math]::Round(($samples | Measure-Object CpuOneCorePercent -Average).Average, 4)
        GdiHandles = $samples[-1].GdiHandles; UserHandles = $samples[-1].UserHandles
        SettingsCyclesRequested = $SettingsCycles
        SettingsCyclesVerified = $verifiedCycles -eq $SettingsCycles
        VerifiedSettingsCycles = $verifiedCycles
        SettingsColdWindowMs = $coldUiWindowMs
        PrivateBeforeSettingsCyclesMB = [math]::Round($before / 1MB, 2)
        PrivateAfterEachSettingsCycleMB = $cyclePrivateMB
        HandlesAfterEachSettingsCycle = $cycleHandles
        FiveCyclePrivateDeltaMB = if ($cyclePrivateMB.Count -ge 5) { [math]::Round($cyclePrivateMB[4] - $before / 1MB, 2) } else { $null }
        Cycle5To20PrivateDeltaMB = if ($cyclePrivateMB.Count -ge 20) { [math]::Round($cyclePrivateMB[19] - $cyclePrivateMB[4], 2) } else { $null }
        SettingsPrivateDeltaMB = [math]::Round(($app.PrivateMemorySize64 - $before) / 1MB, 2)
        RedrawsPerSecond = if ($hasProbes) { ($redrawsAfter - $redrawsBefore) / $Seconds } else { $null }
        ActiveRepeatingTimers = $activeTimers
        WpfLoadedInCore = if ($hasProbes) { [PerfNative]::Query($window, 0x8065) -eq 1 } else { $null }
        Notes = 'UI startup measures window plus connected pipe, not interactive readiness. Diagnostic fields are null in production Release (probes gated). ProfileHeap forces collections: use a separate uninstrumented run for memory/CPU budgets. v1 cycles remain unverified.'
    }
    $samples | Export-Csv "$OutputDirectory/idle.csv" -NoTypeInformation
    $summary | ConvertTo-Json | Set-Content "$OutputDirectory/summary.json"
    $summary | ConvertTo-Json
}
finally {
    # Release only the processes created by this run, including a parked UI.
    if ($ui -and !$ui.HasExited) {
        [void]$ui.CloseMainWindow()
        if (!$ui.WaitForExit(5000)) { $ui.Kill(); $ui.WaitForExit() }
    }
    if ($app -and !$app.HasExited) {
        [void][PerfNative]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)
        if (!$app.WaitForExit(5000)) { $app.Kill() }
    }
    if ($counterProcess) { if (!$counterProcess.HasExited) { $counterProcess.Kill() }; $counterProcess.Dispose() }
    if ($app) { $app.Dispose() }
}
