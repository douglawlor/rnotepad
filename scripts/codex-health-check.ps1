# Copyright (c) 2026 Doug Lawlor.
# SPDX-License-Identifier: MIT
# See LICENSE for the full license text.
[CmdletBinding()]
param(
  [string]$Workspace,
  [switch]$SkipSandboxExecution
)

$ErrorActionPreference = 'Stop'
if (-not $Workspace) { $Workspace = Split-Path -Parent $PSScriptRoot }
$workspacePath = [IO.Path]::GetFullPath($Workspace)
$codexPath = if ($env:HDESKTOP_CODEX) { $env:HDESKTOP_CODEX } else { Join-Path $env:LOCALAPPDATA 'Programs\OpenAI\Codex\bin\codex.exe' }
$configPath = Join-Path $workspacePath '.codex\config.toml'
$expectedRoot = $workspacePath.Replace('\', '\\')
$failures = [Collections.Generic.List[string]]::new()

function Check([string]$Name, [scriptblock]$Operation) {
  try {
    $value = & $Operation
    $suffix = if ($null -ne $value -and "$value") { ": $value" } else { '' }
    Write-Host "PASS  $Name$suffix"
  } catch {
    $failures.Add($Name)
    Write-Host "FAIL  ${Name}: $($_.Exception.Message)"
  }
}

Write-Host "Codex health check for $workspacePath"
Check 'workspace exists' { if (-not (Test-Path -LiteralPath $workspacePath -PathType Container)) { throw 'Workspace is not a directory' } }
Check 'Codex command exists' { if (-not (Test-Path -LiteralPath $codexPath -PathType Leaf)) { throw "Not found: $codexPath" }; $codexPath }
Check 'PowerShell launches' { $version = & powershell.exe -NoLogo -NoProfile -NonInteractive -Command '$PSVersionTable.PSVersion.ToString()'; if ($LASTEXITCODE -ne 0) { throw 'powershell.exe failed' }; $version }
Check 'Codex version' { $version = & $codexPath --version; if ($LASTEXITCODE -ne 0) { throw 'codex --version failed' }; $version }
Check 'command runner exists' {
  $releases = Join-Path $env:USERPROFILE '.codex\packages\standalone\releases'
  $runner = Get-ChildItem -LiteralPath $releases -Recurse -Filter 'codex-command-runner.exe' -File -ErrorAction Stop | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if (-not $runner) { throw 'codex-command-runner.exe was not found in the shared runtime' }
  $runner.FullName
}
Check 'project config exists' { if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw "Not found: $configPath" } }
Check 'sandbox mode configured' { $config = Get-Content -LiteralPath $configPath -Raw; if ($config -notmatch '(?m)^sandbox_mode\s*=\s*"workspace-write"\s*$') { throw 'sandbox_mode must be workspace-write' }; 'workspace-write' }
Check 'expected write root configured' { $config = Get-Content -LiteralPath $configPath -Raw; if ($config -notlike "*$expectedRoot*") { throw "Missing writable root: $workspacePath" }; $workspacePath }
Check 'recent sandbox log failures' {
  $codexHome = $env:CODEX_HOME
  if (-not $codexHome) { $codexHome = Join-Path $env:USERPROFILE '.codex' }
  $logPath = Join-Path $codexHome '.sandbox\sandbox.log'
  if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { return 'No sandbox log found' }
  $matches = Get-Content -LiteralPath $logPath -Tail 300 | Where-Object { $_ -match '(?i)createprocesswithlogonw|\berror\b|\bfailed\b|\bdenied\b' }
  if ($matches) { throw (($matches | Select-Object -Last 8) -join ' | ') }
  'No failure markers in last 300 log lines'
}

if (-not $SkipSandboxExecution) {
  Check 'restricted sandbox execution' {
    $result = & $codexPath sandbox --permission-profile :read-only -C $workspacePath -- powershell.exe -NoLogo -NoProfile -NonInteractive -Command "Write-Output 'sandbox health check passed'" 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($result -join [Environment]::NewLine) }
    if (($result -join "`n") -notmatch 'sandbox health check passed') { throw 'Sandbox command did not produce its expected output' }
    'read-only PowerShell command completed'
  }
} else {
  Write-Host 'SKIP  restricted sandbox execution'
}

if ($failures.Count) { exit 1 }
Write-Host 'PASS  Codex health check completed.'
