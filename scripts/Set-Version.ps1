param([Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$path=Join-Path $root 'src\VersionInfo.cs'
$text=[IO.File]::ReadAllText($path)
$text=[regex]::Replace($text,'Version = "\d+\.\d+\.\d+"','Version = "'+$Version+'"')
[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
$manifest=Join-Path $root 'package\manifest.json'
$data=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
$data.version_number=$Version
$data | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding UTF8
Write-Host "Version $Version gesetzt. CHANGELOG bearbeiten, dann Test.cmd und Release.cmd ausfuehren."
