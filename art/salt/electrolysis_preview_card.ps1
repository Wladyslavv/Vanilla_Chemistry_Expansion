# Replaces one 300x206 card of the workshop preview with several item sprites side by side (e.g. rock salt ore and
# the electrolyzer). Same card layout as art/chili/lantern_preview_card.ps1: cards at x = 44 / 362 / 680 and
# y = 170 / 394 / 618, icon box 220x128 at y+12, one shared nearest-neighbour integer scale for all sprites, sprites
# bottom-aligned with a gap, title ink top at y+152 (Microsoft YaHei bold 24px), subtitle cap height on y+194
# (Segoe UI bold 15px).
param([string]$Preview, [string[]]$Sprites, [string]$Out, [string]$Cn, [string]$En,
      [int]$CX = 362, [int]$CY = 394, [int]$Gap = 6)
Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile($Preview)
$img = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($img)
$g.DrawImage($src, 0, 0, $src.Width, $src.Height)
$src.Dispose()

$CW = 300
$card = [System.Drawing.Color]::FromArgb(34, 40, 49)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $card), $CX + 10, $CY + 6, $CW - 20, 196)

# Crop each sprite to its visible pixels.
$parts = @()
foreach ($f in $Sprites) {
    $b = New-Object System.Drawing.Bitmap $f
    $minx = 9999; $miny = 9999; $maxx = -1; $maxy = -1
    for ($y = 0; $y -lt $b.Height; $y++) { for ($x = 0; $x -lt $b.Width; $x++) {
        if ($b.GetPixel($x, $y).A -gt 8) { $minx = [math]::Min($minx, $x); $maxx = [math]::Max($maxx, $x); $miny = [math]::Min($miny, $y); $maxy = [math]::Max($maxy, $y) } } }
    $parts += ,@($b, $minx, $miny, ($maxx - $minx + 1), ($maxy - $miny + 1))
}
$totalW = 0; $maxH = 0
foreach ($p in $parts) { $totalW += $p[3]; $maxH = [math]::Max($maxH, $p[4]) }
$scale = 6
while ($scale -gt 1 -and (($totalW * $scale + $Gap * ($parts.Count - 1)) -gt 220 -or $maxH * $scale -gt 128)) { $scale-- }
$rowW = $totalW * $scale + $Gap * ($parts.Count - 1)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$x0 = $CX + [math]::Floor(($CW - $rowW) / 2)
$bottom = $CY + 12 + [math]::Floor((128 + $maxH * $scale) / 2)
foreach ($p in $parts) {
    $w = $p[3] * $scale; $h = $p[4] * $scale
    $dest = New-Object System.Drawing.Rectangle $x0, ($bottom - $h), $w, $h
    $g.DrawImage($p[0], $dest, $p[1], $p[2], $p[3], $p[4], [System.Drawing.GraphicsUnit]::Pixel)
    $x0 += $w + $Gap
    $p[0].Dispose()
}
"sprites x$scale, row ${rowW}px"

function Ink([string]$text, [string]$family, [int]$size, [System.Drawing.Color]$color) {
    $font = New-Object System.Drawing.Font $family, $size, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $bmp = New-Object System.Drawing.Bitmap 400, 80, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $tg = [System.Drawing.Graphics]::FromImage($bmp)
    $tg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $tg.DrawString($text, $font, (New-Object System.Drawing.SolidBrush $color), 10, 10)
    $tg.Dispose()
    $a = 99999; $b = 99999; $c = -1; $d = -1
    for ($y = 0; $y -lt 80; $y++) { for ($x = 0; $x -lt 400; $x++) {
        if ($bmp.GetPixel($x, $y).A -gt 40) { $a = [math]::Min($a, $x); $c = [math]::Max($c, $x); $b = [math]::Min($b, $y); $d = [math]::Max($d, $y) } } }
    return @($bmp, $a, $b, $c, $d)
}
$white = [System.Drawing.Color]::FromArgb(245, 245, 245)
$muted = [System.Drawing.Color]::FromArgb(150, 160, 172)
$mid = $CX + $CW / 2

$t = Ink $Cn "Microsoft YaHei" 24 $white
$tw = $t[3] - $t[1] + 1; $th = $t[4] - $t[2] + 1
$g.DrawImage($t[0], (New-Object System.Drawing.Rectangle ([int]($mid - $tw / 2)), ($CY + 152), $tw, $th), $t[1], $t[2], $tw, $th, [System.Drawing.GraphicsUnit]::Pixel)

$s = Ink $En "Segoe UI" 15 $muted
$cap = Ink "H" "Segoe UI" 15 $muted
$capH = $cap[4] - $cap[2] + 1
$sw = $s[3] - $s[1] + 1; $sh = $s[4] - $s[2] + 1
$g.DrawImage($s[0], (New-Object System.Drawing.Rectangle ([int]($mid - $sw / 2)), ($CY + 194 - $capH + 1), $sw, $sh), $s[1], $s[2], $sw, $sh, [System.Drawing.GraphicsUnit]::Pixel)

$g.Dispose()
$rgb = New-Object System.Drawing.Bitmap $img.Width, $img.Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$rg = [System.Drawing.Graphics]::FromImage($rgb)
$rg.DrawImage($img, 0, 0, $img.Width, $img.Height)
$rg.Dispose()
$img.Dispose()
$rgb.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$rgb.Dispose()
"saved $Out"
