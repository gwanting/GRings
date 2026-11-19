$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $output -Force | Out-Null

$sources = @(
  (Join-Path $PSScriptRoot 'QuotaRingsProgram.cs'),
  (Join-Path $PSScriptRoot 'Shared\FullscreenVisibility.cs'),
  (Join-Path $PSScriptRoot 'Shared\RingArc.cs'),
  (Join-Path $PSScriptRoot 'Shared\RingClicks.cs'),
  (Join-Path $PSScriptRoot 'Codex\QuotaData.cs'),
  (Join-Path $PSScriptRoot 'Codex\LocalQuotaFeed.cs'),
  (Join-Path $PSScriptRoot 'Codex\QuotaCard.cs'),
  (Join-Path $PSScriptRoot 'Codex\DualQuotaBar.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\DeepSeekClient.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\PricingPeriod.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\BalanceAlert.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\BalanceReference.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\BalanceAmountRenderer.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\PlatformUsage.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\PanelAppearance.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\PanelMotion.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\PanelBackdrop.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\FrostedContextMenu.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\BalancePanel.cs'),
  (Join-Path $PSScriptRoot 'DeepSeek\DeepSeekBalance.cs'),
  (Join-Path $PSScriptRoot 'Antigravity\AntigravityData.cs'),
  (Join-Path $PSScriptRoot 'Antigravity\AntigravityCard.cs'),
  (Join-Path $PSScriptRoot 'Antigravity\AntigravityQuotaBar.cs')
)

& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' `
  /nologo /target:winexe /main:QuotaRings.Program /optimize+ /platform:x64 `
  "/win32icon:$PSScriptRoot\QuotaRings.ico" `
  /reference:System.Windows.Forms.dll `
  /reference:System.Drawing.dll `
  /reference:System.Web.Extensions.dll `
  /reference:System.Core.dll `
  /reference:System.Security.dll `
  /reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll `
  /reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll `
  /reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll `
  "/out:$output\GRings.exe" `
  @sources

if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "Built: $output\GRings.exe"
