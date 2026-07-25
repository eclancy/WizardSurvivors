param(
    [Parameter(Mandatory=$true)][string]$In,
    [Parameter(Mandatory=$true)][string]$Out,
    [double]$TintR = 1.0,
    [double]$TintG = 1.0,
    [double]$TintB = 1.0,
    [double]$Boost = 1.0
)

Add-Type -AssemblyName System.Drawing

$src = New-Object System.Drawing.Bitmap($In)
$w = $src.Width
$h = $src.Height
$dst = New-Object System.Drawing.Bitmap($w, $h)

for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $p = $src.GetPixel($x, $y)
        $a = $p.A
        if ($a -eq 0) {
            $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0,0,0,0))
            continue
        }
        # relative luminance
        $l = (0.299 * $p.R + 0.587 * $p.G + 0.114 * $p.B) / 255.0
        $r = [math]::Min(255, [int]($l * $TintR * $Boost * 255))
        $g = [math]::Min(255, [int]($l * $TintG * $Boost * 255))
        $b = [math]::Min(255, [int]($l * $TintB * $Boost * 255))
        $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $r, $g, $b))
    }
}

$dst.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$src.Dispose(); $dst.Dispose()
Write-Output "Recolored -> $Out ($w x $h)"
