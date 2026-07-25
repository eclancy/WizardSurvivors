param(
    [Parameter(Mandatory=$true)][string]$SourceDir,
    [Parameter(Mandatory=$true)][string]$OutFile,
    [int]$Cell = 96,
    [int]$Cols = 8,
    [string]$Filter = "*.png,*.jpg"
)

Add-Type -AssemblyName System.Drawing

$patterns = $Filter -split ','
$files = @()
foreach ($p in $patterns) {
    $files += Get-ChildItem -Path $SourceDir -File -Filter $p.Trim()
}
$files = $files | Sort-Object {
    $n = 0
    if ([int]::TryParse($_.BaseName, [ref]$n)) { $n } else { 999999 }
}, Name

if ($files.Count -eq 0) { Write-Error "No files"; exit 1 }

$pad = 22
$rows = [math]::Ceiling($files.Count / $Cols)
$w = $Cols * $Cell
$h = $rows * ($Cell + $pad)

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(30,30,36))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = New-Object System.Drawing.Font("Consolas", 9)
$brush = [System.Drawing.Brushes]::White

for ($i = 0; $i -lt $files.Count; $i++) {
    $col = $i % $Cols
    $row = [math]::Floor($i / $Cols)
    $x = $col * $Cell
    $y = $row * ($Cell + $pad)
    try {
        $img = [System.Drawing.Image]::FromFile($files[$i].FullName)
        $scale = [math]::Min($Cell / $img.Width, $Cell / $img.Height)
        $dw = [int]($img.Width * $scale)
        $dh = [int]($img.Height * $scale)
        $ox = $x + [int](($Cell - $dw) / 2)
        $oy = $y + [int](($Cell - $dh) / 2)
        $g.DrawImage($img, $ox, $oy, $dw, $dh)
        $img.Dispose()
    } catch {}
    $g.DrawString($files[$i].BaseName, $font, $brush, $x, $y + $Cell)
}

$g.Dispose()
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "Wrote $OutFile ($($files.Count) images)"
