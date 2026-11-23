# Aura Bike
Purchase aura_bike in the general store. Interact near the parked bike outside Koh's house to mount/dismount. Directions accelerate/steer; release brakes. The mapped town jump control also jumps while riding, using Koh's same 1.8-unit height and gravity. Parking persists. Booster upgrades speed and original graphics. Dismount before entering buildings or handling items.

Original: scene_object_updates_g00578.c has F_get_bike 13, F_get_bike_b 14, saved positions and speed caps 0x1C0000/0x260000. initialize_effect_and_adjust_value_g01140.c has acceleration/braking and angular step (16 + speed)/4096. TOWN.BIN tables 800D0CC4/800D0CE8 and 800D0D0C/800D0D30 supply eight-facing parked/moving clips, palettes and ticks, reconstructed by Tools/extract_aura_bike.py against town VRAM. Zero-size dynamic rider markers are excluded from vehicle quads; Koh uses his existing player view.

Adaptation: 64 source units/tile, 60 ticks/sec. Movement input gives desired heading. Angular response is 3x the original formula, improving high-speed cornering. Acceleration/braking are tuned to these controls. Town collisions use <=0.1-unit substeps, allowing grounded rises up to 0.65 units and drops up to 1.01 units while preserving deep-water and building restrictions. In the air, the same feet-clearance and landing rules as walking Koh apply. Regular and booster top speeds are reduced to 80% of the earlier tuning (21 and 28.5 units/sec). Impact stops acceleration, adds speed-scaled opposite-travel recoil and a small bump; low-speed contacts have reduced strength and a shorter recovery (0.07-0.32 seconds), with steering allowed during gentle contacts; reverse movement also checks obstacles. Bike/booster no longer occupy furniture slots.

Validation: runtime assembly compiles. Isolated Unity test passed ownership, mounting, acceleration, high-speed 90-degree turning, stopping, booster ratio, parking persistence and wall collision. Native atlas inspected; complete riding appearance not verified in main scene.

Jump/recoil regression validation: regular and booster speed caps 21/28.5; measured jump apex 1.799995 units for both bike and walking Koh; rider/bike stay synchronized and land on the ground; dismount waits until landing; existing impact/reverse-obstacle tests pass. Full in-game riding/jump appearance remains to be checked.

Reverse: holding an input opposite the current facing (dot product below -0.65) brakes to zero, then reverses without turning the facing. Reverse caps at half forward maximum and accelerates at half the forward rate. Animation uses absolute speed. Small height changes use the next support height as wheel clearance, allowing low flower/plant surfaces and bridge-to-shallow-water transitions; native deep-water, tall-obstacle and building restrictions remain.

Reverse/terrain regression checks passed half-speed and half-acceleration caps, braking before reversal, facing retention, soft-contact steering, shallow bridge descent, low flower surfaces, and rejection of deep water/buildings. Existing jump and recoil regression checks also passed.
`nSummon: press T while on foot in the town exterior after purchasing Aura Bike. The bike parks 1.25 units ahead in Koh's facing; if that position is obstructed it parks under Koh instead. Parking is saved. Disabled while riding or inside buildings.

Acceleration dust: dedicated AuraBikeDust particle system and Resources/Shaders/AuraBikeDust shader. World-space overlapping soft tan puffs, up to 110 particles/sec, 0.5-0.95 second lifetime, growing and fading; maximum 256 particles. Emission follows actual acceleration and movement, reverses with travel, skips airborne/recoil/blocked/shallow-water cases, reuses one emitter and owned material per bike lifecycle.

## Riding presentation correction

Recovered rider table TOWN D_800D0C50: groups 225,224,223,224,225,226,227,226, with original sequence ticks. Tools/extract_aura_bike_rider.py installs Rider.bytes and RiderTimings.json. Koh now uses these riding poses instead of idle/walking body sprites and hides held weapons while mounted. Dismount restores normal presentation.

Both vehicle and rider use the native direction order (left/front-left/front/front-right/right/back-right/back/back-left), with mirrored directions 3..5. World/camera-relative Koh octants map with (6 - octant) & 7. The bike uses the resident pixel density 48/1.8 PPU, and the extractor measures the lowest parked wheel pixel for a fixed sole correction (1 pixel in the regular bank). ActorSpriteDepth provides the same object-depth treatment as NPC billboards.

Sound: native update_actor_movement_and_actions calls SD_Call(0x60A), at a period of 2..9 ticks based on speed. The original selector has been extracted to Sfx/Retail/60A.wav. Riding retriggers one dedicated voice at that cadence (absolute reverse speed), respects SFX volume, and stops on blocked movement, idle, dismount or mute. It does not accumulate overlapping one-shots.

Reverse steering now aims the nose opposite the requested travel direction: left input while reversing turns the nose right, and right input turns it left. Half reverse speed/acceleration, braking, terrain clearance and recoil remain intact.

Validation: runtime compilation; isolated GPU render of all eight rider/bike directions and matching mirroring/scale; reverse steering tests; prior collision, jump, reverse cap, terrain and summon probes. Audio verified separately in Play Mode. No full town saved-game playthrough was performed.
