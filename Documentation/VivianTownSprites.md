# Vivian: original field sprite extraction

`NPC_Vivian.prefab` previously used the entire 112×128 dialogue portrait as a
single field sprite. It now uses a separately recovered field atlas and native
animation data. Dialogue portraits, including the high resolution images, are
unchanged.

## Sources and identity

`Tools/extract_vivian_town_sprites.py` reads the original NTSC-U disc and verifies
the SHA-256 hashes of `SLUS_006.14` and `TOWN/TOWN.BIN` before decoding anything.
It reuses the GPU upload decoder in `extract_town_character_packages.py`.

- Resident addresses map to `TOWN.BIN` offsets by subtracting `0x8007D760`.
- The native initializer at `0x800CCA64` registers person slot **12** and binds
  the presentation tables at `0x800D6F74` and `0x800D6F7C`.
- Its private callback table at `0x800D6FA8` contains `0x800CCAAC`,
  `0x800CCB0C`, `0x800CCB6C`, `0x800CCBD0`. The common dispatcher subtracts
  22 from private action IDs. These are the developer symbols
  `SSTP_VIVIAN_TURN_R_INIT` (22), `TURN_L_INIT` (23), `DANCE_A_INIT` (24),
  `DANCE_B_INIT` (25) in `azure-dreams-decomp-master/include/script_symbols.h`.
- Selector **6**, LBA **6752**, 30 sectors, supplies the two original CLUTs:
  `0x76C0` (head) and `0x76C1` (body), at VRAM word `(0,475)` and `(16,475)`.
- Selector **1**, LBA **6782**, 21 sectors, supplies the 4bpp pieces at VRAM
  word `(640,384)` (TPAGE `0x1A`, UV 0–127 / 128–255).

The palettes and assembly are therefore recoverable; they are not absent from
the disc. They live separately from the image upload package. Other reserved
packages reuse these VRAM addresses and cannot be combined indiscriminately.

## Recovered data

The extraction produces **39 complete frames**, **20 unique timed sequences**,
two RGB555 palettes, and eight original four-direction animation tables:

| Runtime clip | Native direction table |
| --- | --- |
| stand | `0x800D6ED4` |
| idle | `0x800D6EE8` |
| turn_right | `0x800D6EFC` |
| turn_left | `0x800D6F10` |
| walk | `0x800D6F24` |
| gesture | `0x800D6F38` |
| step_in | `0x800D6F4C` |
| step_out | `0x800D6F60` |

The descriptive clip names are Unity labels. Each source sequence retains its
per-frame ticks and its hold (`kind 0`) or loop (`kind 1`) terminator. Playback
uses a 60-tick timebase, independent of Unity's rendering frame rate. Four-entry
direction tables reuse the side chain for left/right; the source chain faces
left and the view mirrors it for right. Every frame shares the original foot origin, including breathing and
walking offsets. No cropping or per-frame bounds normalization is applied.

All referenced texture words and palette words must have been written by the
source GPU commands; the extractor fails if any are unresolved. It also fails
on unknown commands, invalid control entries, missing loops or cropped frames.

The full extraction and provenance are saved outside Unity at
`../extracted_assets/reimagined_pipeline/TownResidents/Vivian/`, including
`manifest.json`, separate frames, palette words and a contact sheet.

## Runtime integration and regeneration

Run with Python and Pillow installed:

```powershell
python Tools/extract_vivian_town_sprites.py --install
```

This installs only `Resources/Town/Residents/Vivian.png` and `Vivian.json`.
`NPC_Vivian.prefab` references these through `RetailTownResidentSpriteView`;
`ActorLocomotionView` selects the recovered idle/walk animation and leaves its
native offsets intact. Camera-relative facing supports front, back, right and
mirrored left. All spawn sites that use the prefab receive the field sprite.

`PlayClip` exposes the other recovered clips with their original loop/hold
behavior. The original private dance controllers also move/rotate the actor;
the extracted sprite sequences are not a claim that the existing theater
choreography reproduces those controllers. No invented 3D mesh or portrait
texture is used for the field character.

## Validation

The runtime assembly compiles. A separate Unity 6000.6.7f1 Play Mode probe loads
the actual prefab and checks all 39 frames, eight clips in four directions,
source timings, loop and final-frame holds, side mirroring, camera rotation,
foot pivot, idle/walk transitions and continuous walking without per-frame
restarts. The old portrait view is absent. Captures and the passing report are
in `../Validation/Vivian/`; the user's open Editor was not interrupted.
