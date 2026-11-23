<#
  새 버전 릴리스 준비. Directory.Build.props 의 버전을 올리고 CHANGELOG 항목을 확인한다.
  git 작업은 하지 않고, 실행할 git 명령만 출력한다.
  사용법: pwsh scripts/release.ps1 0.1.1
#>
[CmdletBinding()]
param([Parameter(Mandatory, Position = 0)][string]$Version)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$props = Join-Path $root 'Directory.Build.props'

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "버전은 X.Y.Z 형식이어야 합니다: $Version" }

$xml = Get-Content $props -Raw -Encoding UTF8
if ($xml -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') { throw "Directory.Build.props 에서 <Version> 을 찾지 못했습니다." }
$current = [version]$Matches[1]
if ([version]$Version -lt $current) { throw "새 버전($Version)은 현재 버전($current)보다 높아야 합니다." }

# "## [Unreleased]" 항목이 있고 이 버전의 항목이 아직 없으면, 제목을 이 버전과 오늘 날짜로 바꾼다.
$changelogPath = Join-Path $root 'CHANGELOG.md'
$changelog = Get-Content $changelogPath -Raw -Encoding UTF8
$hasVersion = $changelog -match ('(?m)^##\s*\[' + [regex]::Escape($Version) + '\]')
if (-not $hasVersion -and $changelog -match '(?m)^##\s*\[Unreleased\][^\r\n]*') {
    $date = Get-Date -Format 'yyyy-MM-dd'
    $changelog = [regex]::Replace($changelog, '(?m)^##\s*\[Unreleased\][^\r\n]*', "## [$Version] - $date", 1)
    [IO.File]::WriteAllText($changelogPath, $changelog, (New-Object Text.UTF8Encoding($false)))
    Write-Host "CHANGELOG.md: [Unreleased] -> [$Version] - $date"
}

# CHANGELOG 항목 확인 (없거나 비어 있으면 여기서 실패한다)
$notes =& (Join-Path $PSScriptRoot 'Get-ChangelogEntry.ps1') -Version $Version -ChangelogPath (Join-Path $root 'CHANGELOG.md')
if ($notes -notmatch '\S') { throw "CHANGELOG 항목이 비어 있습니다." }

$new = [regex]::Replace($xml, '<Version>\d+\.\d+\.\d+</Version>', "<Version>$Version</Version>")
[IO.File]::WriteAllText($props, $new, (New-Object Text.UTF8Encoding($false)))

Write-Host "버전 $current -> $Version, CHANGELOG 항목 확인됨:"
Write-Host "----------------------------------------"
Write-Host $notes
Write-Host "----------------------------------------"
Write-Host ""
Write-Host "관리 세션이 실행할 명령 (이 스크립트는 git 을 실행하지 않습니다):"
Write-Host "  git add Directory.Build.props CHANGELOG.md"
Write-Host "  git commit -m `"릴리스 v$Version`""
Write-Host "  git push origin main"
Write-Host "  git tag v$Version"
Write-Host "  git push origin v$Version"
