<#
.SYNOPSIS
    Publishes Twinkle for a clinic PC and puts a shortcut on the desktop.

.DESCRIPTION
    Produces a self-contained folder that runs without the .NET runtime being
    installed, then creates a desktop shortcut carrying the application icon.

    The database is NOT part of the published output. It lives at C:\HMS\DB and
    is left exactly as it is, so republishing a new version never touches the
    clinic's data.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
    powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -To "D:\Twinkle"
#>

param(
    [string] $To = "C:\HMS\App",
    [switch] $NoShortcut
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "Publishing to $To ..." -ForegroundColor Cyan

dotnet publish (Join-Path $root "src\Pharma.App\Pharma.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $To

if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$exe = Join-Path $To "TwinkleHMS.exe"
if (-not (Test-Path $exe)) { throw "TwinkleHMS.exe was not produced." }

# The folders the application expects, created now so the first launch on a
# fresh PC does not have to.
foreach ($dir in @("C:\HMS\DB", "C:\HMS\DBBackup", "C:\HMS\Logs")) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

if (-not $NoShortcut) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $link = Join-Path $desktop "Twinkle Children's Hospital.lnk"

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($link)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $To
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = "Twinkle Children's Hospital — OPD and Pharmacy"
    $shortcut.Save()

    Write-Host "Desktop shortcut: $link" -ForegroundColor Green
}

Write-Host ""
Write-Host "Published.       $exe"          -ForegroundColor Green
Write-Host "Database stays.  C:\HMS\DB\twinkle.db"
Write-Host "Backups.         C:\HMS\DBBackup"
Write-Host "Logs.            C:\HMS\Logs"
