$ErrorActionPreference = 'Stop'
if (!('ReviewNative' -as [type])) {
    Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class ReviewNative {
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int show);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint relation);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    public static bool TransferProbeFocus(IntPtr from, IntPtr to) { SendMessageTimeout(from,0x8065,to,IntPtr.Zero,2,1000,out var result); return result!=IntPtr.Zero; }
    public static IntPtr Find(int pid, string title) { IntPtr result=IntPtr.Zero; EnumWindows((h,p)=> { GetWindowThreadProcessId(h,out uint id); var text=new StringBuilder(256); GetWindowText(h,text,256); if(id==pid && text.ToString()==title) result=h; return true; },IntPtr.Zero); return result; }
    public static bool Above(IntPtr overlay, IntPtr other) { for(var h=GetWindow(other,3);h!=IntPtr.Zero;h=GetWindow(h,3)) if(h==overlay) return true; return false; }
}
'@
}
if (!('PerfNative' -as [type])) {
    $source = Get-Content -LiteralPath "$PSScriptRoot/measure.ps1" -Raw
    Add-Type -TypeDefinition (($source -split "Add-Type @'", 2)[1] -split "'@", 2)[0]
}
function Start-ProbeCore([string]$Executable, [string]$Root, [switch]$ProductionPipe) {
    $app = $null; $pipe = $null
    $previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT; $previousInstance = $env:LIGHTCROSSHAIR_TEST_INSTANCE
    try {
        $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path $Root 'v2'
        if ($ProductionPipe -and (Get-Process LightCrosshair -ErrorAction SilentlyContinue)) { throw 'Close other Core instances before a production-pipe UI benchmark' }
        $env:LIGHTCROSSHAIR_TEST_INSTANCE = if ($ProductionPipe) { '' } else { [Guid]::NewGuid().ToString('N') }
        $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
        $pipeName = "LightCrosshair.v2.$($app.SessionId)" + $(if (!$ProductionPipe) { ".test.$env:LIGHTCROSSHAIR_TEST_INSTANCE" })
        $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', $pipeName, [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
        $pipe.Connect(10000)
        return @{ App=$app; Pipe=$pipe; PipeName=$pipeName; Reader=[IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 4096, $true); Id=0; Events=[Collections.Generic.List[object]]::new(); Root=$env:LIGHTCROSSHAIR_DATA_ROOT; Instance=$env:LIGHTCROSSHAIR_TEST_INSTANCE }
    } catch {
        if ($pipe) { $pipe.Dispose() }; if ($app) { if (!$app.HasExited) { $app.Kill() }; $app.Dispose() }; throw
    } finally { $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot; $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance }
}
function Invoke-ProbeCore($Session, [string]$Method, $Payload = $null) {
    $Session.Id++
    $bytes = [Text.Encoding]::UTF8.GetBytes((@{kind='req';id=$Session.Id;method=$Method;payload=$Payload} | ConvertTo-Json -Depth 48 -Compress) + "`n")
    $Session.Pipe.Write($bytes, 0, $bytes.Length); $Session.Pipe.Flush()
    do {
        $response = $Session.Reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(10)).GetAwaiter().GetResult() | ConvertFrom-Json -Depth 48
        if (!$response) { throw 'Core disconnected' }
        if ($response.kind -eq 'evt') { $Session.Events.Add($response) }
    } until ($response.kind -eq 'res' -and $response.id -eq $Session.Id)
    if (!$response.ok) { throw ($response.error | ConvertTo-Json -Compress) }
    return $response.payload
}
function Stop-ProbeCore($Session) {
    if (!$Session) { return }
    try { if (!$Session.App.HasExited) { Invoke-ProbeCore $Session quit | Out-Null; if (!$Session.App.WaitForExit(10000)) { throw 'Core shutdown timed out' } } }
    finally { $Session.Reader.Dispose(); $Session.Pipe.Dispose(); if (!$Session.App.HasExited) { $Session.App.Kill() }; $Session.App.Dispose() }
}
