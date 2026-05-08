# Research-only PresentMon CSV analyzer.
# Reads an existing CSV file and prints conservative offline summary fields.
# It never launches PresentMon, starts ETW, changes app runtime behavior, or
# writes RTSS/driver/vendor settings.

param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,

    [switch]$ShowColumns
)

$ErrorActionPreference = "Stop"

function Normalize-Name([string]$Value) {
    if ($null -eq $Value) {
        return ""
    }

    return ([regex]::Replace($Value, "[^A-Za-z0-9]", "")).ToLowerInvariant()
}

function Find-Column([string[]]$Columns, [string[]]$Aliases) {
    $lookup = @{}
    for ($i = 0; $i -lt $Columns.Count; $i++) {
        $key = Normalize-Name $Columns[$i]
        if (-not $lookup.ContainsKey($key)) {
            $lookup[$key] = $Columns[$i]
        }
    }

    foreach ($alias in $Aliases) {
        $key = Normalize-Name $alias
        if ($lookup.ContainsKey($key)) {
            return $lookup[$key]
        }
    }

    return $null
}

function Get-Value($Row, [string]$Column) {
    if ([string]::IsNullOrWhiteSpace($Column)) {
        return ""
    }

    $property = $Row.PSObject.Properties[$Column]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ""
    }

    return ([string]$property.Value).Trim()
}

function Get-Double($Row, [string]$Column) {
    $value = Get-Value $Row $Column
    if ([string]::IsNullOrWhiteSpace($value) -or $value -ieq "NA") {
        return $null
    }

    $result = 0.0
    if ([double]::TryParse($value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$result)) {
        if (-not [double]::IsNaN($result) -and -not [double]::IsInfinity($result) -and $result -gt 0) {
            return $result
        }
    }

    return $null
}

function Get-Bool($Row, [string]$Column) {
    $value = Get-Value $Row $Column
    if ([string]::IsNullOrWhiteSpace($value) -or $value -ieq "NA") {
        return $null
    }

    if ($value -match '^(1|true|yes)$') {
        return $true
    }

    if ($value -match '^(0|false|no)$') {
        return $false
    }

    return $null
}

function Get-Percentile([double[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) {
        return $null
    }

    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 1) {
        return $sorted[0]
    }

    $position = ($sorted.Count - 1) * $Percentile
    $lower = [int][math]::Floor($position)
    $upper = [int][math]::Ceiling($position)
    if ($lower -eq $upper) {
        return $sorted[$lower]
    }

    $fraction = $position - $lower
    return $sorted[$lower] + (($sorted[$upper] - $sorted[$lower]) * $fraction)
}

function Format-Number($Value) {
    if ($null -eq $Value) {
        return "N/A"
    }

    return ([double]$Value).ToString("0.###", [System.Globalization.CultureInfo]::InvariantCulture)
}

function Test-GeneratedFrameType([string]$FrameType) {
    $normalized = Normalize-Name $FrameType
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return $false
    }

    return $normalized.Contains("generated") -or
        $normalized.Contains("interpolated")
}

function Read-CsvFields([string]$Path) {
    Add-Type -AssemblyName Microsoft.VisualBasic

    $reader = [System.IO.StreamReader]::new($Path, [System.Text.Encoding]::UTF8, $true)
    try {
        $parser = [Microsoft.VisualBasic.FileIO.TextFieldParser]::new($reader)
        try {
            $parser.TextFieldType = [Microsoft.VisualBasic.FileIO.FieldType]::Delimited
            $parser.SetDelimiters(",")
            $parser.HasFieldsEnclosedInQuotes = $true
            $parser.TrimWhiteSpace = $false

            while (-not $parser.EndOfData) {
                try {
                    Write-Output -NoEnumerate $parser.ReadFields()
                }
                catch [Microsoft.VisualBasic.FileIO.MalformedLineException] {
                    Write-Warning "Skipping malformed CSV row $($parser.ErrorLineNumber): $($_.Exception.Message)"
                }
            }
        }
        finally {
            $parser.Close()
        }
    }
    finally {
        $reader.Dispose()
    }
}

function Get-NormalizedHeaders([string[]]$Headers) {
    $seen = @{}
    $normalized = New-Object System.Collections.Generic.List[string]
    $duplicates = New-Object System.Collections.Generic.List[string]

    foreach ($header in $Headers) {
        $name = if ($null -eq $header) { "" } else { $header.Trim() }
        if ([string]::IsNullOrWhiteSpace($name)) {
            $name = "Column$($normalized.Count + 1)"
        }

        if (-not $seen.ContainsKey($name)) {
            $seen[$name] = 1
            $normalized.Add($name)
            continue
        }

        $seen[$name] = [int]$seen[$name] + 1
        $deduped = "$name`__$($seen[$name])"
        while ($seen.ContainsKey($deduped)) {
            $seen[$name] = [int]$seen[$name] + 1
            $deduped = "$name`__$($seen[$name])"
        }

        $seen[$deduped] = 1
        $normalized.Add($deduped)
        $duplicates.Add("$name -> $deduped")
    }

    return [pscustomobject]@{
        Headers = [string[]]$normalized.ToArray()
        DuplicateMappings = [string[]]$duplicates.ToArray()
    }
}

