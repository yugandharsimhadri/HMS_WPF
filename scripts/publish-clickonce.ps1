<#
.SYNOPSIS
    Publishes the ClickOnce deployment, and zips it so there is one file to carry.

.DESCRIPTION
    ClickOnce produces three things that have to stay together: setup.exe, the
    ShivayaanHMS.application manifest, and an Application Files folder. The
    setup.exe is only a bootstrapper - carried on its own it fails on the far
    machine, because there is nothing beside it to install.

    So the publish is zipped. One file to send; the person at the other end
    unzips it and runs setup.exe.

    What ClickOnce gives you that the IExpress setup file does not:

      - It installs per user, so it needs no administrator.
      - Point UpdateEnabled at a shared folder and every PC picks up new
        versions on its own. That is the real reason to use it.

    What it costs:

      - The .NET Desktop Runtime has to be installed on the target. The
        bootstrapper offers to fetch it, which needs internet on that PC the
        first time.
      - Three things to keep together instead of one.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\publish-clickonce.ps1
    powershell -ExecutionPolicy Bypass -File scripts\publish-clickonce.ps1 -NoZip
#>

param(
    [string] $OutputDir = "C:\HMS\Setup",
    [switch] $NoZip
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\Pharma.App\Pharma.App.csproj"

# The SDK's own MSBuild cannot do this. GenerateBootstrapper, the task that
# builds setup.exe, is .NET Framework only:
#   error MSB4803: The task "GenerateBootstrapper" is not supported on the
#   .NET Core version of MSBuild.
# So Visual Studio has to be installed on the build machine, and this finds it.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if (-not (Test-Path $vswhere)) {
    throw "Visual Studio was not found. ClickOnce needs its MSBuild; the .NET SDK on its own cannot publish one. Use scripts\make-installer.ps1 instead."
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1

if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw "MSBuild was not found in the Visual Studio installation."
}

Write-Host "1/2  Publishing ..." -ForegroundColor Cyan
Write-Host "     $msbuild" -ForegroundColor DarkGray

& $msbuild $project -t:Publish -p:PublishProfile=ClickOnceProfile -p:Configuration=Release -v:m -nologo

if ($LASTEXITCODE -ne 0) { throw "ClickOnce publish failed." }

# PublishUrl in ClickOnceProfile.pubxml. Change it there, not here.
$publish = Join-Path $root "src\Pharma.App\bin\publish"

$setup = Join-Path $publish "setup.exe"
$manifest = Join-Path $publish "ShivayaanHMS.application"

if (-not (Test-Path $setup))    { throw "setup.exe was not produced in $publish." }
if (-not (Test-Path $manifest)) { throw "ShivayaanHMS.application was not produced in $publish." }

Write-Host ""
Write-Host "Published to $publish" -ForegroundColor Green
Get-ChildItem $publish | ForEach-Object { Write-Host ("     " + $_.Name) }

if ($NoZip) {
    Write-Host ""
    Write-Host "All of the above has to travel together. setup.exe alone will fail." -ForegroundColor Yellow
    return
}

Write-Host ""
Write-Host "2/2  Zipping ..." -ForegroundColor Cyan

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

$zip = Join-Path $OutputDir "ShivayaanHMS-ClickOnce.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

# Application Files keeps every revision ever published - each one a full copy
# of the application, and each publish adds another. That is right for a shared
# folder, where a PC part-way through an update still needs the version it is
# coming from; it is dead weight in a zip somebody carries once. Ship only the
# version the manifest actually points at.
$version = ([xml](Get-Content $manifest)).assembly.assemblyIdentity.version
if (-not $version) { throw "Could not read the version out of $manifest." }

$current = "ShivayaanHMS_" + ($version -replace '\.', '_')
$currentPath = Join-Path $publish "Application Files\$current"

if (-not (Test-Path $currentPath)) { throw "The manifest names version $version, but $currentPath is not there." }

$staging = Join-Path $env:TEMP "shivayaanhms-clickonce-zip"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $staging "Application Files") -Force | Out-Null

Copy-Item $setup $staging
Copy-Item $manifest $staging
Copy-Item $currentPath (Join-Path $staging "Application Files") -Recurse

Write-Host "     version $version" -ForegroundColor DarkGray

$others = (Get-ChildItem (Join-Path $publish "Application Files") -Directory).Count - 1
if ($others -gt 0) {
    Write-Host "     $others older version(s) in the publish folder left out of the zip" -ForegroundColor DarkGray
}

Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip
Remove-Item $staging -Recurse -Force

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)

Write-Host ""
Write-Host "One file to send:  $zip  ($mb MB)" -ForegroundColor Green
Write-Host ""
Write-Host "At the other end: unzip it somewhere, then run setup.exe from the"
Write-Host "unzipped folder. Running setup.exe from inside the zip viewer does"
Write-Host "not work - the files it needs are still zipped."
