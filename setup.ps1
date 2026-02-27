# Downloads netcoredbg from https://github.com/Samsung/netcoredbg/releases
# Extracts to ./netcoredbg/
# Usage: .\setup.ps1

$ErrorActionPreference = "Stop"

$version = "3.1.3-1062"
$url = "https://github.com/Samsung/netcoredbg/releases/download/$version/netcoredbg-win64.zip"
$zipPath = "netcoredbg.zip"

Write-Host "Downloading netcoredbg $version..."
Invoke-WebRequest $url -OutFile $zipPath

Write-Host "Extracting..."
if (Test-Path "netcoredbg") {
    Remove-Item "netcoredbg" -Recurse -Force
}
Expand-Archive $zipPath -DestinationPath netcoredbg -Force
Remove-Item $zipPath

Write-Host "netcoredbg installed to ./netcoredbg/"
Write-Host "Executable: ./netcoredbg/netcoredbg.exe"
