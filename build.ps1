# Buduje dist\SlideSync.exe przy użyciu kompilatora C# wbudowanego w Windows (.NET Framework 4.x).
# Nie wymaga Visual Studio ani .NET SDK.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$iconPath = Join-Path $root 'assets\icon.ico'

# --- ikona (generowana raz) -------------------------------------------------
if (-not (Test-Path $iconPath)) {
    Add-Type -AssemblyName System.Drawing
    New-Item -ItemType Directory -Force (Split-Path $iconPath) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap 256, 256
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 110, 220))), 8, 8, 240, 240)
    $tri = [System.Drawing.Point[]]@((New-Object System.Drawing.Point 96, 64), (New-Object System.Drawing.Point 96, 192), (New-Object System.Drawing.Point 200, 128))
    $g.FillPolygon([System.Drawing.Brushes]::White, $tri)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $fs = [System.IO.File]::Create($iconPath)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)          # ICONDIR
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)  # 256x256 (0 = 256), brak palety
    $bw.Write([uint16]1); $bw.Write([uint16]32)                                # planes, bpp
    $bw.Write([uint32]$png.Length); $bw.Write([uint32]22)                      # rozmiar, offset
    $bw.Write($png)
    $bw.Close(); $fs.Close(); $bmp.Dispose()
}

# --- kompilacja ---------------------------------------------------------------
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'Nie znaleziono csc.exe (.NET Framework 4.x).' }

New-Item -ItemType Directory -Force $dist | Out-Null
$out = Join-Path $dist 'SlideSync.exe'
$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    /win32icon:$iconPath /out:$out `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "Kompilacja nie powiodła się (kod $LASTEXITCODE)." }
Write-Host "OK: $out ($([math]::Round((Get-Item $out).Length / 1KB)) KB)"
