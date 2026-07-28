<#
.SYNOPSIS
    Installs Twinkle on this PC. Run by the setup package; not meant to be run
    on its own.

.DESCRIPTION
    Copies the single application file into place, makes the folders the
    application expects, and puts a shortcut on the desktop and in the Start
    menu.

    It never touches C:\HMS\DB. Installing over an existing copy replaces the
    program and leaves the clinic's data, backups and logs exactly as they are -
    which is the whole reason the database does not live beside the executable.
#>

param(
    [string] $To = "C:\HMS\App"
)

$ErrorActionPreference = "Stop"

# C:\ needs an administrator on a fresh machine. Ask for it once, here, rather
# than failing halfway through with half the files copied.
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell -Verb RunAs -Wait -ArgumentList @(
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`"",
        "-To", "`"$To`""
    )
    exit $LASTEXITCODE
}

$source = Join-Path $PSScriptRoot "TwinkleHMS.exe"
if (-not (Test-Path $source)) { throw "TwinkleHMS.exe is not beside this script." }

# The program folder is replaced; everything else is left alone.
if (-not (Test-Path $To)) { New-Item -ItemType Directory -Path $To -Force | Out-Null }

$exe = Join-Path $To "TwinkleHMS.exe"

# A running copy cannot be overwritten, and the person installing an update is
# very often the person who left it open.
$running = Get-Process -Name TwinkleHMS -ErrorAction SilentlyContinue
if ($running) {
    throw "Twinkle is open. Close it and run this again."
}

Copy-Item $source $exe -Force

# Made now so the first launch on a fresh PC does not have to, and so a backup
# has somewhere to go on day one.
foreach ($dir in @("C:\HMS\DB", "C:\HMS\DBBackup", "C:\HMS\Logs")) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

function New-Shortcut([string] $Path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $To
    $link.IconLocation = "$exe,0"
    $link.Description = "Twinkle Children's Hospital - OPD and Pharmacy"
    $link.Save()
}

New-Shortcut (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Twinkle Children's Hospital.lnk")

$startMenu = Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Twinkle Children's Hospital"
if (-not (Test-Path $startMenu)) { New-Item -ItemType Directory -Path $startMenu -Force | Out-Null }
New-Shortcut (Join-Path $startMenu "Twinkle Children's Hospital.lnk")

# So the clinic can find it in Settings and remove it the ordinary way.
#
# FileVersion, not ProductVersion: the build stamps the git commit onto the
# latter, so Add or remove programs would list the version as
# "1.0.0+afbc411d14c2744370be161af44c99975aa09701".
$version = (Get-Item $exe).VersionInfo.FileVersion
$key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TwinkleHMS"

New-Item -Path $key -Force | Out-Null
Set-ItemProperty -Path $key -Name DisplayName     -Value "Twinkle Children's Hospital"
Set-ItemProperty -Path $key -Name DisplayVersion  -Value $version
Set-ItemProperty -Path $key -Name Publisher       -Value "Sivayaan Technologies"
Set-ItemProperty -Path $key -Name InstallLocation -Value $To
Set-ItemProperty -Path $key -Name DisplayIcon     -Value $exe
Set-ItemProperty -Path $key -Name NoModify        -Value 1 -Type DWord
Set-ItemProperty -Path $key -Name NoRepair        -Value 1 -Type DWord
Set-ItemProperty -Path $key -Name UninstallString `
    -Value "powershell -ExecutionPolicy Bypass -Command `"& { Remove-Item '$To' -Recurse -Force; Remove-Item '$startMenu' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TwinkleHMS' -Recurse -Force }`""

Write-Host ""
Write-Host "Installed to $To" -ForegroundColor Green
Write-Host "Data stays at C:\HMS\DB - nothing there was touched."
