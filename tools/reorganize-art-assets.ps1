param(
    [string]$SourceRoot = 'C:\Users\ericc\Desktop\Wizard Survivors Art',
    [string]$RepoRoot = 'C:\Users\ericc\Documents\GitHub\wizard-survivors',
    [string]$OutputRoot = 'assets\organized'
)

$ErrorActionPreference = 'Stop'
$resolvedRepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$resolvedSourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
$resolvedOutputRoot = [System.IO.Path]::Combine($resolvedRepoRoot, $OutputRoot)

function Sanitize-Token([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return 'asset' }
    $token = $value.ToLowerInvariant()
    $token = $token -replace '[^a-z0-9]+', '-'
    $token = $token.Trim('-')
    if ([string]::IsNullOrWhiteSpace($token)) { return 'asset' }
    return $token
}

function Get-Category([string]$relativePath, [string]$fileName) {
    $pathLower = $relativePath.ToLowerInvariant()
    $nameLower = $fileName.ToLowerInvariant()

    if ($pathLower -match 'fantasy_rpg_gui|gui|ui|hud|icon|icons|menu|inventory|login|skills|chat|bg|loading|journal|options|quests|registration|map|font') {
        return 'ui'
    }

    if ($pathLower -match 'enemy|enemies|monster|monsters|skeleton|vampire|orc|undead|skull|mob') {
        return 'enemies'
    }

    if ($pathLower -match 'character|characters|hero|player|wizard|soldier|priest|avatar') {
        return 'characters'
    }

    if ($pathLower -match 'magic|effect|effects|explosion|shield|bolt|lightning|spike|spikes|fireball|fire|blast|animation|spritesheet|sprite|self') {
        return 'effects'
    }

    if ($pathLower -match 'tile|tiles|tileset|map|dungeon|ground|floor|water|cave|desert|castle|tiled') {
        return 'level/tiles'
    }

    if ($pathLower -match 'prop|props|object|objects|tree|bush|rock|stone|crystal|ruin|torch|chest|coin|flag|box|bushes|trees|ruins') {
        return 'level/props'
    }

    if ($pathLower -match 'background|castle|mountain|sky|forest|desert|environment|scene|landscape') {
        return 'level/environment'
    }

    if ($pathLower -match 'font') {
        return 'ui'
    }

    return 'shared'
}

