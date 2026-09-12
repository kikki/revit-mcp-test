#Requires -Version 5.1
[CmdletBinding()]
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:USERPROFILE = Join-Path $root ".profile"
$env:HOME = $env:USERPROFILE
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:NUGET_PACKAGES = Join-Path $root ".nuget\packages"
$env:APPDATA = Join-Path $root ".appdata\roaming"
$env:LOCALAPPDATA = Join-Path $root ".appdata\local"
$env:ProgramData = "C:\ProgramData"
$env:ALLUSERSPROFILE = "C:\ProgramData"
$tempRoot = Join-Path $root "artifacts\temp"
New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME, $env:USERPROFILE, $env:NUGET_PACKAGES, $env:APPDATA, $env:LOCALAPPDATA, $tempRoot -Force | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot
$project = Join-Path $root "MCP_Server\Waabe.RevitMcp.Addin\Waabe.RevitMcp.Addin.csproj"
$addinManifest = Join-Path $root "MCP_Server\Waabe.RevitMcp.Addin\Waabe.RevitMcp.Loader.addin"
$serverOut = Join-Path $root "generated_MCP_Server"
$clientOut = Join-Path $root "generated_MCP_Client"
$nugetConfig = Join-Path $root "NuGet.Config"
$msbuild = $null
$programFilesX86 = ${env:ProgramFiles(x86)}
if ($programFilesX86) {
    $vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    }
}
if (-not $msbuild) {
    $commonMsbuilds = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )
    $msbuild = $commonMsbuilds | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $msbuild) {
    $msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($msbuildCommand) { $msbuild = $msbuildCommand.Source }
}

foreach ($year in @(2023, 2026)) {
    $api = "C:\Program Files\Autodesk\Revit $year\RevitAPI.dll"
    if (-not (Test-Path -LiteralPath $api)) { throw "Missing Revit $year API: $api" }
}

Get-ChildItem -LiteralPath $serverOut -Force | Where-Object Name -ne "README.md" | Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $clientOut -Force | Where-Object Name -ne "README.md" | Remove-Item -Recurse -Force

foreach ($year in @(2023, 2026)) {
    $yearRoot = Join-Path $serverOut "Revit$year"
    $payload = Join-Path $yearRoot "Waabe.RevitMcp.Addin"
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    if ($msbuild) {
        & $msbuild $project /restore /t:Build /nologo /verbosity:minimal `
            /p:Configuration=$Configuration /p:RevitYear=$year `
            /p:RestoreConfigFile=$nugetConfig /p:RestorePackagesPath=$env:NUGET_PACKAGES `
            /p:RestoreRootConfigDirectory=$root /p:OutputPath=$payload
    }
    else {
        dotnet build $project -c $Configuration -p:RevitYear=$year `
            -p:RestoreConfigFile=$nugetConfig `
            -p:RestoreRootConfigDirectory=$root `
            -o $payload --nologo
    }
    if ($LASTEXITCODE -ne 0) { throw "Revit $year build failed." }
    Copy-Item -LiteralPath $addinManifest -Destination (Join-Path $yearRoot "Waabe.RevitMcp.Loader.addin") -Force
}

node (Join-Path $root "tests\mcp-protocol-smoke.mjs")
if ($LASTEXITCODE -ne 0) { throw "MCP protocol smoke test failed." }

$temp = Join-Path ([IO.Path]::GetTempPath()) ("waabe-revit-mcpb-" + [Guid]::NewGuid().ToString("N"))
$zip = Join-Path ([IO.Path]::GetTempPath()) ("waabe-revit-mcpb-" + [Guid]::NewGuid().ToString("N") + ".zip")
$mcpb = Join-Path $clientOut "waabe-revit-readonly.mcpb"
try {
    New-Item -ItemType Directory -Path $temp | Out-Null
    Copy-Item -LiteralPath (Join-Path $root "MCP_Client\manifest.json") -Destination $temp
    Copy-Item -LiteralPath (Join-Path $root "MCP_Client\server") -Destination $temp -Recurse
    Compress-Archive -Path (Join-Path $temp "*") -DestinationPath $zip -CompressionLevel Optimal
    Move-Item -LiteralPath $zip -Destination $mcpb -Force
}
finally {
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
    if (Test-Path $zip) { Remove-Item $zip -Force }
}

$addinBundle = Join-Path $serverOut "waabe-revit-mcp-readonly-addins.zip"
Compress-Archive -Path (Join-Path $serverOut "Revit2023"), (Join-Path $serverOut "Revit2026") `
    -DestinationPath $addinBundle -CompressionLevel Optimal -Force

$clientHash = (Get-FileHash -LiteralPath $mcpb -Algorithm SHA256).Hash.ToLowerInvariant()
$serverHash = (Get-FileHash -LiteralPath $addinBundle -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $clientOut "SHA256SUMS.txt"), "$clientHash  waabe-revit-readonly.mcpb`r`n")
[IO.File]::WriteAllText((Join-Path $serverOut "SHA256SUMS.txt"), "$serverHash  waabe-revit-mcp-readonly-addins.zip`r`n")

Write-Host "Built Revit 2023 + 2026 add-ins: $addinBundle" -ForegroundColor Green
Write-Host "Built Claude MCPB: $mcpb" -ForegroundColor Green
