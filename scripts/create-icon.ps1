Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 32, 32
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 72, 120, 210))
$g.FillEllipse([System.Drawing.Brushes]::White, 6, 6, 20, 20)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$path = Join-Path $PSScriptRoot '..\assets\icon.ico'
$dir = Split-Path $path
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$fs = [System.IO.File]::Create($path)
$icon.Save($fs)
$fs.Close()
$g.Dispose()
$bmp.Dispose()
Write-Host "Created $path"
