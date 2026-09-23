param(
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\B975RgbApp\B975RgbApp.csproj"
$output = Join-Path $PSScriptRoot "publish\win-x64"

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained:$($SelfContained.IsPresent.ToString().ToLowerInvariant()) `
    -p:PublishSingleFile=true `
    --output $output

Write-Host ""
Write-Host "Build completed:" -ForegroundColor Green
Write-Host (Join-Path $output "B975RgbApp.exe")
