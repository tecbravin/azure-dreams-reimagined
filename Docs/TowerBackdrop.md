# Tower backdrop: retail correction

Evidence: `azure-dreams-decomp-master/src/dungeon/actor/state/advance_object_phase_counters.c`, `create_default_state_object.c`, and `depth_strip_projection_g00337.c`; the raw DUNGEON.BIN instruction at `0x800AFCA4` is `lbu ...,6(...)`, confirming that the angle consumes the low byte of the phase counter. Eight texture records at resident address `0x800DEF90` confirm alternating 32x64 half-quads in a 128x128 page.

- Normal angular counter: +2 per 30 Hz tick, or 5.2734375 degrees/second (4096 angular units/turn). Beldo: +4, or 10.546875 degrees/second. Low-byte wrapping is seamless because the texture pattern repeats after two 0x80 segments.
- Normal vertical phase does not move. Second-tower mode advances vertical phase by 0x40 modulo 0x1C00, without spinning.
- Native radius: 6*4096 PSX units, converted at 64 units/tile; 32 segments, 14 strips of 0xE00, native UVs and depth shading.
- Position height +0x4000 is applied after signed strip conversion. It must not overflow the last strips back above the floor.
- The background remains at its world origin for parallax. The background shader preserves projected XY but puts depth at the far plane, matching the retail background ordering table without shrinking it to the map camera's clipping distance.
- Removed the opaque black floor cap, which drew over the background. Native strips and black clear color fill the view.
- Tutorial/legacy geometry uses the same extracted per-floor retail background. Normal angular animation no longer rebuilds the mesh every frame; only second-tower vertical scrolling updates vertices.

This replaces the earlier conversion of the separate 32-pixel water-copy loop into a whole backdrop turn and the previous manually reduced speed multipliers.
