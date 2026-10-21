# Introductory challenge geometry

Stages 102 and 103 use one existing piece each. Their solutions introduce translation
and then a quarter-turn before the larger original stage 101. Both are default entries
in `BuiltInStages`; `StageManager.AdditionalStages` may override either ID with an asset.
The original stage 101 target arrays and inventory are unchanged.

## Authored grid coordinates

The proof fixture reads `Assets/Scenes/stage1_1.unity`, its cell prefab instances,
their parent transforms, and the enabled trigger boxes in the actual I/O prefabs.
The flattened cell index is `8 * row + column`, with rows and columns numbered 0–7.
The resulting world-space ray origins and inward directions are:

| Surface | Origin at row r, column c | Direction |
| --- | --- | --- |
| Floor | `(r, 0, c)` | `(0, 1, 0)` |
| Wall | `(7 - c, 8 - r, -1)` | `(0, 0, 1)` |
| Front wall | `(-1, 8 - r, c)` | `(1, 0, 0)` |

Each ray travels up to eight world units, matching `ProjectionGrid`. Occupied trigger
boxes are 0.95 units on each axis. Disabled enclosing colliders are excluded, as they
cannot contribute ray hits. The I piece has root-local centers
`(-1.5, 0, 0)`, `(-0.5, 0, 0)`, `(0.5, 0, 0)`, `(1.5, 0, 0)`.
The O piece has centers `(±0.5, ±0.5, 0)` before rotation.

## Known solutions and keyboard route

Spawn one piece, apply the horizontal moves and rotation while it remains high above
the floor, then lower it with **A** until it lands. Each arrow press moves 0.5 world
units. **Right Arrow** decreases X; **Up Arrow** decreases Z. **Z** rotates +90° about X.

| Stage | Stock | Spawn root | Solution root | Rotation | Horizontal input |
| --- | --- | --- | --- | --- | --- |
| 102 | One I, block ID 3 | `(6, 9, 6.5)` | `(3.5, 1, 3)` | Identity | Right × 5, Up × 7 |
| 103 | One O, block ID 4 | `(6.5, 9, 6)` | `(3.5, 1, 3.5)` | +90° X | Right × 6, Up × 5, Z × 1 |

The spawn positions include the existing piece offsets from `stage.CreateLegacyBlockSpecs`.
Both solutions are on the existing half-cell translation lattice, sixteen downward
half-steps from their spawn heights. Landing may snap just below the listed root height
because of the 0.95-unit collider size; use **A** until landed rather than relying on a
particular number of key presses. The projection masks also hold at that contact pose.

| Stage | Floor indices | Wall indices | Front wall indices |
| --- | --- | --- | --- |
| 102 | 19, 27, 35, 43 | 58, 59, 60, 61 | 59 |
| 103 | 27, 28, 35, 36 | 59, 60 | 59, 60 |

For stage 102 the four occupied centers are `(2,1,3)` through `(5,1,3)`. For stage 103
they are the four combinations `(x,1,z)` with `x ∈ {3,4}` and `z ∈ {3,4}`. Every center
fits within the board and each floor projection has one supporting floor cell below it.

## Verification boundary

Run `python -m unittest discover -s tools -p 'test_challenge_layouts.py' -v` to recompute
the intersections from serialized Unity assets without third-party Python packages.
`BuiltInChallengeTests` independently checks that the runtime factories publish those
same masks and stock, that default selection works, and that authored overrides win.
These checks prove a matching geometric pose exists with the authored pieces and
reachable half-cell coordinates. They do not run Unity physics, animation, or Play mode.
A real Editor smoke test is still required to confirm the complete landing and input
sequence, including the original ghost-based collision and snapping behavior.