function Get-RelativePath([string]$fromPath, [string]$toPath) {
    $resolvedFrom = [System.IO.Path]::GetFullPath($fromPath)
    $resolvedTo = [System.IO.Path]::GetFullPath($toPath)

    if ($resolvedFrom.EndsWith([System.IO.Path]::DirectorySeparatorChar) -or $resolvedFrom.EndsWith([System.IO.Path]::AltDirectorySeparatorChar)) {
        $baseUri = [System.Uri]($resolvedFrom)
    }
    else {
        $baseUri = [System.Uri]($resolvedFrom + [System.IO.Path]::DirectorySeparatorChar)
    }

    $targetUri = [System.Uri]($resolvedTo)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-TargetCategoryPath([string]$category) {
    switch ($category) {
        'level/tiles' { return Join-Path $resolvedOutputRoot 'level/tiles' }
        'level/props' { return Join-Path $resolvedOutputRoot 'level/props' }
        'level/environment' { return Join-Path $resolvedOutputRoot 'level/environment' }
        'characters' { return Join-Path $resolvedOutputRoot 'characters' }
        'enemies' { return Join-Path $resolvedOutputRoot 'enemies' }
        'effects' { return Join-Path $resolvedOutputRoot 'effects' }
        'ui' { return Join-Path $resolvedOutputRoot 'ui' }
        'shared' { return Join-Path $resolvedOutputRoot 'shared' }
        default { return Join-Path $resolvedOutputRoot 'shared' }
    }
}

function Get-TargetDirectory([string]$category, [string]$relativePath) {
    return Get-TargetCategoryPath $category
}

function Get-FamilyName([string]$relativePath) {
    $segments = $relativePath -split '[\\/]' | Where-Object { $_ -and $_ -notin @('Wizard Survivors Art', '2d Fantasy Game Assets', 'Fantasy_RPG_GUI') }
    if ($segments.Count -eq 0) { return 'generic' }

    $family = $segments[0]
    if ($segments.Count -gt 1 -and $segments[1] -notmatch '^(png|jpg|jpeg|ttf|txt)$') {
        $family = $segments[0] + '-' + $segments[1]
    }

    return Sanitize-Token $family
}

function Get-TargetFileName([string]$category, [string]$relativePath, [string]$fileName) {
    $extension = [System.IO.Path]::GetExtension($fileName)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
    $family = Get-FamilyName $relativePath
    $categoryToken = if ($category -eq 'level/tiles') { 'lvl-tiles' }
        elseif ($category -eq 'level/props') { 'lvl-props' }
        elseif ($category -eq 'level/environment') { 'lvl-env' }
        elseif ($category -eq 'characters') { 'char' }
        elseif ($category -eq 'enemies') { 'enemy' }
        elseif ($category -eq 'effects') { 'fx' }
        elseif ($category -eq 'ui') { 'ui' }
        else { 'shared' }

    $sanitizedStem = Sanitize-Token $stem
    $sanitizedFamily = Sanitize-Token $family
    return "$categoryToken-$sanitizedFamily-$sanitizedStem$extension"
}

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'level/tiles') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'level/props') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'level/environment') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'characters') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'enemies') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'effects') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'ui') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'shared') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputRoot 'quarantine') | Out-Null

$files = Get-ChildItem -Path $resolvedSourceRoot -File -Recurse | Where-Object { $_.Extension -match '\.(png|jpg|jpeg|ttf|txt)$' }

$manifest = [System.Collections.Generic.List[object]]::new()

foreach ($file in $files) {
    $relativePath = Get-RelativePath $resolvedSourceRoot $file.FullName
    $category = Get-Category $relativePath $file.Name
    $targetDir = Get-TargetDirectory $category $relativePath
    $targetFileName = Get-TargetFileName $category $relativePath $file.Name
    $targetPath = Join-Path $targetDir $targetFileName

    if ($category -eq 'shared' -and $relativePath -match 'font|gui|icons|icon|menu|ui') {
        $category = 'ui'
        $targetDir = Get-TargetDirectory $category $relativePath
        $targetFileName = Get-TargetFileName $category $relativePath $file.Name
        $targetPath = Join-Path $targetDir $targetFileName
    }

    if ($category -eq 'shared' -and $relativePath -match 'magic|effect|explosion|shield|lightning') {
        $category = 'effects'
        $targetDir = Get-TargetDirectory $category $relativePath
        $targetFileName = Get-TargetFileName $category $relativePath $file.Name
        $targetPath = Join-Path $targetDir $targetFileName
    }

    $targetDir = [System.IO.Path]::GetDirectoryName($targetPath)
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    if (Test-Path $targetPath) {
        $counter = 1
        $base = [System.IO.Path]::GetFileNameWithoutExtension($targetPath)
        $ext = [System.IO.Path]::GetExtension($targetPath)
        do {
            $candidate = Join-Path $targetDir "$base-$counter$ext"
            $counter++
        } while (Test-Path $candidate)
        $targetPath = $candidate
    }

    Copy-Item -Path $file.FullName -Destination $targetPath -Force

    $manifest.Add([pscustomobject]@{
        source = $file.FullName
        relativeSource = $relativePath
        category = $category
        target = $targetPath
        targetRelative = Get-RelativePath $resolvedRepoRoot $targetPath
        sizeBytes = $file.Length
    })
}

$manifestPath = Join-Path $resolvedOutputRoot 'asset_manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host "Copied $($manifest.Count) assets into $resolvedOutputRoot"
Write-Host "Manifest written to $manifestPath"
