<#
  README 에 쓰는 이미지(docs/images/*.png)를 다시 만든다.
  앱을 바꾼 뒤 이 스크립트를 실행하면 캡처가 갱신된다. 앱 창만 따로 그리므로 실제 화면은 찍히지 않는다.
  docs/images/*.svg (동작 원리 그림)는 손으로 그린 파일이라 이 스크립트가 만들지 않는다.
  사용법: pwsh scripts/make-docs-images.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj = Join-Path $root 'src/Twinput.App/Twinput.App.csproj'
$out = Join-Path $root 'docs/images'

dotnet build $proj -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

$exe = Get-ChildItem (Join-Path $root 'src/Twinput.App/bin/Debug') -Recurse -Filter Twinput.exe | Select-Object -First 1
& $exe.FullName --make-docs $out | Out-Host
Start-Sleep -Seconds 1   # WinExe 는 비동기로 끝나므로 파일이 다 써질 시간을 준다
Get-ChildItem $out -Recurse -Filter *.png | Select-Object FullName, @{n = 'KB'; e = { [math]::Round($_.Length / 1KB) } }
