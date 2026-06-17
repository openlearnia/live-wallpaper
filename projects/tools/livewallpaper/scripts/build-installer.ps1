$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $root "src\LiveWallpaper.App\LiveWallpaper.App.csproj"
$version = (Select-Xml -Path $csproj -XPath "//Version[1]").Node.InnerText.Trim()
$productVersion = if ($version -match '^\d+\.\d+\.\d+$') { "$version.0" } else { $version }
$publishDir = Join-Path $root "publish\win-x64"
$iconPath = Join-Path $root "assets\icon.ico"
$installerDir = Join-Path $root "installer"
$wxsGenerated = Join-Path $installerDir "ProductComponents.wxs"
$msiOut = Join-Path $root "dist\LiveWallpaper-$version-x64.msi"

Write-Host "Publishing app..."
dotnet publish (Join-Path $root "src\LiveWallpaper.App\LiveWallpaper.App.csproj") `
    -c Release -r win-x64 --self-contained `
    -o $publishDir

New-Item -ItemType Directory -Force -Path (Split-Path $msiOut) | Out-Null
$objDir = Join-Path $installerDir "obj"
if (Test-Path $wxsGenerated) { Remove-Item $wxsGenerated -Force }
if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

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
& candle.exe "-dPublishDir=$publishDir" "-dProductVersion=$productVersion" "-dIconPath=$iconPath" `
    (Join-Path $installerDir "LiveWallpaper.wxs") `
    $wxsGenerated `
    -out (Join-Path $installerDir "obj\\")
if ($LASTEXITCODE -ne 0) { throw "candle.exe failed with exit code $LASTEXITCODE" }

& light.exe -ext WixUIExtension -sice:ICE38 -sice:ICE43 `
    (Join-Path $installerDir "obj\LiveWallpaper.wixobj") `
    (Join-Path $installerDir "obj\ProductComponents.wixobj") `
    -out $msiOut
if ($LASTEXITCODE -ne 0) { throw "light.exe failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $msiOut)) { throw "MSI was not created at $msiOut" }

Write-Host "Created $msiOut"
