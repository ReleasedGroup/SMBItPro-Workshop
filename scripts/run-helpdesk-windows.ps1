[CmdletBinding()]
param(
    [switch]$ForceBootstrap,
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$StopExisting,
    [switch]$SkipBrowser
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param(
        [Parameter(Mandatory)]
        [string]$Message
    )

    Write-Host "[helpdesk] $Message" -ForegroundColor Cyan
}

function Ensure-Command {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    if (-not (Get-Command -Name $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' is not installed or not in PATH."
    }
}

function Get-PowerShellExecutable {
    if (Get-Command -Name "pwsh.exe" -ErrorAction SilentlyContinue) {
        return "pwsh.exe"
    }

    if (Get-Command -Name "powershell.exe" -ErrorAction SilentlyContinue) {
        return "powershell.exe"
    }

    throw "Unable to find pwsh.exe or powershell.exe."
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Stop-ExistingHelpdeskProcesses {
    $dotnetProcesses = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
        Where-Object {
            $_.CommandLine -and $_.CommandLine -match "Helpdesk\.Light\.(Api|Worker|Web)\.csproj"
        }

    foreach ($process in $dotnetProcesses) {
        try {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
            Write-Step "Stopped existing process $($process.ProcessId): $($process.CommandLine)"
        }
        catch {
            Write-Warning "Failed to stop process $($process.ProcessId): $($_.Exception.Message)"
        }
    }
}

function Start-ServiceWindow {
    param(
        [Parameter(Mandatory)]
        [string]$ShellExe,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory)]
        [string]$WindowTitle,
        [Parameter(Mandatory)]
        [string]$RunCommand
    )

    $escapedWorkingDirectory = $WorkingDirectory.Replace('"', '`"')
    $escapedTitle = $WindowTitle.Replace("'", "''")
    $launcherCommand = "`$host.UI.RawUI.WindowTitle = '$escapedTitle'; Set-Location -LiteralPath `"$escapedWorkingDirectory`"; $RunCommand"

    Start-Process `
        -FilePath $ShellExe `
        -WorkingDirectory $WorkingDirectory `
        -ArgumentList @("-NoExit", "-Command", $launcherCommand) `
        | Out-Null
}

if ($env:OS -ne "Windows_NT") {
    throw "This script is intended for Windows hosts only."
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path
Set-Location -LiteralPath $repoRoot

$solutionPath = Join-Path $repoRoot "Helpdesk.Light.slnx"
$apiProjectPath = Join-Path $repoRoot "src\Helpdesk.Light.Api\Helpdesk.Light.Api.csproj"
$workerProjectPath = Join-Path $repoRoot "src\Helpdesk.Light.Worker\Helpdesk.Light.Worker.csproj"
$webProjectPath = Join-Path $repoRoot "src\Helpdesk.Light.Web\Helpdesk.Light.Web.csproj"

if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "Could not find Helpdesk.Light.slnx under '$repoRoot'. Run this script from the cloned repository."
}

Ensure-Command -Name "dotnet"

$dotnetSdks = dotnet --list-sdks
if ($dotnetSdks -notmatch "^10\.0\.") {
    Write-Warning "No .NET 10 SDK detected in 'dotnet --list-sdks'. The project targets .NET 10."
}

$webDevelopmentConfigPath = Join-Path $repoRoot "src\Helpdesk.Light.Web\wwwroot\appsettings.Development.json"
$expectedWebDevelopmentConfig = @'
{
  "ApiBaseUrl": "http://localhost:5283/"
}
'@

$existingWebDevelopmentConfig = ""
if (Test-Path -LiteralPath $webDevelopmentConfigPath) {
    $existingWebDevelopmentConfig = Get-Content -LiteralPath $webDevelopmentConfigPath -Raw
}

if ($existingWebDevelopmentConfig.Trim() -ne $expectedWebDevelopmentConfig.Trim()) {
    Write-Step "Configuring Web development API base URL in appsettings.Development.json."
    Write-Utf8File -Path $webDevelopmentConfigPath -Content $expectedWebDevelopmentConfig
}

$bootstrapDirectory = Join-Path $repoRoot ".helpdesk-local"
$bootstrapMarkerPath = Join-Path $bootstrapDirectory "bootstrap-complete.txt"

if ($StopExisting) {
    Write-Step "Stopping existing Helpdesk dotnet processes."
    Stop-ExistingHelpdeskProcesses
}

$isFirstRun = -not (Test-Path -LiteralPath $bootstrapMarkerPath)
if ($isFirstRun) {
    Write-Step "First-time bootstrap detected."
}
elseif ($ForceBootstrap) {
    Write-Step "Force bootstrap requested."
}
else {
    Write-Step "Bootstrap marker found. Running incremental startup checks."
}

if (-not $SkipRestore) {
    Write-Step "Restoring solution packages."
    dotnet restore $solutionPath
}
else {
    Write-Step "Skipping restore as requested."
}

if (-not $SkipBuild) {
    Write-Step "Building solution with warnings as errors."
    dotnet build $solutionPath -warnaserror
}
else {
    Write-Step "Skipping build as requested."
}

if ($isFirstRun -or $ForceBootstrap) {
    if (-not (Test-Path -LiteralPath $bootstrapDirectory)) {
        New-Item -Path $bootstrapDirectory -ItemType Directory -Force | Out-Null
    }

    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    Set-Content -Path $bootstrapMarkerPath -Value "bootstrap_completed_utc=$timestamp" -Encoding ascii
}

$shellExe = Get-PowerShellExecutable
Write-Step "Launching API, Worker, and Web in separate PowerShell windows."

Start-ServiceWindow `
    -ShellExe $shellExe `
    -WorkingDirectory $repoRoot `
    -WindowTitle "Helpdesk API" `
    -RunCommand "dotnet run --project `"$apiProjectPath`" --launch-profile http"

Start-ServiceWindow `
    -ShellExe $shellExe `
    -WorkingDirectory $repoRoot `
    -WindowTitle "Helpdesk Worker" `
    -RunCommand "dotnet run --project `"$workerProjectPath`""

Start-ServiceWindow `
    -ShellExe $shellExe `
    -WorkingDirectory $repoRoot `
    -WindowTitle "Helpdesk Web" `
    -RunCommand "dotnet run --project `"$webProjectPath`" --launch-profile http"

Write-Host ""
Write-Host "Helpdesk services are starting." -ForegroundColor Green
Write-Host "API: http://localhost:5283" -ForegroundColor Green
Write-Host "Web: http://localhost:5006" -ForegroundColor Green
Write-Host "Health: http://localhost:5283/health/ready" -ForegroundColor Green
Write-Host ""
Write-Host "Seeded logins:" -ForegroundColor Yellow
Write-Host "  admin@msp.local / Pass!12345"
Write-Host "  tech@contoso.com / Pass!12345"
Write-Host "  tech@fabrikam.com / Pass!12345"
Write-Host ""

if (-not $SkipBrowser) {
    Start-Sleep -Seconds 3
    Start-Process "http://localhost:5006" | Out-Null
}
