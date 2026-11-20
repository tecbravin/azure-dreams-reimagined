param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot | Split-Path -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$metadata = Get-Content -LiteralPath "$projectRoot/Assets/AzureDreams/Sprites/DungeonCreatures/resource_014/metadata.json" -Raw | ConvertFrom-Json
$indices = [System.Drawing.Bitmap]::FromFile("$projectRoot/Assets/AzureDreams/Sprites/Indexed/resource_014/atlas_indices.png")
$dungeon = [System.IO.File]::ReadAllBytes("$SourceRoot/DUNGEON/DUNGEON.BIN")
$bank = (0x56D6 - 12310 + 13 * 43) * 2048
for ($word = 0; $word -lt 10; $word++) {
    if ([BitConverter]::ToUInt32($dungeon, $bank + $word * 4) -ne $metadata.header_words[$word]) {
        throw 'DUNGEON.BIN does not match the extracted Nyuel metadata.'
    }
}
$table = [BitConverter]::ToUInt32($dungeon, $bank)
$chains = @{}
foreach ($chain in $metadata.chains) { $chains[[int]$chain.pointer] = $chain.parts }
$atlas = New-Object System.Drawing.Bitmap 1920,480
$graphics = [System.Drawing.Graphics]::FromImage($atlas)

function Read-Frames([int]$entry) {
    $start = [BitConverter]::ToUInt32($dungeon, $bank + $table + $entry * 4)
    $end = [BitConverter]::ToUInt32($dungeon, $bank + $table + ($entry + 1) * 4)
    $frames = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    for ($offset = $start; $offset -lt $end; $offset += 8) {
        if ([BitConverter]::ToUInt16($dungeon, $bank + $offset + 2) -ne 2) { continue }
        $pointer = [int][BitConverter]::ToUInt32($dungeon, $bank + $offset + 4)
        $parts = $chains[$pointer]
        $frame = New-Object System.Drawing.Bitmap 96,96
        for ($partIndex = $parts.Count - 1; $partIndex -ge 0; $partIndex--) {
            $part = $parts[$partIndex]
            # CLUT 1 is the bank's embedded rider. Render only the Nyuel body;
            # Koh and the other jockeys are independent mounted characters.
            if ($part.is_callback -or $part.source_page -ne 'resource_atlas' -or $part.texture_mode -ne 0 -or $part.clut_raw -ne 0) { continue }
            for ($y = 0; $y -lt $part.height; $y++) {
                for ($x = 0; $x -lt $part.width; $x++) {
                    $index = $indices.GetPixel($part.u + $x, $part.v + $y).R
                    if ($index -eq 0) { continue }
                    $destX = 48 + $part.x + $x
                    $destY = 84 + $part.y + $y
                    if ($part.flags -band 1) { $destX = 48 - $part.x - 1 - $x }
                    if ($part.flags -band 2) { $destY = 84 - $part.y - 1 - $y }
                    if ($destX -ge 0 -and $destX -lt 96 -and $destY -ge 0 -and $destY -lt 96) {
                        $frame.SetPixel($destX, $destY, [System.Drawing.Color]::FromArgb(255,$index,0,0))
                    }
                }
            }
        }
        $frames.Add($frame)
    }
    return ,$frames
}

for ($row = 0; $row -lt 5; $row++) {
    $idle = Read-Frames $row
    $walk = Read-Frames (5 + $row)
    if ($idle.Count -eq 0 -or $walk.Count -eq 0) { throw "Missing original Nyuel direction $row" }
    for ($column = 0; $column -lt 20; $column++) {
        if ($column -lt 2) { $frame = $idle[[int][Math]::Floor($column * $idle.Count / 2)] }
        elseif ($column -lt 7) { $frame = $walk[[int][Math]::Floor(($column - 2) * $walk.Count / 5)] }
        else { $frame = $idle[0] }
        $graphics.DrawImageUnscaled($frame, $column * 96, $row * 96)
    }
    foreach ($frame in $idle) { $frame.Dispose() }
    foreach ($frame in $walk) { $frame.Dispose() }
}
$destination = "$projectRoot/Assets/AzureDreams/Resources/Town/Racetrack"
[System.IO.Directory]::CreateDirectory($destination) | Out-Null
$atlas.Save("$destination/NyuelIndexed.png")
$graphics.Dispose()
$atlas.Dispose()
$indices.Dispose()
Write-Output "Baked the original Nyuel idle and walking directions without the embedded rider."
