# Point and Click sample — 3D

A vault with a lever, a wall, a key behind that wall, and a gate. Driven by
`Physics3DGameDriver`.

Import this one if **your game's clickable things are meshes with colliders**.

## After importing

1. **`DemoGame > Rebuild Vault (3D) Scene`** — creates `Assets/Scenes/Vault3D.unity` and
   registers it in Build Settings.
2. **`TestGoblin > Export Keyword Catalog`** — publishes this sample's keywords.
3. **`TestGoblin > Install Sample Scenarios`** — copies its scenario where the runner looks.

Press Play: click the lever, then the key, then the gate.

## What this sample is actually for

Occlusion. **The game does not forbid taking the key while the wall is in front of it** —
look at `VaultState.CanTakeKey`, it says nothing about the wall. What stops you is
geometry, and only a driver that raycasts can tell.

That makes it a real test of the driver rather than of the game:

- `TakingTheKey_IsRefusedWhileTheWallIsInTheWay` fails if the driver ever stops checking
  reachability — the game would carry on working perfectly, and nothing else would notice.
- `TakingTheKey_SucceedsOnceTheLeverRetractsTheWall` proves the same click starts working
  once the obstruction moves.

The wall is **moved**, not deactivated. Deactivating it would make the key reachable for
the trivial reason that nothing is there any more; moving it keeps a real collider in the
scene, so the raycast is demonstrably what changed its mind.

## Worth copying into your own game

- **Do not encode reachability as a game rule.** If the game itself refuses the click, the
  driver's check is never exercised and a regression in it is invisible.
- **Handle `IPointerClickHandler`, not `OnMouseDown`.** The driver dispatches pointer
  events; `OnMouseDown` would need it to call into your code by name instead of simulating
  a click.
- **Name objects deliberately.** They are the locators; renaming one breaks every scenario
  that uses it.
