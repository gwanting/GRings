$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' `
  /nologo /target:winexe /main:AntigravityQuotaBar.Program /optimize+ /platform:x64 `
  /reference:System.Windows.Forms.dll `
  /reference:System.Drawing.dll `
  /reference:System.Web.Extensions.dll `
  /reference:System.Core.dll `
  "/out:$output\AntigravityQuotaBar.exe" `
  (Join-Path $PSScriptRoot 'AntigravityData.cs') `
  (Join-Path $PSScriptRoot 'AntigravityCard.cs') `
  (Join-Path $PSScriptRoot 'AntigravityQuotaBar.cs') `
  (Join-Path $PSScriptRoot 'Shared\FullscreenVisibility.cs') `
  (Join-Path $PSScriptRoot 'Shared\RingArc.cs') `
  (Join-Path $PSScriptRoot 'Shared\RingClicks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "Built: $output\AntigravityQuotaBar.exe"
