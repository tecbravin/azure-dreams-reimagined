# Original Monsbaiya exterior-object placements

The constructed exterior objects are now parented beneath **Monsbaiya objects
— original retail lots** at runtime. Their positions are tied to the original
Town lot records, rather than to a separately authored scene transform.

The source is `TOWN.BIN` at resident address `0x800D2EA4`. Each of its
eight-byte records gives an x/z origin and width/height in the Town's 128 by
128 grid. One Unity world unit is one Town grid cell in the retail Town.

| Object | Original lot | Native x/z | Size |
| --- | ---: | ---: | ---: |
| Koh house upgrades | 11 | 50 / 44 | 12 x 14 |
| Hospital | 17 | 10 / 62 | 12 x 8 |
| Temple | 2 | 64 / 28 | 18 x 14 |
| Library | 7 | 88 / 28 | 16 x 14 |
| Fountain | 16 | 56 / 64 | 16 x 16 |
| Bowling alley | 5 | 40 / 28 | 16 x 14 |
| Gym | 28 | 24 / 86 | 16 x 14 |
| Racetrack entrance | 1 | 8 / 24 | 16 x 18 |
| Theatre | 4 | 24 / 28 | 16 x 14 |
| Casino | 31 | 88 / 86 | 16 x 14 |
| Arcade | 18 | 106 / 62 | 12 x 8 |

The rendered exterior mesh may extend beyond a lot by a tile or two for roof
and facade overhangs. The runtime verifies that every rendered construction
mesh still covers its corresponding original lot before it is added.
