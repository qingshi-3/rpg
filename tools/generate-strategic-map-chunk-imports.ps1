param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$projectRootPath = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectRootPrefix = $projectRootPath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$chunkDirectory = Join-Path $projectRootPath 'assets/textures/world/visual-chunks'
$pngs = @(Get-ChildItem -LiteralPath $chunkDirectory -File -Filter '*.png' | Sort-Object Name)
if ($pngs.Count -ne 35) {
    throw "Expected exactly 35 production Chunk PNGs, found $($pngs.Count)."
}

$uidAlphabet = 'abcdefghijklmnopqrstuvwxy012345678'
$existingUids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
Get-ChildItem -LiteralPath $projectRootPath -Recurse -File -Filter '*.import' |
    Where-Object { -not $_.FullName.StartsWith($chunkDirectory + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object {
        $match = [System.Text.RegularExpressions.Regex]::Match(
            [System.IO.File]::ReadAllText($_.FullName),
            '(?m)^uid="([^"]+)"$')
        if ($match.Success -and -not $existingUids.Add($match.Groups[1].Value)) {
            throw "Existing import UID collision prevents safe generation uid=$($match.Groups[1].Value)."
        }
    }

function Get-LowerHexHash([System.Security.Cryptography.HashAlgorithm]$algorithm, [string]$value) {
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
        return [System.BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-PathUid([string]$sourcePath) {
    $digest = Get-LowerHexHash ([System.Security.Cryptography.SHA256]::Create()) ("StrategicMapChunkImportUID`0" + $sourcePath)
    [uint64]$value = [System.Convert]::ToUInt64($digest.Substring(0, 16), 16)
    $value = $value -band [uint64][long]::MaxValue
    $encoded = ''
    do {
        $index = [int]($value % [uint64]$uidAlphabet.Length)
        $encoded = $uidAlphabet[$index] + $encoded
        $value = [uint64][decimal]::Floor(([decimal]$value) / ([decimal]$uidAlphabet.Length))
    } while ($value -gt 0)
    return 'uid://' + $encoded
}

$generatedUids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($png in $pngs) {
    if (-not $png.FullName.StartsWith($projectRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Production Chunk path escaped project root path=$($png.FullName)."
    }
    $relativePath = $png.FullName.Substring($projectRootPrefix.Length).Replace('\', '/')
    $sourcePath = 'res://' + $relativePath
    $cacheHash = Get-LowerHexHash ([System.Security.Cryptography.MD5]::Create()) $sourcePath
    $cachePath = "res://.godot/imported/$($png.Name)-$cacheHash.ctex"
    $uid = Get-PathUid $sourcePath
    if (-not $generatedUids.Add($uid) -or $existingUids.Contains($uid)) {
        throw "Path-derived import UID collision source=$sourcePath uid=$uid."
    }

    $content = @"
[remap]

importer="texture"
type="CompressedTexture2D"
uid="$uid"
path="$cachePath"
metadata={
"vram_texture": false
}

[deps]

source_file="$sourcePath"
dest_files=["$cachePath"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=true
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"@
    [System.IO.File]::WriteAllText(
        $png.FullName + '.import',
        $content.Replace("`r`n", "`n"),
        [System.Text.UTF8Encoding]::new($false))
}

Write-Output "Generated $($pngs.Count) deterministic production Chunk import sidecars."
