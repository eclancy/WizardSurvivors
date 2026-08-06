param(
    [string]$RepoRoot = 'C:\Users\ericc\Documents\GitHub\wizard-survivors',
    [string]$ManifestPath = 'assets\organized\asset_manifest.json'
)

$ErrorActionPreference = 'Stop'
$resolvedRepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$manifest = Get-Content -Path (Join-Path $resolvedRepoRoot $ManifestPath) -Raw | ConvertFrom-Json

function Normalize-Path([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return '' }
    $normal = $value.ToLowerInvariant().Replace('\\', '/').Replace('\', '/')
    if ($normal.StartsWith('res://')) { $normal = $normal.Substring(6) }
    return $normal.Trim('/')
}

function Get-NameStem([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return '' }
    $name = [System.IO.Path]::GetFileNameWithoutExtension($value)
    if ([string]::IsNullOrWhiteSpace($name)) { $name = [System.IO.Path]::GetFileName($value) }
    return $name.ToLowerInvariant()
}

function Get-TokenSet([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return @() }
    $normalized = Normalize-Path $value
    return $normalized -split '/' | Where-Object { $_ } | ForEach-Object { $_ -replace '[^a-z0-9]+', ' ' } | Where-Object { $_ } | ForEach-Object { $_.Trim() }
}

function Find-ManifestEntry([string]$oldPath) {
    $oldNorm = Normalize-Path $oldPath
    $oldFileName = [System.IO.Path]::GetFileName($oldPath)
    $oldStem = Get-NameStem $oldPath
    $oldTokens = Get-TokenSet $oldPath
    $candidates = @()

    foreach ($entry in $manifest) {
        $entrySourceNorm = Normalize-Path $entry.relativeSource
        $entrySourceFileName = [System.IO.Path]::GetFileName($entry.source)
        $entryTargetRelative = Normalize-Path $entry.targetRelative
        $entryTargetFileName = [System.IO.Path]::GetFileName($entry.target)
        $entryTokens = Get-TokenSet ($entrySourceNorm + ' ' + $entryTargetRelative)

        $score = 0
        if ($oldFileName -eq $entrySourceFileName -or $oldFileName -eq $entryTargetFileName) { $score += 100 }
        if ($oldStem -and ($oldStem -eq (Get-NameStem $entrySourceFileName) -or $oldStem -eq (Get-NameStem $entryTargetFileName))) { $score += 50 }

        $overlap = 0
        foreach ($token in $oldTokens) {
            if ($entryTokens -contains $token) { $overlap++ }
        }
        $score += $overlap * 5

        if ($score -gt 0) {
            $candidates += [pscustomobject]@{ Entry = $entry; Score = $score }
        }
    }

    if ($candidates.Count -eq 0) { return $null }
    return ($candidates | Sort-Object Score -Descending | Select-Object -First 1).Entry
}

$files = Get-ChildItem -Path $resolvedRepoRoot -Recurse -File -Force | Where-Object {
    $_.Extension -match '\.(tres|tscn|res|cs|gd|json)$' -and
    $_.FullName -notmatch [regex]::Escape([System.IO.Path]::Combine($resolvedRepoRoot, '.git')) -and
    $_.FullName -notmatch [regex]::Escape([System.IO.Path]::Combine($resolvedRepoRoot, '.godot')) -and
    $_.FullName -notmatch [regex]::Escape([System.IO.Path]::Combine($resolvedRepoRoot, '.github')) -and
    $_.FullName -notmatch [regex]::Escape([System.IO.Path]::Combine($resolvedRepoRoot, 'assets\organized'))
}
$files = $files | Where-Object {
    $content = Get-Content -Path $_.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { return $false }
    return $content -match 'assets/imported|assets/tilesets'
}
$updatedFiles = 0
$updatedPaths = 0

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $pattern = '(?:res://)?assets/(?:imported|tilesets)/[^\s"''`]+ '
    $matches = [regex]::Matches($content, '(?:res://)?assets/(?:imported|tilesets)/[^\s"''`]+')

    if ($matches.Count -eq 0) { continue }

    $updatedContent = $content
    foreach ($match in $matches) {
        $oldValue = $match.Value
        if ($oldValue -match '^res://') {
            $prefix = 'res://'
        }
        else {
            $prefix = ''
        }

        $entry = Find-ManifestEntry $oldValue
        if (-not $entry) { continue }

        $newPath = "$prefix$($entry.targetRelative.Replace('\\', '/'))"
        $newPath = $newPath -replace '\\', '/'
        if ($newPath -ne $oldValue) {
            $updatedContent = $updatedContent.Replace($oldValue, $newPath)
            $updatedPaths++
        }
    }

    if ($updatedContent -ne $content) {
        Set-Content -Path $file.FullName -Value $updatedContent -Encoding UTF8
        $updatedFiles++
    }
}

Write-Host "Updated $updatedFiles files and $updatedPaths asset path references."
