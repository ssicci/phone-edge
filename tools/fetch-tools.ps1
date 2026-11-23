<#
  외부 도구(adb, scrcpy 서버)와 라이선스 파일을 tools/versions.json 대로 받아 해시를 검증한다.
  해시가 다르면 파일을 쓰지 않고 실패한다.
  사용법: pwsh tools/fetch-tools.ps1 [-ToolsDir <받을 폴더>]
#>
[CmdletBinding()]
param([string]$ToolsDir)

$ErrorActionPreference = 'Stop'
if (-not $ToolsDir) { $ToolsDir = $PSScriptRoot }
$cfg = Get-Content (Join-Path $PSScriptRoot 'versions.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$dl = Join-Path $ToolsDir 'dl'
New-Item -ItemType Directory -Force $dl, (Join-Path $ToolsDir 'platform-tools'), (Join-Path $ToolsDir 'dl/licenses') | Out-Null

function Get-Verified([string]$Url, [string]$Name, [string]$Sha256, [string]$Sha1) {
    $path = Join-Path $dl $Name
    if (-not (Test-Path $path) -or (Get-FileHash $path -Algorithm SHA256).Hash -ne $Sha256.ToUpper()) {
        Write-Host "받는 중: $Url"
        Invoke-WebRequest -Uri $Url -OutFile "$path.part" -UseBasicParsing
        Move-Item "$path.part" $path -Force
    }
    $actual = (Get-FileHash $path -Algorithm SHA256).Hash
    if ($actual -ne $Sha256.ToUpper()) { Remove-Item $path -Force; throw "SHA-256 불일치: $Name (기대 $Sha256, 실제 $actual)" }
    if ($Sha1) {
        $a1 = (Get-FileHash $path -Algorithm SHA1).Hash
        if ($a1 -ne $Sha1.ToUpper()) { Remove-Item $path -Force; throw "SHA-1 불일치: $Name (기대 $Sha1, 실제 $a1)" }
    }
    Write-Host "확인됨: $Name"
    return $path
}

# platform-tools (adb)
$pt = $cfg.platformTools
$zip = Get-Verified $pt.url "platform-tools-$($pt.version)-win.zip" $pt.sha256 $pt.sha1
$tmp = Join-Path $dl "pt-extract"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
Expand-Archive $zip $tmp
foreach ($f in $pt.extract) {
    Copy-Item (Join-Path $tmp "platform-tools/$f") (Join-Path $ToolsDir "platform-tools/$f") -Force
}
Remove-Item $tmp -Recurse -Force

# scrcpy 서버 (파일명에 버전을 붙이지 않고 저장한다. AdbPhoneLinkOptions.ServerVersion 과 같아야 한다)
$sv = $cfg.scrcpyServer
$srv = Get-Verified $sv.url "scrcpy-server-v$($sv.version)" $sv.sha256 $null
Copy-Item $srv (Join-Path $ToolsDir 'scrcpy-server') -Force

# 라이선스
$lic = Get-Verified $cfg.scrcpyLicense.url "scrcpy-LICENSE-v$($sv.version)" $cfg.scrcpyLicense.sha256 $null
Copy-Item $lic (Join-Path $ToolsDir 'dl/licenses/scrcpy-LICENSE.txt') -Force
Copy-Item (Join-Path $ToolsDir 'platform-tools/NOTICE.txt') (Join-Path $ToolsDir 'dl/licenses/platform-tools-NOTICE.txt') -Force

Write-Host "완료: platform-tools $($pt.version), scrcpy-server $($sv.version)"
