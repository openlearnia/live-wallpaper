$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $root "src\LiveWallpaper.App\LiveWallpaper.App.csproj"
$version = (Select-Xml -Path $csproj -XPath "//Version[1]").Node.InnerText.Trim()
$productVersion = if ($version -match '^\d+\.\d+\.\d+$') { "$version.0" } else { $version }
$publishDir = Join-Path $root "publish\win-x64"
$installerDir = Join-Path $root "installer"
$wxsGenerated = Join-Path $installerDir "ProductComponents.wxs"
$msiOut = Join-Path $root "dist\LiveWallpaper-$version-x64.msi"

Write-Host "Publishing app..."
dotnet publish (Join-Path $root "src\LiveWallpaper.App\LiveWallpaper.App.csproj") `
    -c Release -r win-x64 --self-contained `
    -o $publishDir

New-Item -ItemType Directory -Force -Path (Split-Path $msiOut) | Out-Null

$heat = Get-Command heat.exe -ErrorAction SilentlyContinue
$candle = Get-Command candle.exe -ErrorAction SilentlyContinue
$light = Get-Command light.exe -ErrorAction SilentlyContinue

if (-not $heat -or -not $candle -or -not $light) {
    Write-Warning "WiX Toolset v3 (heat/candle/light) not found in PATH."
    Write-Host "Publish output is ready at: $publishDir"
    Write-Host "Install WiX 3 and re-run this script to build the MSI."
    exit 0
}

Write-Host "Harvesting files with heat..."
& heat.exe dir $publishDir `
    -cg ProductComponents `
    -dr INSTALLFOLDER `
    -var var.PublishDir `
    -gg -sfrag -srd -out $wxsGenerated

Write-Host "Compiling MSI..."
& candle.exe -dPublishDir=$publishDir -dProductVersion=$productVersion `
    (Join-Path $installerDir "LiveWallpaper.wxs") `
    $wxsGenerated `
    -out (Join-Path $installerDir "obj\\")

& light.exe -ext WixUIExtension `
    (Join-Path $installerDir "obj\LiveWallpaper.wixobj") `
    (Join-Path $installerDir "obj\ProductComponents.wixobj") `
    -out $msiOut

Write-Host "Created $msiOut"
