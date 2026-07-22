Set-Location "C:\Users\Eric\Documents\GitHub\wizard-survivors"
Add-Type -AssemblyName System.Drawing

$srcRoot = 'assets\imported\fantasy\source_mirror'
$dstRoot = 'assets\imported\fantasy\curated\map_props'

$work = @(
    @{ Rel = 'craftpix-net-141354-free-top-down-bushes-pixel-art\PNG\Assets_shadow_source.png'; Out = 'bushes' },
    @{ Rel = 'craftpix-net-385863-free-top-down-trees-pixel-art\PNG\Trees_shadow_source.png'; Out = 'trees' },
    @{ Rel = 'craftpix-net-934618-free-top-down-ruins-pixel-art\PNG\Assets_shadows_source.png'; Out = 'ruins' }
)

function Split-SpacedObjects([string]$src, [string]$dst, [string]$prefix) {
    $bmp = [System.Drawing.Bitmap]::FromFile($src)
    $w = $bmp.Width
    $h = $bmp.Height
    $visited = New-Object 'bool[,]' $w, $h
    $dirs = @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))
    $parts = 0

    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($visited[$x, $y]) { continue }
            $visited[$x, $y] = $true
            if ($bmp.GetPixel($x, $y).A -eq 0) { continue }

            $queue = New-Object System.Collections.Generic.Queue[System.Drawing.Point]
            $queue.Enqueue([System.Drawing.Point]::new($x, $y))
            $minX = $x; $maxX = $x; $minY = $y; $maxY = $y

            while ($queue.Count -gt 0) {
                $p = $queue.Dequeue()
                foreach ($d in $dirs) {
                    $nx = $p.X + $d[0]
                    $ny = $p.Y + $d[1]
                    if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
                    if ($visited[$nx, $ny]) { continue }
                    $visited[$nx, $ny] = $true
                    if ($bmp.GetPixel($nx, $ny).A -gt 0) {
                        $queue.Enqueue([System.Drawing.Point]::new($nx, $ny))
                        if ($nx -lt $minX) { $minX = $nx }
                        if ($nx -gt $maxX) { $maxX = $nx }
                        if ($ny -lt $minY) { $minY = $ny }
                        if ($ny -gt $maxY) { $maxY = $ny }
                    }
                }
            }

            $rect = [System.Drawing.Rectangle]::new($minX, $minY, $maxX - $minX + 1, $maxY - $minY + 1)
            if ($rect.Width -lt 4 -or $rect.Height -lt 4) { continue }

            $tile = $bmp.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $name = "{0}_{1:D3}.png" -f $prefix, $parts
            $out = Join-Path $dst $name
            $tile.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
            $tile.Dispose()
            $parts++
        }
    }

    $bmp.Dispose()
    return $parts
}

$total = 0
foreach ($item in $work) {
    $src = Join-Path $srcRoot $item.Rel
    if (-not (Test-Path -LiteralPath $src)) {
        continue
    }

    $outDir = Join-Path $dstRoot $item.Out
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    # Clear old outputs to keep deterministic names/counts.
    Get-ChildItem -LiteralPath $outDir -File -Filter "*.png" -ErrorAction SilentlyContinue | Remove-Item -Force

    $count = Split-SpacedObjects -src $src -dst $outDir -prefix $item.Out
    $total += $count

    [pscustomobject]@{
        Source = $item.Rel
        OutputFolder = $outDir
        ExportedSprites = $count
    } | Format-List
}

"TotalExported=$total"
