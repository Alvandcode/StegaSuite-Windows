param(
    [string]$InstallDir = "",
    [ValidateSet('Auto', 'Machine', 'CurrentUser')][string]$Scope = 'Auto',
    [switch]$NoDesktopShortcut,
    [switch]$Silent
)
# StegaSuite installer (no external tools needed).
# Default: per-machine under Program Files (needs admin). Without admin rights
# it falls back to a per-user install. Uninstall from Windows Settings > Apps.
$ErrorActionPreference = 'Stop'
$AppName = 'StegaSuite'
$Version = '2.0.0'
$ExeName = 'StegaSuite.exe'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Payload = Join-Path $ScriptDir $ExeName
if (!(Test-Path -LiteralPath $Payload)) { $Payload = Join-Path (Join-Path $ScriptDir 'Payload') $ExeName }

function Say([string]$m) { if (-not $Silent) { Write-Host $m } }
function IsAdmin {
    try {
        $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
        return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch { return $false }
}

if (!(Test-Path -LiteralPath $Payload)) { throw "فایل برنامه پیدا نشد: $Payload" }

if ($Scope -eq 'Auto') { $Scope = if (IsAdmin) { 'Machine' } else { 'CurrentUser' } }
if ($Scope -eq 'Machine' -and -not (IsAdmin)) {
    throw 'برای نصب سراسری باید as Administrator اجرا شود. روی Install.bat راست‌کلیک > Run as administrator.'
}
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = if ($Scope -eq 'Machine') { Join-Path $env:ProgramFiles $AppName } `
                  else { Join-Path $env:LocalAppData "Programs\$AppName" }
}

Say "نصب StegaSuite $Version در: $InstallDir"
Get-Process -Name 'StegaSuite' -ErrorAction SilentlyContinue | ForEach-Object {
    Say 'بستن نسخه در حال اجرا...'; Stop-Process -Id $_.Id -Force
}
Start-Sleep -Seconds 1
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -LiteralPath $Payload -Destination (Join-Path $InstallDir $ExeName) -Force
$ExePath = Join-Path $InstallDir $ExeName

$wsh = New-Object -ComObject WScript.Shell
if ($Scope -eq 'Machine') {
    $StartMenu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
    $Desktop = Join-Path $env:PUBLIC 'Desktop'
    $ArpRoot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
} else {
    $StartMenu = Join-Path $env:AppData 'Microsoft\Windows\Start Menu\Programs'
    $Desktop = [Environment]::GetFolderPath('Desktop')
    $ArpRoot = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
}
$lnk1 = Join-Path $StartMenu "$AppName.lnk"
$s = $wsh.CreateShortcut($lnk1); $s.TargetPath = $ExePath
$s.WorkingDirectory = $InstallDir; $s.IconLocation = "$ExePath,0"
$s.Description = 'StegaSuite - مخفی‌سازی امن فایل‌ها'; $s.Save()
Say "میانبر منوی استارت ساخته شد."
if (-not $NoDesktopShortcut -and (Test-Path -LiteralPath $Desktop)) {
    $lnk2 = Join-Path $Desktop "$AppName.lnk"
    $d = $wsh.CreateShortcut($lnk2); $d.TargetPath = $ExePath
    $d.WorkingDirectory = $InstallDir; $d.IconLocation = "$ExePath,0"
    $d.Description = 'StegaSuite - مخفی‌سازی امن فایل‌ها'; $d.Save()
    Say 'میانبر دسکتاپ ساخته شد.'
}

$UnPath = Join-Path $InstallDir 'Uninstall.ps1'
$UnContent = @'
param([switch]$Silent)
$ErrorActionPreference = 'SilentlyContinue'
$AppName = 'StegaSuite'
$InstallDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SCOPE = '__SCOPE__'
Get-Process -Name 'StegaSuite' -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force; Wait-Process -Id $_.Id -Timeout 10 -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1
$paths = @()
if ($SCOPE -eq 'Machine') {
    $paths += Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\StegaSuite.lnk'
    $paths += Join-Path $env:PUBLIC 'Desktop\StegaSuite.lnk'
    Remove-Item -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\StegaSuite' -Recurse -Force
} else {
    $paths += Join-Path $env:AppData 'Microsoft\Windows\Start Menu\Programs\StegaSuite.lnk'
    $paths += Join-Path ([Environment]::GetFolderPath('Desktop')) 'StegaSuite.lnk'
    Remove-Item -LiteralPath 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\StegaSuite' -Recurse -Force
}
foreach ($p in $paths) { if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force } }
for ($i = 0; $i -lt 5 -and (Test-Path -LiteralPath $InstallDir); $i++) {
    try { Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction Stop; break }
    catch { Start-Sleep -Seconds 1 }
}
if (-not $Silent) { Write-Host 'StegaSuite حذف شد.' }
'@
$UnContent = $UnContent.Replace('__SCOPE__', $Scope)
[IO.File]::WriteAllText($UnPath, $UnContent, (New-Object Text.UTF8Encoding($true)))

if ($Scope -eq 'Machine') {
    $UninstallString = "powershell -NoProfile -ExecutionPolicy Bypass -Command `"Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \`"$UnPath\`" -Silent' -Verb RunAs`""
} else {
    $UninstallString = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$UnPath`" -Silent"
}
$ArpKey = Join-Path $ArpRoot $AppName
New-Item -Path $ArpKey -Force | Out-Null
Set-ItemProperty -LiteralPath $ArpKey -Name 'DisplayName' -Value $AppName
Set-ItemProperty -LiteralPath $ArpKey -Name 'DisplayVersion' -Value $Version
Set-ItemProperty -LiteralPath $ArpKey -Name 'Publisher' -Value 'alvandcode'
Set-ItemProperty -LiteralPath $ArpKey -Name 'InstallLocation' -Value $InstallDir
Set-ItemProperty -LiteralPath $ArpKey -Name 'UninstallString' -Value $UninstallString
Set-ItemProperty -LiteralPath $ArpKey -Name 'DisplayIcon' -Value "$ExePath,0"
Set-ItemProperty -LiteralPath $ArpKey -Name 'NoModify' -Value 1 -Type DWord
Set-ItemProperty -LiteralPath $ArpKey -Name 'NoRepair' -Value 1 -Type DWord
Say 'ثبت در Add/Remove Programs انجام شد.'
Say "تمام شد! نصب در $InstallDir"
