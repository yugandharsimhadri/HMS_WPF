<#
.SYNOPSIS
    Installs Sivaayaan HMS on this PC. Run by the setup package; not meant to
    be run on its own.

.DESCRIPTION
    Copies the single application file into place, makes the folders the
    application expects, and puts a shortcut on the desktop and in the Start
    menu.

    It never touches C:\HMS\DB. Installing over an existing copy replaces the
    program and leaves the clinic's data, backups and logs exactly as they are -
    which is the whole reason the database does not live beside the executable.

    Also cleans up after the product's old name (Twinkle Children's Hospital,
    exe TwinkleHMS.exe): the old exe file, its Start Menu / desktop shortcuts
    and its Add/Remove Programs entry, if any of those are still here from a
    copy installed before the rename. The database itself carries its own
    records over separately - see DbBootstrapper.CarryOverFromTwinkleDb - so
    nothing here touches C:\HMS\DB.
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

$source = Join-Path $PSScriptRoot "ShivayaanHMS.exe"
if (-not (Test-Path $source)) { throw "ShivayaanHMS.exe is not beside this script." }

# The program folder is replaced; everything else is left alone.
if (-not (Test-Path $To)) { New-Item -ItemType Directory -Path $To -Force | Out-Null }

$exe = Join-Path $To "ShivayaanHMS.exe"

# A running copy cannot be overwritten, and the person installing an update is
# very often the person who left it open. Checked under both names, in case
# what is open is actually the pre-rename build.
$running = Get-Process -Name ShivayaanHMS, TwinkleHMS -ErrorAction SilentlyContinue
if ($running) {
    throw "Sivaayaan HMS is open. Close it and run this again."
}

Copy-Item $source $exe -Force

# The old-named exe, if this PC still has one from before the rename - the
# copy above only ever writes the new name, so the old file would otherwise
# sit there for good.
$oldExe = Join-Path $To "TwinkleHMS.exe"
if (Test-Path $oldExe) { Remove-Item $oldExe -Force -ErrorAction SilentlyContinue }

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
    $link.Description = "Sivaayaan HMS - OPD and Pharmacy"
    $link.Save()
}

# The old-named shortcuts, if any - left behind the same way the old exe
# would be, since these are addressed by their old literal file names too.
$oldDesktopShortcut = Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Twinkle Children's Hospital.lnk"
if (Test-Path $oldDesktopShortcut) { Remove-Item $oldDesktopShortcut -Force -ErrorAction SilentlyContinue }

$oldStartMenu = Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Twinkle Children's Hospital"
if (Test-Path $oldStartMenu) { Remove-Item $oldStartMenu -Recurse -Force -ErrorAction SilentlyContinue }

New-Shortcut (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Sivaayaan HMS.lnk")

$startMenu = Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Sivaayaan HMS"
if (-not (Test-Path $startMenu)) { New-Item -ItemType Directory -Path $startMenu -Force | Out-Null }
New-Shortcut (Join-Path $startMenu "Sivaayaan HMS.lnk")

# So the clinic can find it in Settings and remove it the ordinary way.
#
# FileVersion, not ProductVersion: the build stamps the git commit onto the
# latter, so Add or remove programs would list the version as
# "1.0.0+afbc411d14c2744370be161af44c99975aa09701".
$version = (Get-Item $exe).VersionInfo.FileVersion
$oldKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TwinkleHMS"
$key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ShivayaanHMS"

# Removed rather than left behind: its own UninstallString would otherwise
# delete $To - the very folder the new copy was just installed into - if
# anyone ever runs it after the rename.
if (Test-Path $oldKey) { Remove-Item -Path $oldKey -Recurse -Force -ErrorAction SilentlyContinue }

New-Item -Path $key -Force | Out-Null
Set-ItemProperty -Path $key -Name DisplayName     -Value "Sivaayaan HMS"
Set-ItemProperty -Path $key -Name DisplayVersion  -Value $version
Set-ItemProperty -Path $key -Name Publisher       -Value "Sivayaan Technologies"
Set-ItemProperty -Path $key -Name InstallLocation -Value $To
Set-ItemProperty -Path $key -Name DisplayIcon     -Value $exe
Set-ItemProperty -Path $key -Name NoModify        -Value 1 -Type DWord
Set-ItemProperty -Path $key -Name NoRepair        -Value 1 -Type DWord
Set-ItemProperty -Path $key -Name UninstallString `
    -Value "powershell -ExecutionPolicy Bypass -Command `"& { Remove-Item '$To' -Recurse -Force; Remove-Item '$startMenu' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item '$key' -Recurse -Force }`""

Write-Host ""
Write-Host "Installed to $To" -ForegroundColor Green
Write-Host "Data stays at C:\HMS\DB - nothing there was touched."
