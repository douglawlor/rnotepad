# Copyright (c) 2026 Doug Lawlor.
# SPDX-License-Identifier: MIT
# See LICENSE for the full license text.
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$output = Join-Path $PSScriptRoot 'tests\output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:exe /out:"$output\ArgumentRecorder.exe" "$PSScriptRoot\tests\ArgumentRecorder.cs"
if ($LASTEXITCODE -ne 0) { throw 'Argument recorder build failed.' }
& $compiler /nologo /target:exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /out:"$output\Tests.exe" "$PSScriptRoot\tests\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& "$output\Tests.exe" "$PSScriptRoot\bin\rnotepad.exe" "$output\ArgumentRecorder.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
