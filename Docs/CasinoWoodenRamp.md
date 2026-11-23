# Casino 2 wooden ramp

The captured room's surface 10 contains the original wooden incline (x14..18, z9..10, y4..0), upper platform (x13..14, y4), and adjacent upper sloped faces. Navigation previously sampled -128 (solid) in the ramp cells and only recovered horizontal furniture, so the diagonal could not be walked.

CasinoRamp extracts only nonvertical wooden top triangles, excluding the taller rail caps. Their barycentric heights supply ground height/open-floor checks. A per-step height gate blocks entry through the underside or high sides while allowing incremental ascent. A solid underside collision mesh is generated separately without changing the visible original mesh. It is owned by the existing generated-geometry cleanup.

Holding downhill (+X) on a supported sloped face starts a seated slide, accelerating from 2.5 to 7 units/sec. It follows the incline through collision-checked substeps, stops at an obstacle or the bottom and restores normal movement/presentation. Upstairs walking remains ordinary movement. Roulette gameplay control modes take priority.

The user identified the man without money: casino_broke moves from catalog cell (17,17) to (17,19), two tiles toward the bottom of the room. Other residents retain their positions.

Validation in the main Unity Editor: 2/2 CasinoRampTests passed. Actual MoveInTown walked from (18.25,0.55,9.5) to the upper platform at (13.64,4.55,9.5), then entered the slide and returned to (18.17,0.55,9.5), with slide mode cleared. Tests also reject entry through the underside, accept supported feet on the slope, and verify the generated MeshCollider.
