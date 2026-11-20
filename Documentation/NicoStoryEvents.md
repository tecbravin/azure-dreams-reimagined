# Nico story route

The Nico route follows the day-by-day order recovered from the town-event reference and retains the original completion flag `Ft_niko_love` (`5230`).

| Day | Trigger | Result |
| --- | --- | --- |
| 0 | Talk to Nico's father at her house | Nico enters, walks forward, and introduces her culture theme. |
| 1 | Donate 1,000 G at the town lake | First fountain donation. |
| 2 | Nico's wake-up visit, then donate again | The fountain is funded and built. |
| 3 | Talk to Nico at the fountain | Fountain conversation. |
| 7–12 | After Selfi gives the egg, speak to Nico at home | Step 6 and all five Step 7 conversations. |
| 13 | Speak to Chero at the fountain | Ask Chero to play, choose **Help him** with Ghosh, then **The music was beautiful**. |
| 14 | Nico's wake-up visit and Ghosh at her door | Nico invites Koh inside. |
| 15–16 | Speak to Nico at home | Steps 10 and 11. |
| 17 | Enter Nico's house | Final conversation and route completion. |

Nico's eight original portrait variants are available under `StreamingAssets/Media/Portraits/Dialogue`. Their four-frame expressions animate while a Nico story dialogue is open.

## Dialogue evidence

Nico's spoken pages are decoded directly from the US `TOWN.BIN` Shift-JIS
script data, rather than being paraphrased. The implemented route uses the
following verified text ranges:

| Retail file offset | Route use |
| --- | --- |
| `0x418DE3` | Culture speech and the recurring "step above" line. |
| `0x418F7D`–`0x41915B` | The Selfi conversation and its follow-up pages. |
| `0x4191B9`–`0x41960B` | Donation, ambition and collecting-donations pages. |
| `0x419B6C`–`0x419BE7` | Nico's offer to help. |
| `0x4189C5`–`0x418D6C` | Cello and culture pages used before the final scene. |
| `0x41AF13`–`0x41AFF0` | Final confession pages. |

The Portuguese text remains a localisation of those verified English pages;
the English selection preserves the retail wording and page breaks.
