param(
    [int]$MaxCycles = 5,
    [string[]]$CheckCommands = @(
        "dotnet build .\ExtintorCrm.App\ExtintorCrm.App.csproj --no-restore",
        "dotnet build .\ExtintorCrm.App.SmokeTests\ExtintorCrm.App.SmokeTests.csproj --no-restore",
        "dotnet run --project .\ExtintorCrm.App.SmokeTests\ --no-build"
    ),
    [string[]]$FixCommands = @(),
    [switch]$RestoreBeforeLoop,
    [switch]$PromptBetweenCycles
)

$ErrorActionPreference = "Stop"

if ($MaxCycles -lt 1) {
    throw "MaxCycles deve ser maior ou igual a 1."
}

if (-not $CheckCommands -or $CheckCommands.Count -eq 0) {
    throw "Informe pelo menos um comando em -CheckCommands."
}

function Invoke-LoopCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [int]$Cycle,
        [int]$Step,
        [string]$Phase
    )

    Write-Host ""
    Write-Host "[$Phase $Cycle.$Step] $Command" -ForegroundColor Cyan

    $LASTEXITCODE = 0
    $startedAt = Get-Date

    try {
        $scriptBlock = [ScriptBlock]::Create($Command)
        & $scriptBlock
    }
    catch {
        Write-Host "Comando lancou excecao: $($_.Exception.Message)" -ForegroundColor Red
        return [pscustomobject]@{
            Command  = $Command
            ExitCode = 1
            Success  = $false
            Duration = (Get-Date) - $startedAt
        }
    }

    $exitCode = if ($LASTEXITCODE -is [int]) { $LASTEXITCODE } else { 0 }

    return [pscustomobject]@{
        Command  = $Command
        ExitCode = $exitCode
        Success  = ($exitCode -eq 0)
        Duration = (Get-Date) - $startedAt
    }
}

$root = Split-Path -Parent $PSScriptRoot
$cycleResults = [System.Collections.Generic.List[object]]::new()

Push-Location $root
try {
    Write-Host "== Ralph Loop local (Codex) ==" -ForegroundColor Green
    Write-Host "Projeto: $root"
    Write-Host "MaxCycles: $MaxCycles"

    if ($RestoreBeforeLoop) {
        $restoreResult = Invoke-LoopCommand -Command "dotnet restore .\ExtintorCrm.sln" -Cycle 0 -Step 0 -Phase "BOOT"
        if (-not $restoreResult.Success) {
            throw "Falha no restore inicial (exit code: $($restoreResult.ExitCode))."
        }
    }

    $completed = $false
    for ($cycle = 1; $cycle -le $MaxCycles; $cycle++) {
        Write-Host ""
        Write-Host ("=" * 56)
        Write-Host "CICLO $cycle/$MaxCycles" -ForegroundColor Yellow
        Write-Host ("=" * 56)

        $failedCommand = $null
        $stepIndex = 0

        foreach ($command in $CheckCommands) {
            $stepIndex++
            $result = Invoke-LoopCommand -Command $command -Cycle $cycle -Step $stepIndex -Phase "CHECK"
            if (-not $result.Success) {
                $failedCommand = $result
                break
            }
        }

        if (-not $failedCommand) {
            $cycleResults.Add([pscustomobject]@{
                    Cycle          = $cycle
                    Status         = "PASS"
                    FailedCommand  = ""
                    FailedExitCode = 0
                })
            Write-Host ""
            Write-Host "Ciclo $cycle aprovado. Loop concluido com sucesso." -ForegroundColor Green
            $completed = $true
            break
        }

        $cycleResults.Add([pscustomobject]@{
                Cycle          = $cycle
                Status         = "FAIL"
                FailedCommand  = $failedCommand.Command
                FailedExitCode = $failedCommand.ExitCode
            })

        Write-Host ""
        Write-Host "Ciclo $cycle falhou no comando:" -ForegroundColor Red
        Write-Host "  $($failedCommand.Command)"
        Write-Host "Exit code: $($failedCommand.ExitCode)"

        if ($FixCommands.Count -gt 0 -and $cycle -lt $MaxCycles) {
            Write-Host ""
            Write-Host "Executando etapa de correcao..." -ForegroundColor Magenta

            $fixStep = 0
            foreach ($fix in $FixCommands) {
                $fixStep++
                $fixResult = Invoke-LoopCommand -Command $fix -Cycle $cycle -Step $fixStep -Phase "FIX"
                if (-not $fixResult.Success) {
                    Write-Host "Comando de correcao falhou. O loop vai continuar para proximo ciclo." -ForegroundColor DarkYellow
                }
            }
        }
        elseif ($PromptBetweenCycles -and $cycle -lt $MaxCycles) {
            Write-Host ""
            [void](Read-Host "Ajuste no Codex e pressione ENTER para iniciar o proximo ciclo")
        }
    }

    Write-Host ""
    Write-Host "Resumo dos ciclos:" -ForegroundColor White
    foreach ($item in $cycleResults) {
        if ($item.Status -eq "PASS") {
            Write-Host (" - Ciclo {0}: PASS" -f $item.Cycle) -ForegroundColor Green
            continue
        }

        Write-Host (" - Ciclo {0}: FAIL (exit {1})" -f $item.Cycle, $item.FailedExitCode) -ForegroundColor Red
        Write-Host ("   Comando: {0}" -f $item.FailedCommand)
    }

    if (-not $completed) {
        throw "Ralph loop finalizado sem sucesso apos $MaxCycles ciclo(s)."
    }
}
finally {
    Pop-Location
}
