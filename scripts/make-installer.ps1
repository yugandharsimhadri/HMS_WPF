<#
.SYNOPSIS
    Builds one setup file to carry to a clinic PC.

.DESCRIPTION
    Produces a single TwinkleHMSSetup.exe containing the whole application. The
    PC it is run on needs nothing installed first - not even the .NET runtime,
    which is inside the executable.

    ClickOnce cannot do this. Its setup.exe is only a bootstrapper and has to be
    copied along with the .application manifest and the Application Files
    folder beside it; carry the exe on its own and it fails on the far machine.
    So this uses IExpress, which ships with Windows, to wrap the application and
    a short install script into one self-extracting file.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\make-installer.ps1
#>

param(
    [string] $OutputDir = "C:\HMS\Setup"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $env:TEMP "twinkle-setup-staging"
$setup = Join-Path $OutputDir "TwinkleHMSSetup.exe"

Write-Host "1/3  Building the application as a single file ..." -ForegroundColor Cyan

# Self-contained so the clinic PC needs no runtime, single-file so there is one
# thing to wrap, compressed because 73 MB carries better than 180 MB.
dotnet publish (Join-Path $root "src\Pharma.App\Pharma.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $staging `
    --nologo -v q

if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$exe = Join-Path $staging "TwinkleHMS.exe"
if (-not (Test-Path $exe)) { throw "TwinkleHMS.exe was not produced." }

# Everything else the publish drops - symbols, the fonts QuestPDF ships as
# package content, appsettings.json - is not needed beside a single-file build
# and would only make the package bigger.
Get-ChildItem $staging -Exclude "TwinkleHMS.exe" | Remove-Item -Recurse -Force

Copy-Item (Join-Path $PSScriptRoot "installer\install.ps1") $staging
Copy-Item (Join-Path $PSScriptRoot "installer\install.cmd") $staging

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "     $size MB" -ForegroundColor DarkGray

Write-Host "2/3  Writing the package definition ..." -ForegroundColor Cyan

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

$sed = Join-Path $env:TEMP "twinkle.sed"

# IExpress reads an ini-style file rather than taking arguments. The three
# quiet-install lines are what let it be run unattended from a script.
@"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=Install Twinkle Children's Hospital on this PC?
DisplayLicense=
FinishMessage=Twinkle is installed. There is a shortcut on the desktop.
TargetName=$setup
FriendlyName=Twinkle Children's Hospital
AppLaunched=cmd /c install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0="TwinkleHMS.exe"
FILE1="install.cmd"
FILE2="install.ps1"
[SourceFiles]
SourceFiles0=$staging
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
"@ | Set-Content -Path $sed -Encoding ASCII

Write-Host "3/3  Packing (a 73 MB payload takes a minute or two) ..." -ForegroundColor Cyan

& "$env:WINDIR\System32\iexpress.exe" /N /Q $sed | Out-Null

if (-not (Test-Path $setup)) { throw "IExpress did not produce $setup." }

$setupSize = [math]::Round((Get-Item $setup).Length / 1MB, 1)

Write-Host ""
Write-Host "Setup file:  $setup  ($setupSize MB)" -ForegroundColor Green
Write-Host ""
Write-Host "Copy that one file to the clinic PC and run it. It needs nothing"
Write-Host "installed first. Windows will warn that the publisher is unknown,"
Write-Host "because the file is not code-signed - choose More info, then Run anyway."
