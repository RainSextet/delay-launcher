Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = New-Object System.Collections.Generic.List[byte[]]

function New-ScaledIconPng([int]$size) {
    $work = 4
    $dim = $size * $work

    $big = [System.Drawing.Bitmap]::new($dim, $dim, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($dim / 256.0, $dim / 256.0)

    $rect = [System.Drawing.RectangleF]::new(0, 0, 256, 256)
    $c1 = [System.Drawing.Color]::FromArgb(255, 45, 132, 255)
    $c2 = [System.Drawing.Color]::FromArgb(255, 18, 83, 190)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $rect, $c1, $c2, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(0, 0, 112, 112, 180, 90)
    $path.AddArc(144, 0, 112, 112, 270, 90)
    $path.AddArc(144, 144, 112, 112, 0, 90)
    $path.AddArc(0, 144, 112, 112, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)

    $penRing = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 15.0)
    $penRing.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penRing.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawEllipse($penRing, 48, 48, 160, 160)

    $penHands = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 14.0)
    $penHands.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penHands.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($penHands, 128, 128, 128, 76)
    $g.DrawLine($penHands, 128, 128, 174, 152)

    $dot = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $g.FillEllipse($dot, 118, 118, 20, 20)

    $dot.Dispose()
    $penHands.Dispose()
    $penRing.Dispose()
    $path.Dispose()
    $brush.Dispose()
    $g.Dispose()

    $small = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g2.Clear([System.Drawing.Color]::Transparent)
    $g2.DrawImage($big, 0, 0, $size, $size)
    $g2.Dispose()
    $big.Dispose()

    $stream = [System.IO.MemoryStream]::new()
    $small.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return $bytes
}

foreach ($size in $sizes) {
    $pngs.Add((New-ScaledIconPng $size))
}

$outPath = Join-Path $PSScriptRoot 'app.ico'
$stream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $png = $pngs[$i]
    $dimension = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$png.Length)
    $writer.Write([UInt32]$offset)
    $offset += $png.Length
}

foreach ($png in $pngs) {
    $writer.Write($png)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($outPath, $stream.ToArray())
$writer.Dispose()
$stream.Dispose()

Write-Host ("Created " + $outPath + " (" + (Get-Item -LiteralPath $outPath).Length + " bytes)")