function ConvertTo-SafeCsvRows([string]$Path) {
    $csvRows = @(Read-CsvFields $Path)
    if ($csvRows.Count -eq 0) {
        return [pscustomobject]@{
            Rows = @()
            Columns = @()
            DuplicateMappings = @()
        }
    }

    $headerInfo = Get-NormalizedHeaders ([string[]]$csvRows[0])
    $headers = [string[]]$headerInfo.Headers
    $rows = New-Object System.Collections.Generic.List[object]

    for ($rowIndex = 1; $rowIndex -lt $csvRows.Count; $rowIndex++) {
        $fields = [string[]]$csvRows[$rowIndex]
        if ($fields.Count -eq 0 -or (($fields | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -eq 0)) {
            continue
        }

        $row = [ordered]@{}
        for ($columnIndex = 0; $columnIndex -lt $headers.Count; $columnIndex++) {
            $row[$headers[$columnIndex]] = if ($columnIndex -lt $fields.Count) { $fields[$columnIndex] } else { "" }
        }

        if ($fields.Count -gt $headers.Count) {
            Write-Warning "Row $($rowIndex + 1) has $($fields.Count) field(s), but the header has $($headers.Count); extra fields were ignored."
        }

        $rows.Add([pscustomobject]$row)
    }

    return [pscustomobject]@{
        Rows = @($rows.ToArray())
        Columns = $headers
        DuplicateMappings = [string[]]$headerInfo.DuplicateMappings
    }
}

$resolvedPath = Resolve-Path -LiteralPath $CsvPath
$csv = ConvertTo-SafeCsvRows $resolvedPath
$rows = @($csv.Rows)
if ($rows.Count -eq 0) {
    Write-Error "No CSV samples were found in $resolvedPath"
}

$columns = @($csv.Columns)
foreach ($mapping in $csv.DuplicateMappings) {
    Write-Warning "Duplicate columns normalized: $mapping"
}

$appColumn = Find-Column $columns @("Application", "App", "Process", "ProcessName", "ExecutableName")
$fpsColumn = Find-Column $columns @("FPS", "Average FPS", "Avg FPS", "Displayed FPS", "Presented FPS", "FPS-Display", "FPS-Presents", "FPS-App")
$frameTimeColumn = Find-Column $columns @("FrameTime", "MsBetweenPresents", "MsBetweenAppStart", "MsBetweenDisplayChange", "MsBetweenDisplayChangeActual")
$presentIntervalColumn = Find-Column $columns @("MsBetweenPresents", "PresentInterval", "PresentIntervalMs")
$displayIntervalColumn = Find-Column $columns @("MsBetweenDisplayChange", "MsBetweenDisplayChangeActual", "DisplayInterval", "DisplayIntervalMs", "DisplayedTime")
$presentModeColumn = Find-Column $columns @("PresentMode")
$allowsTearingColumn = Find-Column $columns @("AllowsTearing")
$droppedColumn = Find-Column $columns @("Dropped", "DroppedFrame", "DroppedFrames", "WasDropped")
$lateColumn = Find-Column $columns @("Late", "LateFrame", "IsLate")
$frameTypeColumn = Find-Column $columns @("FrameType", "FrameClassification", "DisplayedFrameType")

$frameTimes = New-Object System.Collections.Generic.List[double]
$fpsValues = New-Object System.Collections.Generic.List[double]
$presentModes = @{}
$generatedFrameCount = 0
$allowsTearingCount = 0
$droppedCount = 0
$lateCount = 0
$appCounts = @{}

foreach ($row in $rows) {
    $app = Get-Value $row $appColumn
    if (-not [string]::IsNullOrWhiteSpace($app)) {
        $appCounts[$app] = 1 + [int]($appCounts[$app])
    }

    $fps = Get-Double $row $fpsColumn
    if ($null -ne $fps) {
        $fpsValues.Add($fps)
    }

    $frameTime = Get-Double $row $frameTimeColumn
    if ($null -eq $frameTime) {
        $frameTime = Get-Double $row $displayIntervalColumn
    }
    if ($null -eq $frameTime) {
        $frameTime = Get-Double $row $presentIntervalColumn
    }
    if ($null -ne $frameTime) {
        $frameTimes.Add($frameTime)
    }

    $presentMode = Get-Value $row $presentModeColumn
    if (-not [string]::IsNullOrWhiteSpace($presentMode)) {
        $presentModes[$presentMode] = 1 + [int]($presentModes[$presentMode])
    }

    if ((Get-Bool $row $allowsTearingColumn) -eq $true) {
        $allowsTearingCount++
    }
    if ((Get-Bool $row $droppedColumn) -eq $true) {
        $droppedCount++
    }
    if ((Get-Bool $row $lateColumn) -eq $true) {
        $lateCount++
    }
    if (-not [string]::IsNullOrWhiteSpace($frameTypeColumn) -and (Test-GeneratedFrameType (Get-Value $row $frameTypeColumn))) {
        $generatedFrameCount++
    }
}

$appName = "N/A"
if ($appCounts.Count -gt 0) {
    $appName = ($appCounts.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 1).Key
}

$averageFps = if ($fpsValues.Count -gt 0) { ($fpsValues | Measure-Object -Average).Average } else { $null }
$averageFrameTime = if ($frameTimes.Count -gt 0) { ($frameTimes | Measure-Object -Average).Average } else { $null }
$derivedPresentedAppFps = if ($null -eq $averageFps -and $null -ne $averageFrameTime -and $averageFrameTime -gt 0) { 1000.0 / $averageFrameTime } else { $null }
$p95FrameTime = Get-Percentile ([double[]]$frameTimes.ToArray()) 0.95
$p99FrameTime = Get-Percentile ([double[]]$frameTimes.ToArray()) 0.99

$hasFrameTiming = -not [string]::IsNullOrWhiteSpace($frameTimeColumn) -or
    -not [string]::IsNullOrWhiteSpace($presentIntervalColumn) -or
    -not [string]::IsNullOrWhiteSpace($displayIntervalColumn)

if (-not [string]::IsNullOrWhiteSpace($frameTypeColumn) -and $generatedFrameCount -gt 0) {
    $classification = "VerifiedSignalPresent"
    $evidence = "FrameType explicitly reported $generatedFrameCount generated/interpolated sample(s)."
}
elseif (-not [string]::IsNullOrWhiteSpace($frameTypeColumn)) {
    $classification = "Inconclusive"
    $evidence = "FrameType column is present, but no generated/interpolated values were observed."
}
elseif (-not [string]::IsNullOrWhiteSpace($fpsColumn) -and $hasFrameTiming) {
    $classification = "HeuristicOnly"
    $evidence = "FPS and timing columns exist, but no dedicated generated-frame column exists. Ratios/cadence are not verified evidence."
}
else {
    $classification = "Unavailable"
    $evidence = "No dedicated generated-frame evidence column was found."
}

Write-Host "PresentMon offline capture summary" -ForegroundColor Cyan
Write-Host "=================================="
Write-Host "File: $resolvedPath"
Write-Host "Samples: $($rows.Count)"
Write-Host "Application: $appName"
if ($null -ne $averageFps) {
    Write-Host "Average FPS: $(Format-Number $averageFps)"
}
elseif ($null -ne $derivedPresentedAppFps) {
    Write-Host "Average Presented/App FPS (derived): $(Format-Number $derivedPresentedAppFps)"
    Write-Host "FPS Derivation Note: Derived from average frame time; may exclude driver-generated frames unless PresentMon exposes them explicitly."
}
else {
    Write-Host "Average FPS: N/A"
}
Write-Host "Average Frame Time: $(Format-Number $averageFrameTime) ms"
Write-Host "P95 Frame Time: $(Format-Number $p95FrameTime) ms"
Write-Host "P99 Frame Time: $(Format-Number $p99FrameTime) ms"
Write-Host "Allows Tearing Samples: $allowsTearingCount"
Write-Host "Dropped Samples: $droppedCount"
Write-Host "Late Samples: $lateCount"
Write-Host "Frame Generation Classification: $classification"
Write-Host "Frame Generation Evidence: $evidence"

Write-Host ""
Write-Host "Present Modes:"
if ($presentModes.Count -eq 0) {
    Write-Host "  N/A"
}
else {
    foreach ($entry in ($presentModes.GetEnumerator() | Sort-Object -Property Name)) {
        Write-Host "  $($entry.Name): $($entry.Value)"
    }
}

if ($ShowColumns) {
    Write-Host ""
    Write-Host "Columns:"
    foreach ($column in $columns) {
        Write-Host "  $column"
    }
}

Write-Host ""
Write-Host "Research boundary: this script only reads an existing CSV. It does not launch PresentMon, start ETW, or modify LightCrosshair runtime behavior."
