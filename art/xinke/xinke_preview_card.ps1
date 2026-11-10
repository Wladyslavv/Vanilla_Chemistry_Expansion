# Replaces the "Nutrifruit Alcohol" card (row 2, column 3) of the workshop preview with the Xinke shrub
# growing in a hydroponic machine. Mirrors make_preview3.py: card at (680,394) 300x206, icon box 220x128 at
# y+12, nearest-neighbour integer scale, title msyhbd 24px at y+146, subtitle segoeuib 15px at y+178.
param([string]$Preview, [string]$Machine, [string]$Overlay, [string]$Out, [string]$Cn, [string]$En)
Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile($Preview)
$img = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($img)
$g.DrawImage($src, 0, 0, $src.Width, $src.Height)
$src.Dispose()

$CX = 680; $CY = 394; $CW = 300
$card = [System.Drawing.Color]::FromArgb(34, 40, 49)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $card), $CX + 10, $CY + 6, $CW - 20, 196)

# Sprite: machine with the ripening Xinke overlay on top, cropped to its visible pixels.
$sprite = New-Object System.Drawing.Bitmap 48, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [System.Drawing.Graphics]::FromImage($sprite)
foreach ($f in @($Machine, $Overlay)) { $l = [System.Drawing.Image]::FromFile($f); $sg.DrawImage($l, 0, 0, 48, 64); $l.Dispose() }
$sg.Dispose()
$minx = 99; $miny = 99; $maxx = -1; $maxy = -1
for ($y = 0; $y -lt 64; $y++) { for ($x = 0; $x -lt 48; $x++) {
    if ($sprite.GetPixel($x, $y).A -gt 8) { $minx = [math]::Min($minx, $x); $maxx = [math]::Max($maxx, $x); $miny = [math]::Min($miny, $y); $maxy = [math]::Max($maxy, $y) } } }
$w = $maxx - $minx + 1; $h = $maxy - $miny + 1
$scale = [math]::Max(1, [math]::Min(6, [math]::Min([math]::Floor(220 / $w), [math]::Floor(128 / $h))))
$bw = $w * $scale; $bh = $h * $scale
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$dest = New-Object System.Drawing.Rectangle ($CX + [math]::Floor(($CW - $bw) / 2)), ($CY + 12 + [math]::Floor((128 - $bh) / 2)), $bw, $bh
$g.DrawImage($sprite, $dest, $minx, $miny, $w, $h, [System.Drawing.GraphicsUnit]::Pixel)
"sprite ${w}x${h} scale $scale at $($dest.X),$($dest.Y)"

# Text: render to a scratch bitmap, find the ink box, then place it centred with the ink top/bottom where the
# old card's text sat (title ink top 546, subtitle baseline 588).
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
$g.DrawImage($t[0], (New-Object System.Drawing.Rectangle ([int]($mid - $tw / 2)), 546, $tw, $th), $t[1], $t[2], $tw, $th, [System.Drawing.GraphicsUnit]::Pixel)

$s = Ink $En "Segoe UI" 15 $muted
$sw = $s[3] - $s[1] + 1; $sh = $s[4] - $s[2] + 1
$g.DrawImage($s[0], (New-Object System.Drawing.Rectangle ([int]($mid - $sw / 2)), (588 - $sh + 1), $sw, $sh), $s[1], $s[2], $sw, $sh, [System.Drawing.GraphicsUnit]::Pixel)

$g.Dispose()
$rgb = New-Object System.Drawing.Bitmap $img.Width, $img.Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$rg = [System.Drawing.Graphics]::FromImage($rgb); $rg.DrawImage($img, 0, 0, $img.Width, $img.Height); $rg.Dispose()
$rgb.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"saved $Out"
