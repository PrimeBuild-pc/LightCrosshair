param([string]$Executable="$PSScriptRoot/results-final/color-core/LightCrosshair.exe",[string]$OutputDirectory="$PSScriptRoot/results-final/color-recovery")
. "$PSScriptRoot/ReviewProbe.ps1"
$out=[IO.Path]::GetFullPath($OutputDirectory);New-Item -ItemType Directory -Force -Path $out | Out-Null
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc-final-colors-'+[guid]::NewGuid().ToString('N'))
$exe=[IO.Path]::GetFullPath($Executable)
dotnet build "$PSScriptRoot/ColorProbe" -c Release --nologo
if($LASTEXITCODE){throw 'Readback helper build failed'}
$reader="$PSScriptRoot/ColorProbe/bin/Release/net10.0-windows/ColorProbe.exe"
function Read-Colors([string]$Name){& $reader (Join-Path $out ($Name+'.json')) (Join-Path $root 'readback');if($LASTEXITCODE){throw 'Readback failed'}}
function Assert-Same([string]$A,[string]$B){if((Get-FileHash (Join-Path $out ($A+'.json'))).Hash -ne (Get-FileHash (Join-Path $out ($B+'.json'))).Hash){throw "Color readbacks differ: $A and $B"}}
function Connect-Again($s){$s.Pipe=[IO.Pipes.NamedPipeClientStream]::new('.',$s.PipeName,[IO.Pipes.PipeDirection]::InOut,[IO.Pipes.PipeOptions]::Asynchronous);$s.Pipe.Connect(10000);$s.Reader=[IO.StreamReader]::new($s.Pipe,[Text.Encoding]::UTF8,$false,4096,$true);Invoke-ProbeCore $s hello @{protocolVersion=1;client='color-recovery'} | Out-Null}
$session=$null
Read-Colors original
try {
 $session=Start-ProbeCore $exe $root;Invoke-ProbeCore $session hello @{protocolVersion=1;client='color-recovery'} | Out-Null
 $settings=(Invoke-ProbeCore $session getSnapshot).store.settings
 $settings | Add-Member displayColorsEnabled $true -Force
 $settings | Add-Member displayColors @{gamma=110;contrast=110;brightness=105;vibrance=60} -Force
 Invoke-ProbeCore $session saveSettings $settings | Out-Null;Start-Sleep -Seconds 1;Read-Colors global
 Invoke-ProbeCore $session previewDisplayColors @{colors=@{gamma=120;contrast=120;brightness=110;vibrance=65}} | Out-Null;Read-Colors preview
 Invoke-ProbeCore $session previewDisplayColors @{colors=$null} | Out-Null;Read-Colors released;Assert-Same global released
 Invoke-ProbeCore $session previewDisplayColors @{colors=@{gamma=120;contrast=120;brightness=110;vibrance=65}} | Out-Null
 $session.Reader.Dispose();$session.Pipe.Dispose();Start-Sleep -Seconds 1;Read-Colors disconnected;Assert-Same global disconnected
 Connect-Again $session
 Invoke-ProbeCore $session previewDisplayColors @{colors=@{gamma=120;contrast=120;brightness=110;vibrance=65}} | Out-Null
 $session.App.Kill();$session.App.WaitForExit();Stop-ProbeCore $session;$session=$null
 if(!(Test-Path -LiteralPath (Join-Path $root 'v2/colors-applied.json'))){throw 'Forced kill did not retain recovery journal'}
 $session=Start-ProbeCore $exe $root;Invoke-ProbeCore $session hello @{protocolVersion=1;client='color-recovery'} | Out-Null
 $settings=(Invoke-ProbeCore $session getSnapshot).store.settings;$settings.displayColorsEnabled=$false
 Invoke-ProbeCore $session saveSettings $settings | Out-Null;Start-Sleep -Seconds 1;Read-Colors recovered;Assert-Same original recovered
 if((Invoke-ProbeCore $session getSnapshot).runtime.displayColorsError){throw 'Driver reported a color error'}
} finally {
 # Startup always replays the durable original before applying persisted globals.
 if(!$session -and (Test-Path -LiteralPath (Join-Path $root 'v2/colors-applied.json'))){$session=Start-ProbeCore $exe $root;Invoke-ProbeCore $session hello @{protocolVersion=1;client='recovery-cleanup'} | Out-Null}
 Stop-ProbeCore $session
 Read-Colors exited;Assert-Same original exited
 if(Test-Path -LiteralPath (Join-Path $root 'v2/colors-applied.json')){throw 'Recovery marker remains after exit'}
}
$rows=foreach($file in Get-ChildItem -LiteralPath $out -Filter '*.json'){
 $d=Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
 foreach($entry in $d.Displays.psobject.Properties){[pscustomobject]@{Stage=$file.BaseName;Backend='GDI';Adapter='';Display=$entry.Name;Channel='Red[128]';Value=$entry.Value.Red[128]}}
 foreach($channel in $d.AmdChannels){[pscustomobject]@{Stage=$file.BaseName;Backend='ADL';Adapter=$channel.Adapter;Display=$channel.Display;Channel=$channel.Type;Value=$channel.Value}}
}
$rows | Export-Csv -LiteralPath (Join-Path $out 'readback.csv') -NoTypeInformation
'PASS: global colors, final preview, release, disconnect, forced kill/recovery and exit; exact original snapshot restored.'
