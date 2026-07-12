# Generates the application icon: a simple resistor glyph (zig-zag body between
# two leads) on a dark background. Visual quality is explicitly not a goal (TDD §5.7) —
# this exists so the exe has a distinguishable, on-theme icon, not to look polished.
# Renders each size natively for crispness and packs a multi-resolution .ico.
#
# Usage: powershell -ExecutionPolicy Bypass -File scripts\make-app-icon.ps1
# Output: src\EquivalentResistorCalculator.Gui\EquivalentResistorCalculator.ico

param(
    [string]$OutIco = "$PSScriptRoot\..\src\EquivalentResistorCalculator.Gui\EquivalentResistorCalculator.ico"
)

Add-Type -AssemblyName System.Drawing

function New-RoundedRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x,           $y,           $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y,           $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d,   0, 90)
    $p.AddArc($x,           $y + $h - $d, $d, $d,  90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $m    = $S * 0.055
    $rw   = $S - 2 * $m
    $rad  = $S * 0.18
    $rect = New-RoundedRect $m $m $rw $rw $rad

    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 24, 26, 30))
    $g.FillPath($bg, $rect)
    $g.SetClip($rect)

    # Resistor body: horizontal leads with a zig-zag band in the middle.
    $cy      = $m + $rw * 0.5
    $leadEnd = $m + $rw * 0.20
    $bodyEnd = $m + $rw * 0.80
    $lw      = [Math]::Max(1.4, $S / 256.0 * 10.0)

    $leadPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 210, 210, 215), $lw)
    $leadPen.StartCap = $leadPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($leadPen, $m, $cy, $leadEnd, $cy)
    $g.DrawLine($leadPen, $bodyEnd, $cy, $m + $rw, $cy)

    $zigCol = [System.Drawing.Color]::FromArgb(255, 235, 175, 60)
    $zigPen = New-Object System.Drawing.Pen($zigCol, $lw)
    $zigPen.StartCap = $zigPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $zigPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $peaks  = 6
    $bandW  = $bodyEnd - $leadEnd
    $amp    = $rw * 0.16
    $pts    = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    $pts.Add((New-Object System.Drawing.PointF($leadEnd, $cy)))
    for ($i = 1; $i -le $peaks; $i++) {
        $x = $leadEnd + $bandW * $i / ($peaks + 1)
        $y = if ($i % 2 -eq 1) { $cy - $amp } else { $cy + $amp }
        $pts.Add((New-Object System.Drawing.PointF($x, $y)))
    }
    $pts.Add((New-Object System.Drawing.PointF($bodyEnd, $cy)))
    $g.DrawLines($zigPen, $pts.ToArray())

    $g.ResetClip()

    $bw = [Math]::Max(1.0, $S / 256.0 * 4.0)
    $bp = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 70, 72, 78), $bw)
    $g.DrawPath($bp, $rect)

    $bg.Dispose(); $leadPen.Dispose(); $zigPen.Dispose(); $bp.Dispose()
    $rect.Dispose(); $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs  = @()
foreach ($s in $sizes) {
    $b  = Draw-Icon $s
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,($ms.ToArray())
    $b.Dispose()
}

# Pack ICO container (PNG-compressed entries, Vista+).
$fs = New-Object System.IO.FileStream($OutIco, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)                # reserved
$bw.Write([UInt16]1)                # type = icon
$bw.Write([UInt16]$sizes.Count)     # image count

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s   = $sizes[$i]
    $len = $pngs[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 means 256 in ICO
    $bw.Write([Byte]$dim)           # width
    $bw.Write([Byte]$dim)           # height
    $bw.Write([Byte]0)              # palette count
    $bw.Write([Byte]0)              # reserved
    $bw.Write([UInt16]1)            # color planes
    $bw.Write([UInt16]32)          # bits per pixel
    $bw.Write([UInt32]$len)         # bytes in resource
    $bw.Write([UInt32]$offset)      # offset
    $offset += $len
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Host "[OK] Wrote $OutIco ($($sizes.Count) sizes)"
