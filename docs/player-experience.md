# Player feedback, challenges and local progression

The existing scene receives the experience automatically from `stage.Start()`. It adds a `GameplayExperience` coordinator and a self-owned UGUI overlay at runtime; no scene hierarchy or saved button reference is replaced. `GameplayExperience` joins the progression model, projection feedback and HUD. Each module can also be used independently.

## What the player gets

- A compact playing card with elapsed time, check count, blocks used, personal best, stars and the last check's floor/wall/front error counts.
- Dedicated **Check**, **Retry** and **Menu** buttons, plus an **Achievements & settings** drawer.
- Check feedback on each projection cell: amber means a required cell is missing, pink means unexpected occupancy, and teal means a required cell matches. Text counts and a legend accompany the colors. Colors describe the **last check**, not continuous live physics; check again after moving a piece.
- A short clear toast and a fixed pool of 16 decorative celebration particles. Achievements and improving checks produce queued feedback. The queue is bounded; returning to the menu or restarting clears obsolete effects.
- **Hints**, **High contrast** and **Reduced motion** settings. Reduced motion disables moving celebration particles and toast movement. These device-local choices persist separately from game progress.
- Two short challenges, **Warm-up I (102)** and **Flat square (103)**, alongside **Original tower (101)**. Select them from the new menu shortcut row. Exact layouts and reproducible placements are in [challenge layouts](challenge-layouts.md).

The overlay respects `Screen.safeArea` and uses scaled UGUI text and touch controls. Decorative graphics do not intercept input. The drawer's modal backdrop exists only while open; it keeps touches from reaching gameplay through the drawer. Keyboard and legacy piece input are also suspended while it is open, while the moving piece retains ownership. The authored scene's existing EventSystem continues to handle input.

## Score and stars

Only the first solved check in an attempt records a clear. Further checks of that completed attempt leave its score, timer, check count and awards frozen. Retry starts a new attempt with a new session ID.

```text
score = max(100, 1000 - ceil(elapsedSeconds) * 2 - (checks - 1) * 100)
3 stars: first check and elapsed <= 60 seconds
2 stars: at most 3 checks and elapsed <= 180 seconds
1 star: any other clear
```

Time uses Unity's unscaled clock, so slowing game time does not create a scoring advantage. Opening the achievements drawer does not pause the challenge timer. The timer freezes after the clear is recorded.

Each stage retains its highest score, fastest clear and highest star count independently. Total XP is the sum of each stage's best score: replaying an equal or worse result cannot farm XP. Improving a stage adds only the difference. Consecutive checks that reduce the error count show an improvement combo; it is encouragement rather than an extra score multiplier.

## Local achievements

| Stable ID | Name | Requirement |
| --- | --- | --- |
| `first_block` | First step | Successfully spawn a block |
| `first_clear` | Shadow solver | Clear any stage |
| `first_try` | First-check finish | Clear on the first check |
| `swift_clear` | Quick thinker | Clear within 60 seconds |
| `three_stages` | Three-dimensional thinker | Clear three distinct stages |
| `builder` | Block enthusiast | Successfully spawn 10 blocks across attempts |

“Use a block” means a committed spawn whose stock was debited, not a confirmed physical landing. Rejected or canceled generation does not count. Stable IDs determine unlocks; display names can change without awarding them again. These are local achievements, without a platform account or an external achievement service.

## Storage and failure handling

`ProgressionTracker` depends on `IProgressStore`, not Unity APIs. `PlayerPrefsProgressStore` implements that boundary with the versioned key `StackAndSlide.Progress.v1`. Its bounded, invariant text codec works on the stored Unity version without a JSON package and validates the entire payload before accepting it. Settings use the prefix `StackAndSlide.Experience.v1.`.

Progress is saved on accepted block-use changes and clears, rather than every frame. A failed write retains progress in memory and marks the HUD record as **not saved**. Pausing the application retries a previously failed save. Storage errors do not stop gameplay. A malformed or unsupported save starts with an empty in-memory profile; deleting application data also removes these local records.

For cloud saves or a platform achievement service, implement another `IProgressStore` or consume validated profile snapshots. Keep authentication, transport and platform calls outside the pure score and achievement model.

## Session and event ordering

`StageSessionInfo`, `BlockSpawnInfo` and detailed `ProjectionResult` carry session IDs. Two attempts at the same stage are distinct. Cumulative successful-spawn counts are authoritative, so duplicate or delayed notifications cannot count one block twice.

The built-in experience listens to internal commit notifications before public `SessionChanged`, `BlockSpawned` and `PuzzleEvaluated` callbacks. A public result listener may immediately go to the menu or another stage without discarding a clear that was already committed. Effects check the current session and cannot repaint a newer attempt with an old result.

`stage.CurrentSession` supports late binding and re-enabling the coordinator. The view renders supplied `ExperienceHudState` data and only raises actions for input; rendering a model does not emit button events. Disable/destroy removes subscriptions and hides or disposes the owned HUD canvas. The pure domain never creates a Unity object or reads the clock itself.

## Validation boundaries

Run the portable C# suite and all Python tooling tests:

```sh
python3 tests/run_gameplay_tests.py
python3 -m unittest discover -s tools -p 'test_*.py' -v
```

The C# suite checks score/star boundaries, repeated checks, replay XP, save validation and failure recovery, event ordering, session cancellation, diagnostics, UI input wiring, safe-area calculations and bounded effect ownership using API substitutes. The Python geometry tests read the actual scene and prefab YAML for the two new challenges. Neither suite imports or renders a Unity scene.

In the intended Unity editor, open `Assets/Scenes/stage1_1.unity` and smoke-test all three menu shortcuts; spawn, move, rotate, land and check; inspect every board face and the cell legend; retry and return to menu during feedback; scroll the achievements; toggle each setting; rotate/resize the screen; and reload the app to check persistence. Verify native touch targets, masking, text, material colors, particles and ghost alignment. Unity 6.6 import, licensing, Play mode and Android execution still require the Windows editor checks in [the migration guide](unity-upgrade.md).
