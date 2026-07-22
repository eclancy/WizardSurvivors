Set-Location "C:\Users\Eric\Documents\GitHub\wizard-survivors"

$manifestDir = "assets\imported\fantasy\manifests"
$ambPath = Join-Path $manifestDir "ambiguous_review_manifest.csv"
$rows = Import-Csv $ambPath

# Prefill Fantasy Dungeon Singles based on path + size
foreach ($r in $rows) {
    $rel = $r.RelativePath -replace '/', '\\'
    if ($rel -match '^Fantasy Dungeon Tileset\\Singles\\(A1|A2|A4)\\(.+)\.png$') {
        $group = $matches[1].ToLowerInvariant()
        $name = $matches[2].ToLowerInvariant()
        $r.Category = 'dungeon_tilesheet_or_tile'

        if ([string]::IsNullOrWhiteSpace($r.ProposedLabel)) {
            $r.ProposedLabel = "dungeon_${group}_${name}"
        }

        if ($r.Width -eq '576' -and $r.Height -eq '192' -and [string]::IsNullOrWhiteSpace($r.Notes)) {
            $r.Notes = 'Likely 4x12 organized tilesheet (auto-prefill from user rule)'
        }

        if ([string]::IsNullOrWhiteSpace($r.UserClarification)) {
            $r.UserClarification = 'prefilled_needs_review'
        }
    }
}

$rows | Export-Csv -Path $ambPath -NoTypeInformation -Encoding UTF8

# Build pending queue for review
$pending = $rows | Where-Object { $_.UserClarification -ne 'confirmed' }
$queuePath = Join-Path $manifestDir 'clarify_queue.csv'
$queue = $pending | ForEach-Object {
    $need = if ($_.Category -eq 'dungeon_tilesheet_or_tile') {
        'Confirm tile theme and if sheet is 4x12; keep/discard'
    }
    elseif ($_.Category -eq 'spaced_object_sheet') {
        'Confirm object family (rocks/plants/trees/ruins) and if auto-split should be done'
    }
    elseif ($_.Category -eq 'numeric_unknown') {
        'Name the effect/object and intended usage (spell/map/item/ui)'
    }
    else {
        'Confirm label and usage'
    }

    [pscustomobject]@{
        RelativePath = $_.RelativePath
        Width = $_.Width
        Height = $_.Height
        Category = $_.Category
        ProposedLabel = $_.ProposedLabel
        NeedFromUser = $need
        UserClarification = $_.UserClarification
        Notes = $_.Notes
    }
}
$queue | Export-Csv -Path $queuePath -NoTypeInformation -Encoding UTF8

# Generate visual markdown batches
$batchDir = Join-Path $manifestDir 'review_batches'
New-Item -ItemType Directory -Force -Path $batchDir | Out-Null
$batchSize = 24
$items = $queue | Sort-Object Category, RelativePath
$total = $items.Count
$batchCount = [math]::Ceiling($total / $batchSize)

for ($i = 0; $i -lt $batchCount; $i++) {
    $start = $i * $batchSize
    $subset = $items | Select-Object -Skip $start -First $batchSize

    $lines = @()
    $lines += "# Sprite Clarification Batch $($i + 1) of $batchCount"
    $lines += ""
    $lines += "For each row, please provide:"
    $lines += "- final label"
    $lines += "- category/usage (spell vfx, map prop, item icon, enemy sprite, tile, ui)"
    $lines += "- keep for game: yes or no"
    $lines += "- any notes (animation frames, tile grid, corruption, etc.)"
    $lines += ""

    $idx = $start + 1
    foreach ($r in $subset) {
        $imgPath = "../../source_mirror/" + ($r.RelativePath -replace '\\', '/')
        $enc = [uri]::EscapeUriString($imgPath)
        $lines += "## $idx. $($r.RelativePath -replace '\\', '/')"
        $lines += ""
        $lines += "![sprite]($enc)"
        $lines += ""
        $lines += "- current category: $($r.Category)"
        $lines += "- proposed label: $($r.ProposedLabel)"
        $lines += "- size: $($r.Width)x$($r.Height)"
        $lines += "- need from you: $($r.NeedFromUser)"
        if (-not [string]::IsNullOrWhiteSpace($r.Notes)) {
            $lines += "- notes: $($r.Notes)"
        }
        $lines += ""
        $idx++
    }

    $out = Join-Path $batchDir ("batch_{0:D2}.md" -f ($i + 1))
    Set-Content -Path $out -Value $lines -Encoding UTF8
}

[pscustomobject]@{
    UpdatedManifestRows = $rows.Count
    QueueRows = $queue.Count
    BatchCount = $batchCount
    QueuePath = $queuePath
    BatchDir = $batchDir
} | Format-List
