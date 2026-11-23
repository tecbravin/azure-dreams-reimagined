# Town construction locations and doors

Verified against the US TOWN.BIN building records at `0x800D2644` (file `0x54EE4`), parcel table at `0x800D2EA4`, `CheckBuildBuildingLand`, and the tile grid of `ad_model_town_max`. Every nonzero tile of each exterior below matches its native stamp in the developed-town capture (108–236 matches per building).

| Building | Native record / stamp | Parcel footprint | Capture location / default parcel | Door | Interior |
|---|---|---|---|---|---|
| Casino | 6 / 17 | 16×14 | 24,28 / slot 4 | 32,36 | Interior_2b_casino_1 |
| Library | 7 / 22 | 12×8 | 106,62 / slot 18 | 112,69 | Interior_07_library |
| Gym | 8 / 23 | 16×14 | 88,28 / slot 7 | 96,38 | Interior_08_gym |
| Amusement center | 9 / 24 | 16×14 | 24,86 / slot 28 | 32,95 | Interior_09_arcade_1 |
| Theater | 11 / 26 | 16×14 | 40,28 / slot 5 | 48,37 | Interior_0b_theater |
| Bowling alley | 12 / 31 | 16×14 | 88,86 / slot 31 | 96,95 | Interior_0c_alley |

The five interchangeable parcels are slots 4, 5, 7, 28 and 31. The original compatibility check compares the building's width and height with the parcel's width and height, excludes occupied/pending assignments and excludes the protected slot. Library has only one free compatible 12×8 parcel (slot 18); it is not one of the interchangeable 16×14 projects. Upgrades, temple, fountain and racetrack retain their dedicated sites.

The earlier extracted filenames misidentified the exteriors: old theater=casino, old alley=theater, old library=gym, old arcade=library, old gym=amusement center, old casino=bowling. `Tools/build_construction_lots.py` now uses the verified identities and supports `--only` to rebuild individual exterior assets without overwriting unrelated house repairs.

All project geometry bounds, collision footprint, doorway and return position translate together when choosing another lot. Touching a constructed doorway uses its project destination; leaving uses the corresponding translated outside point. Confirmation rechecks whether the selected parcel is still free before charging gold. Repairs preserve valid explicit saved choices; missing, invalid or duplicate active/pending assignments receive distinct free parcels, with the native capture parcel preferred. Old library lot keys no longer reserve a 16×14 parcel. No new purchase or reset is required.
