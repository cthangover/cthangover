#Requires -Version 5.1

param(
    [switch]$Loop,
    [int]$Interval = 3,
    [switch]$VerboseOutput
)

function Get-GpuStats {
    $gpuQuery = "index,name,temperature.gpu,fan.speed,utilization.gpu,utilization.memory,power.draw,power.limit,clocks.gr,clocks.mem,pstate,memory.used,memory.total"
    try {
        $raw = & "nvidia-smi" --query-gpu=$gpuQuery --format=csv,noheader,nounits 2>$null
        if ($LASTEXITCODE -ne 0) { throw "nvidia-smi failed" }
    } catch {
        Write-Warning "nvidia-smi not available or failed. Install NVIDIA driver."
        return
    }

    $gpuData = @()
    foreach ($line in $raw) {
        $cols = $line -split ',\s*'
        if ($cols.Count -lt 13) { continue }

        $temp = [double]$cols[2]
        $fan  = [double]$cols[3]
        $util = [double]$cols[4]
        $memUtil = [double]$cols[5]
        $power = [double]$cols[6]
        $powerLimit = [double]$cols[7]

        # Color-coded temp
        if ($temp -ge 80)       { $tempColor = "Red" }
        elseif ($temp -ge 70)   { $tempColor = "Yellow" }
        elseif ($temp -ge 60)   { $tempColor = "DarkYellow" }
        else                    { $tempColor = "Green" }

        # Color-coded utilization
        if ($util -ge 80)       { $utilColor = "Red" }
        elseif ($util -ge 50)   { $utilColor = "Yellow" }
        else                    { $utilColor = "Green" }

        $gpuData += [PSCustomObject]@{
            Index        = $cols[0]
            Name         = $cols[1]
            Temp         = $temp
            TempColor    = $tempColor
            FanPercent   = $fan
            GPULoad      = $util
            GPULoadColor = $utilColor
            MemLoad      = $memUtil
            PowerW       = $power
            PowerLimitW  = $powerLimit
            CoreMHz      = $cols[8]
            MemMHz       = $cols[9]
            PState       = $cols[10]
            MemUsedMB    = [math]::Round([double]$cols[11], 0)
            MemTotalMB   = [math]::Round([double]$cols[12], 0)
        }
    }
    return $gpuData
}

function Show-GpuTable {
    Clear-Host
    Write-Host "`n=== GPU Monitor $(Get-Date -Format 'HH:mm:ss') ===`n" -ForegroundColor Cyan

    $gpuList = Get-GpuStats
    if (-not $gpuList) { return }

    foreach ($gpu in $gpuList) {
        # Header
        Write-Host " GPU $($gpu.Index): $($gpu.Name)  |  P-State: $($gpu.PState)" -ForegroundColor White

        # Temperature bar
        $tempBar = ''
        $tempPct = [math]::Min(100, ($gpu.Temp / 95) * 40)
        for ($i = 0; $i -lt $tempPct; $i++) { $tempBar += '#' }
        Write-Host " Temp      : " -NoNewline
        Write-Host ("{0,3} C  [{1}]" -f $gpu.Temp, $tempBar) -ForegroundColor $gpu.TempColor

        # Fan speed
        $fanColor = if ($gpu.FanPercent -ge 70) { "Yellow" } else { "Green" }
        Write-Host " Fan       : " -NoNewline
        Write-Host ("{0,3} %" -f $gpu.FanPercent) -ForegroundColor $fanColor

        # GPU load
        Write-Host " GPU Load  : " -NoNewline
        Write-Host ("{0,3} %   [{1}]" -f $gpu.GPULoad, ('#' * [math]::Round($gpu.GPULoad / 5))) -ForegroundColor $gpu.GPULoadColor

        # Memory load
        Write-Host " Mem Load  : " -NoNewline
        Write-Host ("{0,3} %  [{1}/{2} MB]" -f $gpu.MemLoad, $gpu.MemUsedMB, $gpu.MemTotalMB) -ForegroundColor $(if ($gpu.MemLoad -ge 80) { "Yellow" } else { "Green" })

        # Power
        $powerColor = if ($gpu.PowerW -ge ($gpu.PowerLimitW * 0.7)) { "Yellow" } else { "Green" }
        Write-Host " Power     : " -NoNewline
        Write-Host ("{0,5} W / {1} W" -f $gpu.PowerW, $gpu.PowerLimitW) -ForegroundColor $powerColor

        # Clocks
        Write-Host (" Clocks    : Core {0} MHz  |  Mem {1} MHz" -f $gpu.CoreMHz, $gpu.MemMHz)

        Write-Host ""
    }

    # Top GPU memory consumers
    Write-Host "--- GPU Processes ---" -ForegroundColor Cyan
    try {
        $smi = & "nvidia-smi" 2>$null | Out-String
        $inProcess = $false
        $procData = @()
        foreach ($line in ($smi -split "\r?\n")) {
            if ($line -match "Processes:") { $inProcess = $true; continue }
            if (-not $inProcess) { continue }
            if ($line -match '^\|\s*(\d+)\s+N/A\s+N/A\s+(\d+)\s+(\S+)\s+(.+?)\s+(N/A|\d+MiB)\s+\|') {
                $g = $matches[1]
                $p = [int]$matches[2]
                $t = $matches[3]
                $n = $matches[4].Trim()
                $m = $matches[5]
                $mb = 0
                if ($m -match '^(\d+)MiB') { $mb = [int]$Matches[1] }
                $procData += [PSCustomObject]@{ GPU = $g; PID = $p; Type = $t; Name = $n; MemMB = $mb }
            }
        }
        if ($procData.Count -gt 0) {
            $procData | Sort-Object MemMB -Descending | Select-Object -First 12 | Format-Table -AutoSize
        } else {
            Write-Host "  (no processes detected)" -ForegroundColor DarkGray
        }
    } catch {
        Write-Host "  (unable to query GPU process list: $($_.Exception.Message))" -ForegroundColor DarkGray
    }

    # CPU snapshot
    Write-Host "--- Top CPU Consumers ---" -ForegroundColor Cyan
    Get-Process | Sort-Object CPU -Descending | Select-Object -First 8 @(
        @{N='Process';E={$_.ProcessName}}
        @{N='PID';E={$_.Id}}
        @{N='CPU (s)';E={[math]::Round($_.CPU,1)}}
        @{N='RAM (MB)';E={[math]::Round($_.WorkingSet64/1MB,0)}}
    ) | Format-Table -AutoSize
}

# ---- Main ----
if ($Loop) {
    Write-Host ("GPU Monitor - refresh every {0}s. Press Ctrl+C to exit." -f $Interval) -ForegroundColor DarkGray
    while ($true) {
        Show-GpuTable
        Start-Sleep -Seconds $Interval
    }
} else {
    Show-GpuTable
}

