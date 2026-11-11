# Pulunpa Spawn Port

## Scope
First real C# slice of Pulunpa spawn factory (`create_actor_grid_object`, `spawn_pulunpa.c` lines 56-129). Only identity, ally/enemy flags, original sleep RNG roll, and normal/variant state selection are ported. Attribute formulas, animation tables, node allocation, position movement, and sprites are intentionally not invented.

## Component
- File: `Assets/AzureDreams/Scripts/Actors/PulunpaSpawn.cs`
- Type: `AzureDreamsReimagined.PulunpaSpawn : MonoBehaviour`
- Holds a fresh `AzureDreams.Core.ActorRecord` in `Record`.

## Initialize contract
```csharp
SpawnResult Initialize(
    int flags,
    int kindId,
    int variant,
    int spawnValue,
    AzureDreams.Core.RetailRng rng,
    byte variantCode)
```

Behavior mirrors the original:
- `Species` (`+0x13`) = `MonsterSpecies.PULUNPA` (0x15).
- `kind = flags & 3`.
- kind 1: `Flags14 |= ClassEnabled(0x2000) | PartyMember(0x4000)`; `Flags1C |= PlayerSide(0x2000) | PartyMember(0x4000)`; normal callback, FrameSet1.
- kind 2/3: `Flags14 |= ClassEnabled(0x2000)`; `Flags1C |= PlayerSide(0x2000)`; normal callback, FrameSet1.
- kind 0 with no extra flag bits:
  - if not sleep resistant (`!(Flags14 & 0x200)`): consume one RNG value; if odd consume a second and `SetStatus(3, StatusId.SLEEP, (random & 0x3F) | 0x20)` (slot 3 first for a new record).
  - if `variantCode < 10`: normal callback, FrameSet1 (overrides DirectionFrames when sleep was applied).
  - else: `ByteA7 = 1`, variant callback, FrameSet2.
- kind 0 with extra flag bits: normal callback, FrameSet1, no RNG.
- final raw bytes: `0x9A = 0xFF`, `0x9C = 0xFF`, `0xA8 = 0`; `0xA7 = 1` only in variant branch.

RNG source is `AzureDreams.Core.RetailRng`. No parallel Random class is introduced.

## Parent integration (no double TurnClock)
The component only allocates/initializes an `ActorRecord`. It does not advance turns, register nodes, move actors, or mutate global clocks. The controller should call `Initialize(...)` once per spawn and then feed `component.Record` into the existing actor pipeline. Do not call spawn factory logic inside a turn tick.

## Tests and evidence
- EditMode: `Assets/Tests/Editor/PulunpaSpawnTests.cs` — 7/7 passed.
- PlayMode: `Assets/Tests/PulunpaSpawnRuntimeTests.cs` — 1/1 passed, component exercised on a real GameObject.
- RED evidence: compile failure recorded before implementation in isolated clone (`pulunpa-red.log`).
- GREEN logs: `pulunpa-green-edit2.xml`, `pulunpa-green-play.xml`.

## Files and hashes
- `Assets/AzureDreams/Scripts/Actors/PulunpaSpawn.cs` — ba24376e93b40725e1793b00f53dc1aca5655bdef2561642f9eace4f0153643c
- `Assets/Tests/Editor/PulunpaSpawnTests.cs` — 0c57656d3c2a06865e1ab42e4af4f576285770e54a131f7c6ceb3063592d41c9
- `Assets/Tests/PulunpaSpawnRuntimeTests.cs` — f5cff3620c73f9ed4b6827c5209e051a5f6ec12269e79d288c8da2d65a7148a8

## Limitations
- New records start with `Flags14 = 0`, so the sleep-resistance branch is not reachable for a freshly allocated record; it is represented in the port but not exercised.
- `unk_8C` callback pointers and frame tables are represented as choices, not actual engine addresses/tables.
- Visual binding for Pulunpa is unresolved; no sprite/prefab was invented.
- Core.dll is referenced only; source Core was not edited or rebuilt.
