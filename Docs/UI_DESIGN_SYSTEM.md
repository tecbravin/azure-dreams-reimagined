# Runtime window design system

All game menus use the existing IMGUI window renderer. `WindowDesignSystem` is the shared contract; do not add per-scene fonts, background colors or opacity overrides.

| Element | Rule |
| --- | --- |
| Font | `Resources/UI/WindowFont` (Liberation Sans), shared by both art modes and all scenes |
| Text | White in every interaction state; semantic item/status colors remain explicit |
| Background | RGB 0.025 / 0.027 / 0.12, alpha 0.74 |
| Frame | Original game frame through `DrawDialogueFrame`, with the same background in the fallback |
| Padding / gap | 12 / 8 logical UI units, before the existing screen scaling |
| Page corner | Same 16-unit icon at bottom right; speaker windows and small HUD badges are excluded |
| Footer | Reserve 24 units when laying out content above the page corner |
| Speaker | `DrawSpeakerWindow`: separate measured window above the dialogue, left aligned, 8-unit gap; narration has no name |
| Long text | Measure font metrics; fit single-line labels to their rectangles and scroll long dialogue rather than clipping it |

Use `DrawHudPanel` / `DrawDialogueFrame` for windows, `BeginHudMenu` for automatic content padding/footer, `DrawInventoryButton` for measured action labels, and `DrawDialogueText` for scrollable dialogue. `DrawContinuePrompt` adds click-to-advance behavior to the common page indicator; decorative page corners do not introduce an input action. Existing inventory pagination keeps its own next-page action in the same position.

Do not put speaker names inside the body or place dialogue choices outside the main dialogue window. Keep larger menu panels tall enough for their rows; fitting is a safeguard for localized labels, not a substitute for layout space. When adding a fixed-position panel, reserve `Content(panel, footer: true)` before filling it.

`WindowDesignSystemTests` covers footer separation, speaker placement, font availability, opacity and text states. Validate actual screens in Play Mode after changing their individual layouts, including Portuguese text and large inventory values.
