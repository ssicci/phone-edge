<#
  CHANGELOG.md 에서 한 버전의 항목 본문(제목 줄 제외)을 꺼내 출력한다. 항목이 없으면 실패한다.
  사용법: pwsh scripts/Get-ChangelogEntry.ps1 -Version 0.1.0 [-OutFile RELEASE_NOTES.md]
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$OutFile,
    [string]$ChangelogPath
)

$ErrorActionPreference = 'Stop'
if (-not $ChangelogPath) { $ChangelogPath = Join-Path (Join-Path $PSScriptRoot '..') 'CHANGELOG.md' }
$lines = Get-Content $ChangelogPath -Encoding UTF8
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match ('^##\s*\[' + [regex]::Escape($Version) + '\]')) { $start = $i; break }
}
if ($start -lt 0) { throw "CHANGELOG.md 에 '## [$Version] - 날짜' 항목이 없습니다." }

$body = @()
for ($i = $start + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^##\s') { break }
    $body += $lines[$i]
}
$text = ($body -join "`n").Trim()
if (-not $text) { throw "CHANGELOG.md 의 [$Version] 항목이 비어 있습니다." }

if ($OutFile) { [IO.File]::WriteAllText($OutFile, $text + "`n", (New-Object Text.UTF8Encoding($false))) }
else { $text }
