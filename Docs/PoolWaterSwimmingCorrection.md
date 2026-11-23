# Pool water and swimming correction

Exit: restored-pool swimwear blocks leaving with `Preciso me trocar primeiro.` A contact latch is set before attempting a transition, so holding movement on the exit does not start a coroutine or refresh the message repeatedly. Walking outside the exit radius rearms it; normal clothes permit leaving. Pool reset clears the latch.

Swimming: the old swimsuit locomotion branch returned before the swimming branch, and the latter reused ordinary walking poses. Tools/extract_koh_swimming.py now installs the 35 native frames from alternate town groups 50..54, seven frames of six ticks each in five views. Source association: SSTP_PLT_DM_SWIM (41), callback 0x800992F8 -> D_800D0158 -> alternate table D_800CFFC0 = 52,51,50,51,52,53,54,53. Swimming selection precedes swimsuit locomotion and resumes normal swimsuit walking on exit.

Water: only the restored-pool material enables TownWater's new optional depth write. Other town water retains its prior setting. This establishes a stable water surface before sprite rendering and hides submerged body pixels. The original opaque flat floor under the pool is removed from the rendered geometry, exposing the existing reconstructed basin 1.2 units below the water. Water detection uses the captured water triangles, not the rectangular bounds, respecting the shaped corners/slide inlet.

Residents: PoolSwimmerView caches its room and retail view at configuration. In water, the visual is offset below the surface with a subtle bob; actor roots/navigation and dialogue reach remain unchanged. No per-frame component lookups or allocations were added by this component. All offsets restore on leaving the water.

Validation in main Unity Editor: 3 new pool regression tests, existing PoolChangingRoomTests and DialogueChoiceCloseRegressionTests passed (5/5). Tests cover contact/rearm, normal-clothes exit allowance, native water footprint and depth material, basin floor, swimming precedence/directions and return to walking. The actual captured scene was rendered with Koh and two submerged resident banks; see Validation/NpcRestaurant/pool-fixed.png. Full saved-game pool events were not replayed.
