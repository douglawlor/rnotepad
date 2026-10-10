# Copyright (c) 2026 Doug Lawlor.
# SPDX-License-Identifier: MIT
# See LICENSE for the full license text.
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw 'Install .NET Framework 4.8 to build RNotepad.' }
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /out:"$output\rnotepad.exe" /win32manifest:"$PSScriptRoot\app.manifest" /resource:"$PSScriptRoot\LICENSE",RNotepad.LICENSE "$PSScriptRoot\src\RNotepad.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $output 'LICENSE') -Force
Write-Output "Built $output\rnotepad.exe"
